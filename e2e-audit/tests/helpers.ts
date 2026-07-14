import { Page, TestInfo, APIRequestContext, expect } from '@playwright/test'

export const SHOTS = '../docs/reviews/screenshots'

// Attach console + network error capture to a page. Returns the collected arrays.
export function watch(page: Page) {
  const consoleErrors: string[] = []
  const pageErrors: string[] = []
  const netFail: string[] = []
  const slow: string[] = []
  const reqStart = new Map<string, number>()

  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()) })
  page.on('pageerror', (e) => pageErrors.push(e.message))
  page.on('request', (r) => reqStart.set(r.url(), Date.now()))
  page.on('response', (r) => {
    const s = r.status()
    const started = reqStart.get(r.url())
    const dur = started ? Date.now() - started : 0
    if (s >= 400) netFail.push(`${s} ${r.request().method()} ${r.url()}`)
    if (dur > 3000 && r.url().includes('/api/')) slow.push(`${dur}ms ${r.url()}`)
  })
  return { consoleErrors, pageErrors, netFail, slow }
}

// Navigate to a buyer/vendor persona + hash route, wait for network idle.
export async function goAs(page: Page, persona: string, route: string) {
  await page.goto(`/?as=${persona}#${route}`, { waitUntil: 'networkidle' })
}

export async function shot(page: Page, name: string) {
  await page.screenshot({ path: `${SHOTS}/${name}.png`, fullPage: true }).catch(() => {})
}

// Resolve an RFQ's server-generated id from its stable Code (e.g. RFQ-2026-0087). Seed GUIDs are
// non-deterministic across DB reseeds, so tests must look ids up by code rather than hardcode them.
export async function rfqIdByCode(request: APIRequestContext, code: string, persona = 'u_faridah'): Promise<string> {
  const res = await request.get('/api/rfqs', { headers: { 'X-Demo-User': persona } })
  expect(res.ok(), `GET /api/rfqs failed (${res.status()})`).toBeTruthy()
  const rfq = (await res.json()).find((r: { code: string; id: string }) => r.code === code)
  expect(rfq, `RFQ ${code} not found in seed`).toBeTruthy()
  return rfq.id
}

// Assert a page has no JS/page errors; network 4xx/5xx and slow reqs are reported but not hard-failed here.
export function reportHealth(w: ReturnType<typeof watch>, info: TestInfo, name: string) {
  const lines: string[] = []
  if (w.pageErrors.length) lines.push(`PAGE ERRORS: ${w.pageErrors.join(' | ')}`)
  if (w.consoleErrors.length) lines.push(`CONSOLE ERRORS: ${w.consoleErrors.slice(0, 5).join(' | ')}`)
  if (w.netFail.length) lines.push(`NET 4xx/5xx: ${[...new Set(w.netFail)].join(' | ')}`)
  if (w.slow.length) lines.push(`SLOW>3s: ${[...new Set(w.slow)].join(' | ')}`)
  if (lines.length) info.annotations.push({ type: `health:${name}`, description: lines.join('\n') })
  return lines
}

/** CF-FIX2-T2: drive the searchable select — open by label, type-to-filter, Enter picks
 *  the highlighted match (its proven keyboard contract). */
export async function pickSearch(page: Page, label: string, filter: string) {
  await page.getByRole('button', { name: label, exact: true }).click()
  const box = page.getByRole('combobox', { name: `Search ${label}` })
  await box.fill(filter)
  await page.getByRole('listbox', { name: `${label} options` }).getByRole('option').first().waitFor()
  await page.keyboard.press('Enter')
}

// CF-FIX-3 era: admins can author REQUIRED custom fields at any time (e.g. the operator's
// shared 'Partner' field). Tests that save custom values must satisfy required-and-empty
// defs GENERICALLY rather than assume none exist (established precedent — 'Remarks').
export async function requiredCustomValues(
  request: APIRequestContext, recordType: string, recordId: string, persona = 'u_faridah',
): Promise<Record<string, string>> {
  const res = await request.get(`http://localhost:5260/api/custom-values/${recordType}/${recordId}`,
    { headers: { 'X-Demo-User': persona } })
  const out: Record<string, string> = {}
  for (const d of await res.json()) if (d.required && !d.value) out[d.code] = 'e2e'
  return out
}

// Same, but on-screen: fill any required-and-empty custom inputs before clicking Save.
export async function fillRequiredCustomFields(
  page: Page, request: APIRequestContext, recordType: string, recordId: string, persona = 'u_faridah',
) {
  const needed = await requiredCustomValues(request, recordType, recordId, persona)
  const res = await request.get(`http://localhost:5260/api/custom-values/${recordType}/${recordId}`,
    { headers: { 'X-Demo-User': persona } })
  const defs = await res.json()
  for (const code of Object.keys(needed)) {
    const label = defs.find((d: { code: string }) => d.code === code)?.label
    if (!label) continue
    const el = page.getByLabel(label, { exact: true }).first()
    if (await el.count()) await el.fill('e2e')
  }
}

// CF-FIX4-T4: UI-created header fields are cascade-PLACED on forms, and CF-FIX-3's guard
// rightly blocks deleting a placed field. Cleanups unplace first (the governed path),
// then delete. Uses the surgical unplace endpoint (works on system forms for custom keys).
export async function hardDeleteField(
  request: APIRequestContext, def: { id: string; code: string }, expectStatus = 204,
) {
  const H = { 'X-Demo-User': 'u_admin' }
  const report = await (await request.get(`http://localhost:5260/api/custom-fields/${def.id}/references`, { headers: H })).json()
  for (const ref of report.configReferences ?? [])
    if (ref.consumerName === 'Entry Forms' && ref.targetId)
      await request.delete(`http://localhost:5260/api/entry-forms/${ref.targetId}/fields/${encodeURIComponent(def.code)}`, { headers: H })
  const res = await request.delete(`http://localhost:5260/api/custom-fields/${def.id}`, { headers: H })
  expect(res.status(), `hard-delete ${def.code}`).toBe(expectStatus)
}

// CF-FIX5-T3: drag was REMOVED from the entry-form builder (arrows are the sole reorder
// mechanism — proven in unit tests + a dedicated browser arrow proof). Older tests that
// needed a field parked in a specific group/subtab drove it by dragging; they now set the
// placement through the SAME whole-form save the app uses (a pure layout write).
export async function moveFieldViaForm(
  request: APIRequestContext, recordType: string, formId: string, fieldKey: string,
  target: { group?: string; subtab?: string | null },
) {
  const H = { 'X-Demo-User': 'u_admin', 'Content-Type': 'application/json' }
  const cur = (await (await request.get(`http://localhost:5260/api/entry-forms?recordType=${recordType}`, { headers: H })).json())
    .find((f: { id: string }) => f.id === formId)
  await request.put(`http://localhost:5260/api/entry-forms/${formId}`, { headers: H, data: {
    name: cur.name, recordType,
    fields: cur.fields.map((f: { fieldKey: string; fieldGroup: string; subtab: string | null }) => f.fieldKey === fieldKey
      ? { ...f, fieldGroup: target.group ?? f.fieldGroup, subtab: target.subtab !== undefined ? target.subtab : f.subtab }
      : f),
  } })
}

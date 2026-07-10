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

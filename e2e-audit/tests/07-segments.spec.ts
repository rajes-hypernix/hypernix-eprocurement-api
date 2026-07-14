import { test, expect } from '@playwright/test'
import { goAs, shot, pickSearch } from './helpers'

// D6 GATE (as ruled): an ADMIN defines "Project" with three values and applies it to
// Requisition + PurchaseOrder + Invoice in the Setup screen; a BUYER assigns it on one
// record of each type; a PO view filters by the segment; a SLICED KPI (the UI proof of
// group-by) lands on her dashboard with the named Unassigned bucket; series-group-by is
// proven at the API level (as ruled — the stacked chart portlet is BACKLOG); and the
// (iii-a) parity evidence pins that the four SYSTEM segments mirror the PR's dimension
// columns exactly — same DimCode derivation, write-refused ("edit the PR").
// Run-stamped + self-cleaning (assignments nulled, applications removed; the empty def
// remains — defs have no delete surface by design, dimension keys are never dropped).

const API = 'http://localhost:5260'
const STAMP = Date.now().toString().slice(-6)
const SEG_NAME = `Project ${STAMP}`
const SEG_CODE = `seg_project_${STAMP}`
const VIEW_NAME = `Alpha Plant POs (${STAMP})`
const KPI_TITLE = `PO value by project (${STAMP})`
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }

// SourcingMapping.DimCode replicated verbatim: uppercase, each non-alphanumeric → '-',
// trim '-', cap 40. The (iii-a) condition: ONE derivation for columns and segment values.
const dim = (label: string) =>
  [...label.toUpperCase()].map((c) => (/[A-Z0-9]/.test(c) ? c : '-')).join('').replace(/^-+|-+$/g, '').slice(0, 40)

test('segments gate: admin defines → buyer assigns → view filters → sliced KPI → (iii-a) parity', async ({ page, request }) => {
  // ---- 1. ADMIN defines the segment + three values, applies to PR/PO/Invoice ----
  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New segment' }).click()
  await page.getByLabel('Name', { exact: true }).fill(SEG_NAME)
  await page.getByRole('button', { name: 'Create segment' }).click()
  await page.waitForTimeout(800)
  // CF-FIX4-T6: values STAGE (the lists convention) — three staged, ONE Save commits.
  for (const v of ['Alpha Plant', 'Beta Plant', 'Gamma Yard']) {
    await page.getByLabel('Label', { exact: true }).fill(v)
    await page.getByRole('button', { name: 'Add value' }).click()
  }
  await page.getByRole('button', { name: 'Save values' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText('ALPHA-PLANT')).toBeVisible()     // the DimCode-derived key, on the glass
  for (const rt of ['Requisition', 'PurchaseOrder', 'Invoice']) {
    await page.getByRole('button', { name: `Apply ${SEG_NAME} to ${rt} header`, exact: true }).click()
    // CF-FIX4-T7: header apply runs the form+group cascade — accept the defaults
    // (standard form pre-selected, Header group).
    await page.getByRole('button', { name: 'Apply segment' }).click()
    await page.waitForTimeout(600)
  }
  await shot(page, 'D6-gate-1-define')

  // Resolve the run-stamped def code (server derives it from the name).
  const defs = await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json()
  const def = defs.find((d: { name: string }) => d.name === SEG_NAME)
  expect(def, 'the new segment def exists').toBeTruthy()
  const segKey = def.code as string

  // ---- 2. BUYER assigns it on one record of each applied type (PO through the UI) ----
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  const po = pos[0]
  await goAs(page, 'u_faridah', `pos/${po.id}`)
  await page.waitForTimeout(1500)
  const section = page.locator('div.card', { hasText: 'Segments' }).last()
  await expect(section).toBeVisible()
  await pickSearch(page, SEG_NAME, 'Alpha Plant')   // CF-FIX4-T6: segment pickers are the searchable select
  await shot(page, 'D6-gate-2-assign')
  await page.getByRole('button', { name: 'Save segments' }).click()
  await page.waitForTimeout(1000)

  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const invoices = await (await request.get(`${API}/api/invoices`, { headers: BUYER })).json()
  for (const [type, id] of [['Requisition', prs[0].id], ['Invoice', invoices[0].id]] as const) {
    const r = await request.put(`${API}/api/segment-assignments/${type}/${id}`, {
      headers: { ...BUYER, ...JSON_H },
      data: { assignments: { [segKey]: 'BETA-PLANT' }, lineId: null },
    })
    expect(r.ok(), `assign on ${type} (${r.status()})`).toBeTruthy()
  }

  // ---- 3. A PO view FILTERS by the segment key — same registry machinery as any field ----
  const view = await (await request.post(`${API}/api/views`, {
    headers: { ...BUYER, ...JSON_H },
    data: {
      name: VIEW_NAME, recordType: 'PurchaseOrder',
      filters: [{ fieldKey: segKey, operator: 'Eq', value: 'ALPHA-PLANT', value2: null }],
      columns: [{ fieldKey: 'Code' }, { fieldKey: 'Total' }],
    },
  })).json()
  const run = await (await request.get(`${API}/api/views/${view.id}/run`, { headers: BUYER })).json()
  expect(run.rows.length).toBe(1)
  expect(String(run.rows[0].Code)).toBe(po.code)

  // ---- 4. The SLICED KPI through the UI — group-by with the named Unassigned bucket ----
  const allPos = await (await request.post(`${API}/api/views`, {
    headers: { ...BUYER, ...JSON_H },
    data: { name: `All POs (${STAMP})`, recordType: 'PurchaseOrder', filters: [], columns: [{ fieldKey: 'Code' }] },
  })).json()
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'Add KPI' }).first().click()
  await page.getByLabel('KPI title').fill(KPI_TITLE)
  await pickSearch(page, 'Record type', 'Purchase Order')
  await pickSearch(page, 'Saved view', `All POs (${STAMP})`)
  await page.getByLabel('Function').selectOption('sum')
  await page.getByLabel('Field').selectOption('Total')
  await pickSearch(page, 'Slice by segment (optional)', SEG_NAME)
  await page.getByRole('button', { name: 'Add KPI' }).last().click()
  await page.waitForTimeout(1500)
  const kpi = page.locator('section', { hasText: KPI_TITLE })
  await expect(kpi.getByText('Alpha Plant')).toBeVisible()
  await expect(kpi.getByText('Unassigned')).toBeVisible()       // honest-null on dimensions, on the glass
  await shot(page, 'D6-gate-3-sliced-kpi')

  // ---- 5. Series group-by, proven at the API level (as ruled; stacked portlet = BACKLOG) ----
  const prView = await (await request.post(`${API}/api/views`, {
    headers: { ...BUYER, ...JSON_H },
    data: { name: `PRs (${STAMP})`, recordType: 'Requisition', filters: [], columns: [{ fieldKey: 'Code' }] },
  })).json()
  const series = await (await request.get(
    `${API}/api/views/${prView.id}/series?fn=count&bucket=RaisedDate&months=24&groupBy=${segKey}`,
    { headers: BUYER })).json()
  expect(series.groupedBy).toBe(segKey)
  const keys = series.series.map((s: { key: string }) => s.key)
  expect(keys).toContain('BETA-PLANT')                          // the PR assigned in step 2
  expect(keys).toContain('__unassigned')                        // every other PR — surfaced, never dropped
  const total = series.buckets.reduce((s: number, b: { value: number }) => s + b.value, 0)
  const grouped = series.series.flatMap((g: { buckets: { value: number }[] }) => g.buckets)
    .reduce((s: number, b: { value: number }) => s + b.value, 0)
  expect(grouped).toBe(total)                                   // the slices reconcile to the whole

  // ---- 6. (iii-a) PARITY EVIDENCE: system segments ≡ PR dimension columns ----
  const SYSTEM: Array<[string, string]> = [
    ['seg_department', 'department'], ['seg_location', 'location'],
    ['seg_category', 'category'], ['seg_job', 'job'],
  ]
  for (const prRow of prs.slice(0, 3)) {
    const pr = await (await request.get(`${API}/api/requisitions/${prRow.id}`, { headers: BUYER })).json()
    const assigns = await (await request.get(`${API}/api/segment-assignments/Requisition/${prRow.id}`, { headers: BUYER })).json()
    for (const [segCode, prop] of SYSTEM) {
      const a = assigns.find((x: { segmentCode: string }) => x.segmentCode === segCode)
      const label = (pr[prop] ?? '') as string
      if (!label) { expect(a?.valueCode ?? null).toBeNull(); continue }
      expect(a, `${segCode} present on ${pr.code}`).toBeTruthy()
      expect(a.valueCode, `${segCode} code ≡ DimCode(${prop}) on ${pr.code}`).toBe(dim(label))
      expect(a.valueLabel, `${segCode} label ≡ column on ${pr.code}`).toBe(label)
    }
  }
  // Columns are the SINGLE truth: writing a system-segment assignment on a PR is refused.
  const refuse = await request.put(`${API}/api/segment-assignments/Requisition/${prs[0].id}`, {
    headers: { ...BUYER, ...JSON_H },
    data: { assignments: { seg_department: 'FINANCE' }, lineId: null },
  })
  expect(refuse.status()).toBe(400)                             // "edit the PR" — loud validation

  // ---- cleanup: dashboard reset, views deleted, assignments nulled, applications removed ----
  expect((await request.delete(`${API}/api/dashboards/mine`, { headers: BUYER })).status()).toBe(204)
  for (const v of [view.id, allPos.id, prView.id])
    expect((await request.delete(`${API}/api/views/${v}`, { headers: BUYER })).status()).toBe(204)
  const clear = { assignments: { [segKey]: null }, lineId: null }
  for (const [type, id] of [['PurchaseOrder', po.id], ['Requisition', prs[0].id], ['Invoice', invoices[0].id]] as const)
    expect((await request.put(`${API}/api/segment-assignments/${type}/${id}`, { headers: { ...BUYER, ...JSON_H }, data: clear })).ok()).toBeTruthy()
  for (const rt of ['Requisition', 'PurchaseOrder', 'Invoice'])
    expect((await request.delete(`${API}/api/segments/${def.id}/applications/${rt}`, { headers: ADMIN })).ok()).toBeTruthy()
})

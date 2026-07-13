import { test, expect } from '@playwright/test'
import { goAs, shot } from './helpers'

// D5 GATE (as ruled): an ADMIN creates "Warranty Expiry" (Date) on PurchaseOrder in the
// Setup screen; a BUYER populates it on a real PO, a PO saved view filters by it with the
// ruled @today+90d token form, and a KPI counting expiring POs lands on her dashboard —
// same session, zero deployments, performed by personas. Run-stamped + self-cleaning.

const API = 'http://localhost:5260'
const STAMP = Date.now().toString().slice(-6)
const FIELD_LABEL = `Warranty Expiry ${STAMP}`
const FIELD_CODE = `cf_warranty_expiry_${STAMP}`
const VIEW_NAME = `Expiring in 90 days (${STAMP})`
const KPI_TITLE = `Warranties expiring (${STAMP})`
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }

test('custom-field gate: admin defines → buyer populates → view filters → KPI counts', async ({ page, request }) => {
  // ---- 1. ADMIN creates the def in the Setup screen ----
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label').fill(FIELD_LABEL)
  // CF-FIX1-T7/T8: the type picker is the searchable select with full names now.
  await page.getByRole('button', { name: 'Data type' }).click()
  await page.getByRole('combobox', { name: 'Search Data type' }).fill('Date')
  await page.getByRole('listbox', { name: 'Data type options' }).getByRole('option', { name: 'Date', exact: true }).click()
  await shot(page, 'D5-gate-1-def')
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText(FIELD_CODE)).toBeVisible()

  // ---- 2. BUYER populates it on a real PO (the identical FieldSpec pipeline) ----
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  const po = pos[0]
  const inside = new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10)
  await goAs(page, 'u_faridah', `pos/${po.id}`)
  await page.waitForTimeout(1500)
  const section = page.locator('div.card', { hasText: 'Custom fields' }).last()
  await expect(section).toBeVisible()
  await page.getByLabel(FIELD_LABEL).fill(inside)
  await shot(page, 'D5-gate-2-populate')
  await page.getByRole('button', { name: 'Save custom fields' }).click()
  await page.waitForTimeout(1000)

  // ---- 3. A PO view filters by the custom key with the ruled @today+90d token form ----
  const view = await (await request.post(`${API}/api/views`, {
    headers: BUYER,
    data: {
      name: VIEW_NAME, recordType: 'PurchaseOrder',
      filters: [{ fieldKey: FIELD_CODE, operator: 'Lte', value: '@today+90d', value2: null }],
      columns: [{ fieldKey: 'Code' }, { fieldKey: FIELD_CODE }],
    },
  })).json()
  const run = await (await request.get(`${API}/api/views/${view.id}/run`, { headers: BUYER })).json()
  expect(run.rows.length).toBe(1)
  expect(String(run.rows[0].Code)).toBe(po.code)

  // ---- 4. The KPI, pinned through the UI — count matches the view's rows ----
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'Add KPI' }).first().click()
  await page.getByLabel('KPI title').fill(KPI_TITLE)
  await page.getByLabel('Record type').selectOption('PurchaseOrder')
  await page.getByLabel('Saved view').selectOption({ label: VIEW_NAME })
  await page.getByLabel('Function').selectOption('count')
  await page.getByRole('button', { name: 'Add KPI' }).last().click()
  await page.waitForTimeout(1500)
  const kpi = page.locator('section', { hasText: KPI_TITLE })
  await expect(kpi.getByText('1', { exact: true })).toBeVisible()
  await shot(page, 'D5-gate-3-kpi')

  // ---- cleanup: dashboard reset, view deleted; the def is deactivated (it has a value —
  // the ruled lifecycle forbids deletion, which is itself worth exercising) ----
  expect((await request.delete(`${API}/api/dashboards/mine`, { headers: BUYER })).status()).toBe(204)
  expect((await request.delete(`${API}/api/views/${view.id}`, { headers: BUYER })).status()).toBe(204)
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const def = defs.find((d: { code: string }) => d.code === FIELD_CODE)
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(409)   // values written → never deleted
  expect((await request.post(`${API}/api/custom-fields/${def.id}/active`,
    { headers: { ...ADMIN, 'Content-Type': 'application/json' }, data: 'false' })).ok()).toBeTruthy()
})

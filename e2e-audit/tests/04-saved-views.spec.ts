import { test, expect } from '@playwright/test'
import { goAs, shot } from './helpers'

// D3 Phase 3 GATE (as ruled): a PERSON builds and shares "Open RFQs closing this month"
// entirely in the UI as the buyer; a second internal persona picks it and sees the same
// rows; the vendor persona runs it and sees only its own reachable (invited) RFQs.
// The view name carries a run-stamp so re-runs stay clean; the view is deleted at the end.

const API = 'http://localhost:5260'
const STAMP = Date.now().toString().slice(-6)
const VIEW_NAME = `Open RFQs closing this month (${STAMP})`

const tableCodes = async (page: import('@playwright/test').Page) =>
  (await page.locator('table tbody tr td:first-child').allInnerTexts()).map((t) => t.trim()).filter(Boolean)

test('saved views gate: buyer builds + shares; approver sees the same rows; vendor is scoped', async ({ page, request }) => {
  // ---- 1. The buyer builds the view in the UI ----
  await goAs(page, 'u_faridah', 'rfqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New saved view' }).click()
  await page.getByLabel('View name').fill(VIEW_NAME)

  await page.getByRole('button', { name: 'Add criterion' }).click()
  await page.getByLabel('Field').selectOption('Status')
  await page.getByLabel('Operator').selectOption('Eq')
  await page.getByLabel('Status', { exact: true }).selectOption('Open')

  await page.getByRole('button', { name: 'Add criterion' }).click()
  await page.getByLabel('Field').nth(1).selectOption('ClosesUtc')
  await page.getByLabel('Operator').nth(1).selectOption('Between')
  await page.getByLabel('Value', { exact: true }).nth(0).selectOption('@startOfMonth')
  await page.getByLabel('Value', { exact: true }).nth(1).selectOption('@endOfMonth')

  await page.getByText('Shared (visible to everyone — publication)').click()   // buyer holds ManageSharedViews
  await shot(page, 'D3-gate-1-builder')
  await page.getByRole('button', { name: 'Save view' }).click()
  await page.waitForTimeout(1200)
  await shot(page, 'D3-gate-2-buyer-rows')
  const buyerCodes = await tableCodes(page)

  // ---- 2. A second internal persona picks the shared view and sees the SAME rows ----
  await goAs(page, 'u_lim', 'rfqs')
  await page.waitForTimeout(1200)
  await page.getByRole('combobox', { name: 'Saved view' }).selectOption({ label: VIEW_NAME })
  await page.waitForTimeout(1200)
  await shot(page, 'D3-gate-3-approver-rows')
  const approverCodes = await tableCodes(page)
  expect(approverCodes).toEqual(buyerCodes)

  // ---- 3. The vendor persona runs the shared view and sees ONLY its invited RFQs ----
  const views = await (await request.get(`${API}/api/views?recordType=Rfq`, { headers: { 'X-Demo-User': 'VU-sentausa' } })).json()
  const shared = views.find((v: { name: string }) => v.name === VIEW_NAME)
  expect(shared, 'the shared view is visible to the vendor').toBeTruthy()

  const run = await (await request.get(`${API}/api/views/${shared.id}/run`, { headers: { 'X-Demo-User': 'VU-sentausa' } })).json()
  const vendorCodes: string[] = run.rows.map((r: Record<string, unknown>) => String(r.Code))

  const reachable = await (await request.get(`${API}/api/rfqs`, { headers: { 'X-Demo-User': 'VU-sentausa' } })).json()
  const reachableCodes: string[] = reachable.map((r: { code: string }) => r.code)

  for (const code of vendorCodes) {
    expect(reachableCodes, `vendor row ${code} must be an RFQ sentausa is invited to`).toContain(code)
    expect(buyerCodes, `vendor row ${code} is a subset of the buyer's rows`).toContain(code)
  }
  expect(vendorCodes.length).toBeLessThanOrEqual(buyerCodes.length)

  // ---- cleanup: the buyer deletes the run-stamped view ----
  const del = await request.delete(`${API}/api/views/${shared.id}`, { headers: { 'X-Demo-User': 'u_faridah' } })
  expect(del.status()).toBe(204)
})

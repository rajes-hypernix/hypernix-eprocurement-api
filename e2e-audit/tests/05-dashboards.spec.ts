import { test, expect } from '@playwright/test'
import { goAs, shot } from './helpers'

// D4 Phase 4 GATE (as ruled): a PERSON builds a KPI from her own saved view, pins it to her
// personalized dashboard and arranges it; the number matches the view's row count; the
// vendor persona's dashboard shows only own-scope numbers. Run-stamped + self-cleaning.

const API = 'http://localhost:5260'
const STAMP = Date.now().toString().slice(-6)
const VIEW_NAME = `Open RFQs closing this month (${STAMP})`
const KPI_TITLE = `Closing this month (${STAMP})`
const H = { 'X-Demo-User': 'u_faridah' }

test('dashboard gate: buyer builds a KPI from her view, pins + arranges it; vendor sees own-scope only', async ({ page, request }) => {
  // Her own saved view (the D3 builder flow is gate-proven in 04; created via API here).
  const created = await (await request.post(`${API}/api/views`, {
    headers: H,
    data: {
      name: VIEW_NAME, recordType: 'Rfq',
      filters: [
        { fieldKey: 'Status', operator: 'Eq', value: 'Open', value2: null },
        { fieldKey: 'ClosesUtc', operator: 'Between', value: '@startOfMonth', value2: '@endOfMonth' },
      ],
      columns: [{ fieldKey: 'Code' }, { fieldKey: 'Title' }, { fieldKey: 'Status' }],
    },
  })).json()
  const run = await (await request.get(`${API}/api/views/${created.id}/run`, { headers: H })).json()
  const expectedCount = run.rows.length

  // ---- The person: Add KPI on the dashboard, from that view, target 3 ----
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'Add KPI' }).first().click()
  await page.getByLabel('KPI title').fill(KPI_TITLE)
  await page.getByLabel('Record type').selectOption('Rfq')
  await page.getByLabel('Saved view').selectOption({ label: VIEW_NAME })
  await page.getByLabel('Function').selectOption('count')
  await page.getByLabel('Target (optional)').fill('3')
  await shot(page, 'D4-gate-1-addkpi')
  await page.getByRole('button', { name: 'Add KPI' }).last().click()
  await page.waitForTimeout(1500)

  // Pinned on her personalized copy, showing the view's live row count + target.
  const kpiCard = page.locator('section', { hasText: KPI_TITLE })
  await expect(kpiCard).toBeVisible()
  await expect(kpiCard.getByText(String(expectedCount), { exact: true })).toBeVisible()
  await expect(kpiCard.getByText(/target 3/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Arrange portlets' })).toBeVisible()

  // ---- Arrange: move the KPI to the top, arrow-based ----
  await page.getByRole('button', { name: 'Arrange portlets' }).click()
  const up = page.getByRole('button', { name: `Move ${KPI_TITLE} up` })
  for (let i = 0; i < 12 && (await up.count()) > 0; i++) {
    const first = await page.locator('section[aria-label]').first().getAttribute('aria-label')
    if (first === KPI_TITLE) break
    await up.click()
    await page.waitForTimeout(120)
  }
  await page.getByRole('button', { name: 'Arrange portlets' }).click()   // Done → persists
  await page.waitForTimeout(1200)
  await expect(page.locator('section[aria-label]').first()).toHaveAttribute('aria-label', KPI_TITLE)
  await shot(page, 'D4-gate-2-arranged')

  // ---- Vendor: dashboard numbers are OWN-SCOPE (metric == the vendor's own API numbers) ----
  await goAs(page, 'VU-sentausa', 'dashboard')
  await page.waitForTimeout(1500)
  const vh = { 'X-Demo-User': 'VU-sentausa' }
  const toBid = await (await request.get(`${API}/api/metrics/vendorRfqsToBid/value`, { headers: vh })).json()
  const queue = page.locator('section', { hasText: 'Your work queue' })
  await expect(queue.getByText('RFQs to bid')).toBeVisible()
  await expect(queue.locator('.card.stat', { hasText: 'RFQs to bid' }).locator('.num')).toHaveText(String(toBid.value))
  await expect(page.getByText('RFQ invitations')).toBeVisible()
  await shot(page, 'D4-gate-3-vendor')

  // ---- cleanup: reset the buyer's dashboard, delete the run-stamped view ----
  expect((await request.delete(`${API}/api/dashboards/mine`, { headers: H })).status()).toBe(204)
  expect((await request.delete(`${API}/api/views/${created.id}`, { headers: H })).status()).toBe(204)
})

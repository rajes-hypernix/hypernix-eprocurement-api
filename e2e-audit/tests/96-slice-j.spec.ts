import { test, expect } from '@playwright/test'
import { goAs, shot, watch, rfqIdByCode } from './helpers'

// RFQ-2026-0087 (Open, has Declined/Rescinded/Extended). Resolved by code — seed GUIDs change per reseed.
let RFQ: string
test.beforeAll(async ({ request }) => { RFQ = await rfqIdByCode(request, 'RFQ-2026-0087') })

test('Buyer: RfqDetailHub shows invitation lifecycle + activity timeline', async ({ page }) => {
  const w = watch(page)
  await goAs(page, 'u_faridah', `rfqs/${RFQ}`)
  await page.waitForTimeout(1500)
  await shot(page, 'J-buyer-rfq-detail')
  await expect(page.getByRole('heading', { name: 'Invited Vendors' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Activity' })).toBeVisible()
  // lifecycle pills + governance actions present
  await expect(page.getByText('Declined').first()).toBeVisible()
  await expect(page.getByText('Rescinded').first()).toBeVisible()
  await expect(page.getByRole('button', { name: /Extend deadline/ })).toBeVisible()
  await expect(page.getByRole('button', { name: /Add vendor/ })).toBeVisible()
  // open the Extend modal (shows Extension n of max · Original close)
  await page.getByRole('button', { name: /Extend deadline/ }).click()
  await page.waitForTimeout(300)
  await shot(page, 'J-buyer-extend-modal')
  await expect(page.getByText(/Extension \d of 2/)).toBeVisible()
  await page.locator('.modal').getByRole('button', { name: 'Cancel' }).click()
  console.log('J buyer console errors:', JSON.stringify(w.pageErrors))
  expect(w.pageErrors).toEqual([])
})

test('Buyer: Add-vendor modal reuses picker behaviour (search + invited-disabled)', async ({ page }) => {
  await goAs(page, 'u_faridah', `rfqs/${RFQ}`)
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Add vendor/ }).click()
  await page.waitForTimeout(400)
  await shot(page, 'J-buyer-add-vendor')
  await expect(page.getByLabel('Search vendors')).toBeVisible()
  // at least one Invite button available
  await expect(page.getByRole('button', { name: /Invite/ }).first()).toBeVisible()
})

test('Vendor: bid page offers decline / intend affordances', async ({ page }) => {
  const w = watch(page)
  await goAs(page, 'VU-borneo', `bid/${RFQ}`)
  await page.waitForTimeout(1500)
  await shot(page, 'J-vendor-bid-page')
  // vendor sees governance affordances (some subset depending on their current status)
  const hasAction = await page.getByRole('button', { name: /Decline invitation|I intend to bid|Withdraw bid/ }).count()
  expect(hasAction).toBeGreaterThan(0)
  console.log('J vendor console errors:', JSON.stringify(w.pageErrors))
  expect(w.pageErrors).toEqual([])
})

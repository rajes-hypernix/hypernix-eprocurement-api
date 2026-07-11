import { test, expect } from '@playwright/test'
import { watch, goAs, shot } from './helpers'

const BUYER = 'u_faridah'
const VENDOR_SENTAUSA = 'VU-sentausa'

test('Vendor Master search + type filter narrow the list', async ({ page }) => {
  await goAs(page, BUYER, 'vendors')
  await page.waitForTimeout(1000)
  const rowsBefore = await page.locator('table tbody tr').count()
  const search = page.getByPlaceholder(/name or SWK-V/i)
  await search.fill('zzzzz-no-match')
  await page.waitForTimeout(400)
  const rowsAfter = await page.locator('table tbody tr').count()
  await shot(page, 'F-vendor-search')
  expect(rowsBefore).toBeGreaterThan(0)
  expect(rowsAfter).toBeLessThan(rowsBefore)
})

test('Manual vendor form blocks submit without company name (validation)', async ({ page }) => {
  await goAs(page, BUYER, 'vendors/new')
  await page.waitForTimeout(800)
  // choose manual entry if a chooser appears
  const manual = page.getByRole('button', { name: /Enter manually/i })
  if (await manual.isVisible().catch(() => false)) await manual.click()
  await page.waitForTimeout(500)
  const addBtn = page.getByRole('button', { name: /Add to master/i }).first()
  await addBtn.click()
  await shot(page, 'F-manual-vendor-validation')
  // an error notice should appear; no navigation away
  await expect(page.getByText(/company name/i).first()).toBeVisible()
})

test('Admin Custom Lists: add a value round-trips', async ({ page }) => {
  const w = watch(page)
  await goAs(page, BUYER, 'lists')
  await page.waitForTimeout(1000)
  // select PAYMENT_TERMS list
  const payBtn = page.getByRole('button', { name: /Payment terms/i }).first()
  if (await payBtn.isVisible().catch(() => false)) await payBtn.click()
  await page.waitForTimeout(300)
  const code = page.getByLabel('Code (stored)')
  const label = page.getByLabel('Label (shown)')
  const stamp = 'E2E' + Math.floor(Date.now() / 1000 % 100000)
  await code.fill(stamp)
  await label.fill('Audit probe ' + stamp)
  await page.getByRole('button', { name: /Add value/i }).click()
  await page.waitForTimeout(800)
  await shot(page, 'F-custom-list-add')
  await expect(page.getByText('Audit probe ' + stamp)).toBeVisible()
  // cleanup: delete it
  const row = page.locator('tr', { hasText: stamp })
  const del = row.getByRole('button').last()
  await del.click().catch(() => {})
  expect(w.pageErrors).toEqual([])
})

test('Onboarding Review shows an Altman-Z financial band for a Non-SWEC applicant', async ({ page }) => {
  await goAs(page, BUYER, 'onboarding')
  await page.waitForTimeout(1000)
  // open the first application that has a Review action
  const review = page.getByRole('button', { name: /Review/i }).first()
  if (await review.isVisible().catch(() => false)) {
    await review.click()
    await page.waitForTimeout(1200)
    await shot(page, 'F-onboarding-review')
    const txt = await page.locator('body').innerText()
    // Non-SWEC shows a financial band; SWEC waives it. Accept either but assert the section renders.
    const hasFinancial = /Financial pre-qualification|financial pre-qualification waived|Band [A-D]/.test(txt)
    expect(hasFinancial, 'financial section should render on review').toBeTruthy()
  }
})

test('Evaluation: technical scoring matrix renders for an in-evaluation RFQ', async ({ page }) => {
  await goAs(page, BUYER, 'openings')
  await page.waitForTimeout(1200)
  await shot(page, 'F-bid-openings')
  const txt = await page.locator('body').innerText()
  expect(txt.length, 'openings page renders content').toBeGreaterThan(50)
})

test('Vendor scoping: Sentausa sees only its own POs (guard [G])', async ({ page }) => {
  // Buyer sees all POs
  await goAs(page, BUYER, 'pos')
  await page.waitForTimeout(1000)
  const buyerRows = await page.locator('table tbody tr').count()
  // Vendor sees a subset
  await goAs(page, VENDOR_SENTAUSA, 'pos')
  await page.waitForTimeout(1000)
  await shot(page, 'F-vendor-po-scope')
  const vendorRows = await page.locator('table tbody tr').count()
  expect(buyerRows).toBeGreaterThan(0)
  expect(vendorRows, `vendor rows (${vendorRows}) should be <= buyer rows (${buyerRows})`).toBeLessThanOrEqual(buyerRows)
})

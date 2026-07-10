import { test, expect } from '@playwright/test'
import { goAs, shot } from './helpers'
test('RFQ list: filter bar + dd/mm/yyyy closes', async ({ page }) => {
  await goAs(page, 'u_faridah', 'rfqs')
  await page.waitForTimeout(1200)
  await shot(page, 'RFQ-filterbar-dates')
  // filter bar facets present
  for (const f of ['Envelope', 'Closes', 'Status']) {
    await expect(page.getByRole('button', { name: new RegExp(`Filter by ${f}`) })).toBeVisible()
  }
  await expect(page.getByLabel('Search RFQs')).toBeVisible()
  // a closes cell renders dd/mm/yyyy (not yyyy-mm-dd) for at least one non-draft RFQ
  const body = await page.locator('table tbody').innerText()
  const dmy = /\b\d{2}\/\d{2}\/\d{4}\b/.test(body)
  const ymd = /\b\d{4}-\d{2}-\d{2}\b/.test(body)
  console.log('RFQ dates: hasDDMMYYYY=' + dmy + ' hasYYYYMMDD=' + ymd)
  expect(dmy, 'a dd/mm/yyyy close date should render').toBeTruthy()
  expect(ymd, 'no yyyy-mm-dd should remain').toBeFalsy()
  // exercise a facet: filter by Envelope
  await page.getByRole('button', { name: /Filter by Envelope/ }).click()
  await page.waitForTimeout(300)
  await shot(page, 'RFQ-filter-envelope-open')
})

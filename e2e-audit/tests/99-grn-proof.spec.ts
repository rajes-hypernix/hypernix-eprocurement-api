import { test } from '@playwright/test'
import { goAs, shot } from './helpers'
test('GRN receipt date proof', async ({ page }) => {
  await goAs(page, 'u_faridah', 'deliveries')
  await page.waitForTimeout(1200)
  // open the ASN that now has a GRN
  const row = page.getByText('ASN-2026-0511').first()
  if (await row.isVisible().catch(() => false)) { await row.click(); await page.waitForTimeout(1000) }
  await shot(page, 'PROOF-grn-received-date')
})

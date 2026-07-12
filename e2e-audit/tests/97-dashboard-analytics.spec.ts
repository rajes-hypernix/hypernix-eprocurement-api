import { test, expect } from '@playwright/test'
import { watch, goAs, shot } from './helpers'

// D4 rewrote this spec: it used to pin the MOCK analytics panel ("up 12.8%", forecast
// toggle) — exactly what the D4 headline gate deletes. It now pins the honest,
// server-driven dashboard: live portlets, computed numbers, honest empty states.

test('Buyer dashboard: portlets render live, honest states, no console errors, links navigate', async ({ page }) => {
  const w = watch(page)
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1500)
  await shot(page, 'DA-buyer-dashboard')

  // The migrated stat cards render as the Sourcing pipeline scorecard with live values.
  await expect(page.getByText('Sourcing pipeline')).toBeVisible()
  await expect(page.getByText('Open requisitions')).toBeVisible()

  // The spend chart renders its HONEST minimum-data state (seed has one month of invoices)
  // — never an illustrative trend, never a fabricated forecast.
  await expect(page.getByText(/Not enough history yet/).first()).toBeVisible()
  await expect(page.getByText('up 12.8%')).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Show forecast/i })).toHaveCount(0)

  // The vs-LY meter is honestly absent-valued (no last-year invoice history in seed).
  await expect(page.getByText('vs same month last year')).toBeVisible()

  // Recent purchase orders is a live saved-view list (real PO codes from the seed).
  await expect(page.getByText('Recent purchase orders')).toBeVisible()

  // Scorecard click-through: Open requisitions → Requisitions screen.
  await page.getByText('Open requisitions').click()
  await page.waitForTimeout(800)
  await expect(page.getByRole('heading', { level: 1, name: /Requisitions/ })).toBeVisible()

  console.log('DA console errors:', JSON.stringify(w.pageErrors), 'net:', JSON.stringify([...new Set(w.netFail)]))
  expect(w.pageErrors).toEqual([])
})

test('Vendor dashboard: work queue + RFQ invitations portlet, own-scope only', async ({ page }) => {
  await goAs(page, 'VU-sentausa', 'dashboard')
  await page.waitForTimeout(1500)
  await shot(page, 'DA-vendor-dashboard-unchanged')
  await expect(page.getByText('RFQ invitations')).toBeVisible()
  await expect(page.getByText('Your work queue')).toBeVisible()
  await expect(page.getByText('Sourcing pipeline')).toHaveCount(0)
})

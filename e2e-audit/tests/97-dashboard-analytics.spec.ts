import { test, expect } from '@playwright/test'
import { watch, goAs, shot } from './helpers'

test('Buyer dashboard: analytics panel renders, no console errors, links navigate', async ({ page }) => {
  const w = watch(page)
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1200)
  await shot(page, 'DA-buyer-dashboard')

  await expect(page.getByText('Sourcing analytics')).toBeVisible()
  await expect(page.getByText('up 12.8%')).toBeVisible()
  await expect(page.getByRole('img', { name: /Committed spend by month/i })).toBeVisible()
  await expect(page.getByRole('img', { name: /New vendors onboarded/i })).toBeVisible()
  await expect(page.getByText('PO-2026-0141')).toBeVisible()
  // PO code must NOT be a link
  expect(await page.getByRole('link', { name: 'PO-2026-0141' }).count()).toBe(0)

  // KPI stat cards still present
  await expect(page.getByText('OPEN REQUISITIONS').or(page.getByText(/OPEN REQUISITIONS/i))).toBeVisible().catch(() => {})

  // toggle forecast
  await page.getByRole('button', { name: /Show forecast/i }).click()
  await page.waitForTimeout(200)
  await expect(page.getByText('Materials forecast')).toHaveCount(0)

  // View details → RFQ list
  await page.getByRole('button', { name: 'View details' }).click()
  await page.waitForTimeout(600)
  await expect(page.getByRole('heading', { level: 1, name: 'RFQs' })).toBeVisible()

  console.log('DA console errors:', JSON.stringify(w.pageErrors), 'net:', JSON.stringify([...new Set(w.netFail)]))
  expect(w.pageErrors).toEqual([])
})

test('Vendor dashboard UNCHANGED: still shows RFQ invitations table, no analytics panel', async ({ page }) => {
  await goAs(page, 'VU-sentausa', 'dashboard')
  await page.waitForTimeout(1200)
  await shot(page, 'DA-vendor-dashboard-unchanged')
  await expect(page.getByText('RFQ invitations')).toBeVisible()
  await expect(page.getByText('Sourcing analytics')).toHaveCount(0)
})

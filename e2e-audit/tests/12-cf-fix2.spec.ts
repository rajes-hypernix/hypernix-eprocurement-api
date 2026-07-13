import { test, expect } from '@playwright/test'
import { goAs } from './helpers'

// CF-FIX-2 browser proofs — one test per finding (round 2 of the operator's testing).

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

test('CF-FIX2-T4: square corners — modal, input, button, card and the searchable select all render radius 0; dots stay round', async ({ page }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  const radius = (loc: ReturnType<typeof page.locator>) =>
    loc.first().evaluate((el) => getComputedStyle(el).borderRadius)

  expect(await radius(page.getByRole('button', { name: 'New field' }))).toBe('0px')   // button
  expect(await radius(page.locator('table').first())).toBe('0px')                     // table card surface
  expect(await radius(page.locator('.badge').first())).toBe('0px')                    // pills square too (ruled)
  // The status DOT (::before) keeps its 50% — the one sanctioned rounding.
  const dot = await page.locator('.badge').first().evaluate((el) => getComputedStyle(el, '::before').borderRadius)
  expect(dot).toBe('50%')

  await page.getByRole('button', { name: 'New field' }).click()
  expect(await radius(page.getByRole('dialog'))).toBe('0px')                    // modal
  expect(await radius(page.getByLabel('Label', { exact: true }))).toBe('0px')   // input
  expect(await radius(page.locator('.sselect-control'))).toBe('0px')            // searchable select
  await page.getByRole('button', { name: 'Cancel' }).click()
})

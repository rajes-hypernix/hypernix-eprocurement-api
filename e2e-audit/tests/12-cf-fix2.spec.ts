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

test('CF-FIX2-T1: contextual prefixes — custbody_/custcol_/CUSTLIST_ with the affix INSIDE the field', async ({ page, request }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)

  // Header field: the affix reads custbody_ inside the field; user keys `customer`.
  await page.getByRole('button', { name: 'New field' }).click()
  await expect(page.locator('.affix')).toHaveText('custbody_')
  await page.getByLabel('Label', { exact: true }).fill(`Fix2 Cust ${STAMP}`)
  await page.getByLabel('Internal ID', { exact: true }).fill(`customer_${STAMP}`)
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText(`custbody_customer_${STAMP}`)).toBeVisible()

  // Line field: the affix flips to custcol_ WITH the Scope choice.
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Scope (header field or line column)', { exact: true }).selectOption('Line')   // still a native select until T2
  await expect(page.locator('.affix')).toHaveText('custcol_')
  await page.getByRole('button', { name: 'Cancel' }).click()

  // List: CUSTLIST_ affix; stored code carries it.
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New list' }).click()
  await expect(page.locator('.affix')).toHaveText('CUSTLIST_')
  await page.getByLabel('Internal ID', { exact: true }).fill(`FIX2${STAMP}`)
  await page.getByLabel('Name', { exact: true }).fill(`Fix2 List ${STAMP}`)
  await page.getByRole('button', { name: 'Create list' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText(`CUSTLIST_FIX2${STAMP}`).first()).toBeVisible()

  // cleanup
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const def = defs.find((d: { code: string }) => d.code === `custbody_customer_${STAMP}`)
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
  expect((await request.delete(`${API}/api/custom-lists/CUSTLIST_FIX2${STAMP}`, { headers: ADMIN })).status()).toBe(204)
})

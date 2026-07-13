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

test('CF-FIX2-T2: the searchable select is the standard everywhere — a record ListValue field AND the previously-plain Vendor Master region filter', async ({ page, request }) => {
  // A ListValue custom field on a PO picks via type-to-filter (record surface).
  const lists = await (await request.get(`${API}/api/custom-lists`, { headers: ADMIN })).json()
  const currency = lists.find((l: { code: string }) => l.code === 'CURRENCY')
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Fix2 Terms ${STAMP}`, recordType: 'PurchaseOrder', dataType: 'ListValue', customListId: currency.id, required: false, helpText: '', sort: 0 } })).json()
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  await goAs(page, 'u_faridah', `pos/${pos[0].id}`)
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: `Fix2 Terms ${STAMP}`, exact: true }).click()
  await page.getByRole('combobox', { name: `Search Fix2 Terms ${STAMP}` }).fill('ring')
  await expect(page.getByRole('option', { name: /Ringgit/ })).toBeVisible()   // customList options load async
  await page.keyboard.press('Enter')
  await expect(page.getByRole('button', { name: `Fix2 Terms ${STAMP}`, exact: true })).toContainText('Ringgit')

  // Vendor Master's Region filter (was a plain native select) is now the searchable component.
  await goAs(page, 'u_faridah', 'vendors')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'Region', exact: true }).click()
  await page.getByRole('combobox', { name: 'Search Region' }).fill('Sar')
  await page.keyboard.press('Enter')
  await expect(page.getByRole('button', { name: 'Region', exact: true })).toContainText('Sarawak')

  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX2-T5: values stage until ONE Save — reload-before-save shows none; Cancel discards; staged parenting resolves', async ({ page, request }) => {
  const ID = `T5${STAMP}`
  await request.post(`${API}/api/custom-lists`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: ID, name: `Fix2 Staged ${STAMP}`, description: null, parentListCode: null } })
  const CODE = `CUSTLIST_${ID}`
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Fix2 Staged ${STAMP}`) }).click()

  // Stage three values — one parenting a STAGED sibling — nothing persists yet.
  const stage = async (label: string) => {
    await page.getByLabel('Label', { exact: true }).fill(label)
    await page.getByRole('button', { name: 'Add value' }).click()
  }
  await stage('Root A')
  await page.getByLabel('Label', { exact: true }).fill('Child of A')
  await page.getByRole('button', { name: 'Parent (optional)' }).click()
  await page.getByRole('combobox', { name: 'Search Parent (optional)' }).fill('Root A')
  await page.keyboard.press('Enter')
  await page.getByRole('button', { name: 'Add value' }).click()
  await stage('Root B')
  await expect(page.getByText('3 unsaved values')).toBeVisible()
  expect((await (await request.get(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).json()).values).toHaveLength(0)   // NOT persisted

  // One Save commits all three; staged parent resolved to the real sibling id.
  await page.getByRole('button', { name: 'Save values' }).click()
  await page.waitForTimeout(1200)
  const vals = (await (await request.get(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).json()).values
  expect(vals).toHaveLength(3)
  const rootA = vals.find((v: { label: string }) => v.label === 'Root A')
  expect(vals.find((v: { label: string }) => v.label === 'Child of A').parentValueCode).toBe(rootA.code)

  // Cancel discards staged rows.
  await stage('Never Saved')
  await expect(page.getByText('1 unsaved value')).toBeVisible()
  await page.getByRole('button', { name: 'Discard unsaved values' }).click()
  await expect(page.getByText('unsaved value')).toHaveCount(0)
  expect((await (await request.get(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).json()).values).toHaveLength(3)

  // cleanup (children first)
  for (const label of ['Child of A', 'Root A', 'Root B']) {
    const v = (await (await request.get(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).json()).values.find((x: { label: string }) => x.label === label)
    await request.delete(`${API}/api/custom-lists/values/${v.id}`, { headers: ADMIN })
  }
  expect((await request.delete(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).status()).toBe(204)
})

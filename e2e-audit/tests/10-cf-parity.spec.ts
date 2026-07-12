import { test, expect } from '@playwright/test'
import { goAs } from './helpers'

// CF programme browser proofs — ONE growing spec; each test name matches a ledger Test column
// entry. A ledger box only ticks when its test here drives the capability on screen and passes.

const API = 'http://localhost:5260'
const BUYER = { 'X-Demo-User': 'u_faridah' }
const ADMIN = { 'X-Demo-User': 'u_admin' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

test('CF1-T1: money renders grouped 100,000.00 on read surfaces and round-trips raw on edit', async ({ page, request }) => {
  // Read surface: a PO with a 5-digit total renders grouped.
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  const big = pos.find((p: { total: number }) => p.total >= 10000)
  expect(big, 'a seeded PO with a groupable total').toBeTruthy()
  await goAs(page, 'u_faridah', 'pos')
  await page.waitForTimeout(1200)
  const grouped = Number(big.total).toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  await expect(page.locator('td', { hasText: `RM ${grouped}` }).first()).toBeVisible()

  // Edit surface: a Money custom field groups on blur and SUBMITS the raw numeric.
  const cf = await (await request.post(`${API}/api/custom-fields`, {
    headers: { ...ADMIN, ...JSON_H },
    data: { label: `Budget Cap ${STAMP}`, recordType: 'PurchaseOrder', dataType: 'Money', customListId: null, required: false, helpText: '', sort: 0 },
  })).json()
  await goAs(page, 'u_faridah', `pos/${big.id}`)
  await page.waitForTimeout(1500)
  const field = page.getByLabel(`Budget Cap ${STAMP}`)
  await field.fill('123456.5')
  await field.blur()
  await expect(field).toHaveValue('123,456.50')          // grouped display on blur
  await field.focus()
  await expect(field).toHaveValue('123456.5')            // raw numeric back on focus (round-trip)
  await field.blur()
  await page.getByRole('button', { name: 'Save custom fields' }).click()
  await page.waitForTimeout(1000)
  const values = await (await request.get(`${API}/api/custom-values/PurchaseOrder/${big.id}`, { headers: BUYER })).json()
  expect(Number(values.find((v: { code: string }) => v.code === cf.code)?.value)).toBe(123456.5)   // RAW numeric stored (server normalizes money to 2dp)

  // cleanup: clear the value then hard-delete the def
  await request.put(`${API}/api/custom-values/PurchaseOrder/${big.id}`, {
    headers: { ...BUYER, ...JSON_H }, data: { values: { [cf.code]: null } },
  })
  expect((await request.delete(`${API}/api/custom-fields/${cf.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF1-T2: edit a custom list — rename, alphabetical order-mode, guarded delete', async ({ page, request }) => {
  // A run-stamped list, values deliberately entered Z-then-A, bound to a PO custom field.
  const listCode = `CFLIST${STAMP}`
  await request.post(`${API}/api/custom-lists`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: listCode, name: `CF List ${STAMP}`, description: null, parentListCode: null } })
  for (const [c, l] of [['Z1', 'Zebra'], ['A1', 'Aardvark']] as const)
    await request.post(`${API}/api/custom-lists/${listCode}/values`, { headers: { ...ADMIN, ...JSON_H },
      data: { code: c, label: l, parentValueCode: null } })
  const cf = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Pick ${STAMP}`, recordType: 'PurchaseOrder', dataType: 'ListValue',
      customListId: (await (await request.get(`${API}/api/custom-lists/${listCode}`, { headers: ADMIN })).json()).id,
      required: false, helpText: '', sort: 0 } })).json()

  // The list-self EDIT the operator asked for: rename + flip to alphabetical, in the UI.
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `CF List ${STAMP}` }).click()
  await page.getByRole('button', { name: 'Edit list' }).click()
  await page.getByLabel('Name').fill(`CF List ${STAMP} v2`)
  await page.getByLabel('Show options in').selectOption('Alphabetical')
  await page.getByRole('button', { name: 'Save list' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText('A→Z')).toBeVisible()                    // order-mode badge on the glass

  // The order mode drives a REAL picker: the bound field's options are now A-then-Z.
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  await goAs(page, 'u_faridah', `pos/${pos[0].id}`)
  await page.waitForTimeout(1500)
  const opts = await page.getByLabel(`Pick ${STAMP}`).locator('option').allTextContents()
  const labels = opts.filter((o) => o === 'Zebra' || o === 'Aardvark')
  expect(labels).toEqual(['Aardvark', 'Zebra'])                        // alphabetical, not entered order

  // Guarded delete: the bound list DEACTIVATES (badge), never vanishes under the field.
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `CF List ${STAMP} v2` }).click()
  await page.getByRole('button', { name: 'Delete list' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText('Inactive')).toBeVisible()

  // cleanup: unbind (delete the def) → now clean → hard delete.
  expect((await request.delete(`${API}/api/custom-fields/${cf.id}`, { headers: ADMIN })).status()).toBe(204)
  expect((await request.delete(`${API}/api/custom-lists/${listCode}`, { headers: ADMIN })).status()).toBe(204)
})

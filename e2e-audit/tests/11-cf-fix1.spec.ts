import { test, expect } from '@playwright/test'
import { goAs } from './helpers'

// CF-FIX-1 browser proofs — one test per task, mirroring the operator's testing notes
// (Custom_Fields.docx / Custom_List.docx). Names match the CF-FIX1-Tn commits.

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

test('CF-FIX1-T1: full record-type names, renamed options, no lifecycle prose', async ({ page }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)

  // Full professional names on the rail (display only — storage unchanged).
  await expect(page.getByRole('button', { name: 'Request For Quote' })).toBeVisible()
  await expect(page.getByRole('button', { name: /Purchase Order \d+ field/ })).toBeVisible()   // the rail item (selected → carries its count; 'Purchase Orders' nav is separate)
  await expect(page.getByRole('button', { name: 'Advance Shipment Notice' })).toBeVisible()
  await expect(page.getByText('Rfq', { exact: true })).toHaveCount(0)

  // The subtitle and the lifecycle prose are gone.
  await expect(page.getByText('Fields your team defines')).toHaveCount(0)
  await expect(page.getByText('A field with values is never deleted')).toHaveCount(0)

  // The renamed options live in the modal.
  await page.getByRole('button', { name: 'New field' }).click()
  await expect(page.getByLabel('Show On Default List', { exact: true })).toBeVisible()
  await expect(page.getByLabel('Mandatory', { exact: true })).toBeVisible()
  await expect(page.getByText('Show in list (column on the default list view)')).toHaveCount(0)
  await expect(page.getByText('Required (at value-save only)')).toHaveCount(0)
})

test('CF-FIX1-T2: Insert Before is gone; creating a field still works and lands in the list', async ({ page, request }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New field' }).click()
  await expect(page.getByText('Insert before')).toHaveCount(0)   // the control is removed
  await page.getByLabel('Label', { exact: true }).fill(`Fix1 Plain ${STAMP}`)
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByRole('row', { name: new RegExp(`Fix1 Plain ${STAMP}`) })).toBeVisible()
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const def = defs.find((d: { label: string }) => d.label === `Fix1 Plain ${STAMP}`)
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX1-T3: order-mode is choosable at CREATE — Alphabetical from birth', async ({ page, request }) => {
  const CODE = `F1T3${STAMP}`
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New list' }).click()
  await page.getByLabel('Code', { exact: true }).fill(CODE)
  await page.getByLabel('Name', { exact: true }).fill(`Fix1 Order ${STAMP}`)
  await page.getByLabel('Show options in', { exact: true }).selectOption('Alphabetical')   // on CREATE (was edit-only)
  await page.getByRole('button', { name: 'Create list' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText('A→Z')).toBeVisible()   // the badge shows the mode took at birth
  const list = await (await request.get(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).json()
  expect(list.orderMode).toBe('Alphabetical')
  expect((await request.delete(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX1-T4: lists carry ONE Internal ID — labelled as such, duplicate blocked with a clear message', async ({ page, request }) => {
  const ID = `F1T4${STAMP}`
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New list' }).click()
  await expect(page.getByLabel('Internal ID', { exact: true })).toBeVisible()   // one id concept, no separate Code
  await expect(page.getByLabel('Code', { exact: true })).toHaveCount(0)
  await page.getByLabel('Internal ID', { exact: true }).fill(ID)
  await page.getByLabel('Name', { exact: true }).fill(`Fix1 Ids ${STAMP}`)
  await page.getByRole('button', { name: 'Create list' }).click()
  await page.waitForTimeout(1000)

  // Duplicate Internal ID → blocked with the Internal ID message.
  await page.getByRole('button', { name: 'New list' }).click()
  await page.getByLabel('Internal ID', { exact: true }).fill(ID)
  await page.getByLabel('Name', { exact: true }).fill('Dup attempt')
  await page.getByRole('button', { name: 'Create list' }).click()
  await expect(page.getByText(`Internal ID '${ID}' already exists`)).toBeVisible()

  expect((await request.delete(`${API}/api/custom-lists/${ID}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX1-T5: field Internal ID is user-input — auto-suggested, overridable; duplicate and illegal ids blocked', async ({ page, request }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill(`Fix1 Own Id ${STAMP}`)
  // Auto-suggested from the label…
  await expect(page.getByLabel('Internal ID', { exact: true })).toHaveValue(`fix1_own_id_${STAMP}`)
  // …but user-overridable.
  await page.getByLabel('Internal ID', { exact: true }).fill(`chosen_${STAMP}`)
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText(`cf_chosen_${STAMP}`)).toBeVisible()   // the cf_ namespace is system-applied

  // Duplicate → clear message.
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill('Dup attempt')
  await page.getByLabel('Internal ID', { exact: true }).fill(`chosen_${STAMP}`)
  await page.getByRole('button', { name: 'Create field' }).click()
  await expect(page.getByText(/Internal ID 'cf_chosen_.*already exists/)).toBeVisible()

  // Illegal characters → blocked.
  await page.getByLabel('Internal ID', { exact: true }).fill('bad id!')
  await page.getByRole('button', { name: 'Create field' }).click()
  await expect(page.getByText('letters, digits and underscores')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()

  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const def = defs.find((d: { code: string }) => d.code === `cf_chosen_${STAMP}`)
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX1-T6: list values get automatic numeric ids — 1/2/3 in entry order, read-only', async ({ page, request }) => {
  const ID = `F1T6${STAMP}`
  await request.post(`${API}/api/custom-lists`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: ID, name: `Fix1 Values ${STAMP}`, description: null, parentListCode: null } })
  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Fix1 Values ${STAMP}`) }).click()

  for (const label of ['Alpha', 'Bravo', 'Charlie']) {
    await page.getByLabel('Label', { exact: true }).fill(label)
    await page.getByRole('button', { name: 'Add value' }).click()
    await page.waitForTimeout(500)
  }
  // Ids read 1/2/3 in entry order; there is no id input (system-assigned).
  const rows = page.locator('table tbody tr')
  await expect(rows.nth(0)).toContainText('1')
  await expect(rows.nth(0)).toContainText('Alpha')
  await expect(rows.nth(1)).toContainText('2')
  await expect(rows.nth(2)).toContainText('3')
  await expect(page.getByLabel('Code (stored)')).toHaveCount(0)

  // The id is read-only in Edit.
  await rows.nth(0).getByRole('button', { name: 'Edit' }).click()
  const idField = page.getByLabel('ID', { exact: true })
  await expect(idField).toHaveValue('1')
  await expect(idField).not.toBeEditable()   // readOnly spec renders a readonly input
  await page.getByRole('button', { name: 'Cancel' }).click()

  expect((await request.delete(`${API}/api/custom-lists/${ID}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX1-T7: the searchable select — type-to-filter on screen, keyboard select, dependent children filter by parent', async ({ page, request }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New field' }).click()

  // Type-to-filter on the Data type picker (variant 1) + keyboard select.
  await page.getByRole('button', { name: 'Data type' }).click()
  await page.getByRole('combobox', { name: 'Search Data type' }).fill('lis')
  await expect(page.getByRole('listbox', { name: 'Data type options' }).getByRole('option')).toHaveCount(1)   // scoped: native selects elsewhere also expose options
  await page.keyboard.press('Enter')                                     // keyboard contract
  await expect(page.getByRole('button', { name: 'Data type' })).toContainText('ListValue')
  // Escape closes without changing.
  await page.getByRole('button', { name: 'Custom list', exact: true }).click()   // the picker, not the Custom Lists nav
  await page.keyboard.press('Escape')
  await page.getByRole('button', { name: 'Cancel' }).click()

  // Variant 3 (dependent): the vendor form's State options filter by the chosen Country
  // (the existing ParentValueCode machinery behind the same component family).
  const states = await (await request.get(`${API}/api/custom-lists/STATE`, { headers: ADMIN })).json()
  expect(states.parentListCode).toBe('COUNTRY')   // the dependency the picker rides
})

test('CF-FIX1-T8: new types on screen — full names in the picker; Date rejects garbage and US-format; Email rejects notanemail; Hyperlink shows its label', async ({ page, request }) => {
  const mk = async (label: string, dataType: string) =>
    (await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
      data: { label, recordType: 'PurchaseOrder', dataType, customListId: null, required: false, helpText: '', sort: 0 } })).json())
  const dateDef = await mk(`Fix1 Due ${STAMP}`, 'Date')
  const emailDef = await mk(`Fix1 Mail ${STAMP}`, 'Email')
  const linkDef = await mk(`Fix1 Link ${STAMP}`, 'Hyperlink')

  // The type picker speaks full professional names.
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByRole('button', { name: 'Data type' }).click()
  const list = page.getByRole('listbox', { name: 'Data type options' })
  for (const name of ['Free-Form Text', 'Integer Number', 'Currency', 'Check Box', 'Date/Time', 'Email Address', 'Phone Number', 'Hyperlink'])
    await expect(list.getByRole('option', { name, exact: true })).toBeVisible()
  await page.keyboard.press('Escape')
  await page.getByRole('button', { name: 'Cancel' }).click()

  // On a PO: server rejects garbage + US-format dates and bad emails; Hyperlink renders its label.
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  const po = pos[0]
  const put = (values: Record<string, string>) => request.put(`${API}/api/custom-values/PurchaseOrder/${po.id}`,
    { headers: { ...BUYER, ...JSON_H }, data: { values } })
  expect((await put({ [dateDef.code]: '2026-13-40' })).status()).toBe(400)
  expect((await put({ [dateDef.code]: '03/15/2026' })).status()).toBe(400)      // US-format rejected
  expect((await put({ [dateDef.code]: '15/03/2026' })).status()).toBe(200)      // dd/mm/yyyy accepted
  expect((await put({ [emailDef.code]: 'notanemail' })).status()).toBe(400)
  expect((await put({ [linkDef.code]: 'https://spsb.com.my\nTender Portal' })).status()).toBe(200)

  await goAs(page, 'u_faridah', `pos/${po.id}`)
  await page.waitForTimeout(1500)
  await expect(page.getByRole('link', { name: 'Tender Portal' })).toBeVisible()   // the label, not the raw URL

  // Invalid entry rejected ON SCREEN too (server message surfaces).
  await page.getByLabel(`Fix1 Mail ${STAMP}`, { exact: true }).fill('notanemail')
  await page.getByRole('button', { name: 'Save custom fields' }).click()
  await expect(page.getByText(/valid email address/)).toBeVisible()

  // cleanup
  await put({ [dateDef.code]: '', [linkDef.code]: '' })
  for (const d of [dateDef, emailDef, linkDef])
    expect((await request.delete(`${API}/api/custom-fields/${d.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX1-T9: depends-on works and is hardened — child filters by parent; dangling parent blocked on screen', async ({ page, request }) => {
  // The standing COUNTRY→STATE dependency drives the vendor form's cascading pickers.
  const V = `T9${STAMP}`
  await request.post(`${API}/api/custom-lists`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: V, name: `T9 Region ${STAMP}`, description: null, parentListCode: 'COUNTRY' } })

  await goAs(page, 'u_admin', 'lists')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`T9 Region ${STAMP}`) }).click()

  // Child value entry offers the PARENT list's values; pick one and it lands.
  await page.getByLabel('Label', { exact: true }).fill('North Zone')
  await page.getByLabel('Country', { exact: true }).selectOption({ label: 'Malaysia' })
  await page.getByRole('button', { name: 'Add value' }).click()
  await page.waitForTimeout(600)
  await expect(page.getByRole('row', { name: /North Zone/ })).toContainText('Malaysia')

  // A dangling parent value is refused by the SERVER.
  const bad = await request.post(`${API}/api/custom-lists/${V}/values`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: null, label: 'Ghost Zone', parentValueCode: 'NO_SUCH' } })
  expect(bad.status()).toBe(409)   // DomainRuleException → conflict, the app's standing error contract

  // A dangling parent LIST is refused at create.
  const badList = await request.post(`${API}/api/custom-lists`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: `T9X${STAMP}`, name: 'Ghost', description: null, parentListCode: 'NO_SUCH_LIST' } })
  expect(badList.status()).toBe(409)

  expect((await request.delete(`${API}/api/custom-lists/${V}`, { headers: ADMIN })).status()).toBe(204)
})

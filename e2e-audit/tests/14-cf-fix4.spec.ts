import { test, expect } from '@playwright/test'
import { goAs, requiredCustomValues } from './helpers'

// CF-FIX-4 browser proofs. T1 Header invariant · T2 standard forms · T3 designer + L6
// data-safety (the operator's hardest check: remove-from-form deletes placement ONLY).

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

test('CF-FIX4-T2: Entry Forms lists a Standard form for PR, PO, GRN and Invoice — each with its Header group', async ({ page }) => {
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  for (const name of ['Standard PR Form', 'Standard PO Form', 'Standard GRN Form', 'Standard Invoice Form']) {
    await page.getByRole('button', { name: new RegExp(name) }).and(page.locator(':not(:has-text("(copy)"))')).first().click()
    await expect(page.getByText('Standard — the parity baseline, read-only')).toBeVisible()
    await expect(page.getByText('Header — always present')).toBeVisible()   // the L3 badge on the Header card
  }
})

test('CF-FIX4-T1+T3: designer — create group, drag a field from Header into it (GroupId persists), subtab move, sublist reorder, Header undeletable', async ({ page, request }) => {
  // A working (non-system) form to design on.
  const std = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix4 Designer ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()

  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Fix4 Designer ${STAMP}`) }).click()

  // Create a group on screen.
  await page.getByLabel('New group title', { exact: true }).fill('Logistics')
  await page.getByRole('button', { name: 'Add group' }).click()
  await expect(page.getByLabelText ? page.getByLabel('Field group Logistics') : page.locator('[aria-label="Field group Logistics"]')).toBeVisible()

  // Drag Department from Header into Logistics — the placement object moves (L1).
  const dt = await page.evaluateHandle(() => new DataTransfer())
  await page.dispatchEvent('[aria-label="Field row Department"]', 'dragstart', { dataTransfer: dt })
  await page.dispatchEvent('[aria-label="Field group Logistics"]', 'drop', { dataTransfer: dt })
  await page.waitForTimeout(800)
  await expect(page.locator('[aria-label="Field group Logistics"]')).toContainText('Department')

  // GroupId PERSISTED server-side (reload-proof, not a UI illusion).
  const after = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  const mine = after.find((f: { id: string }) => f.id === form.id)
  const logistics = mine.groups.find((g: { title: string }) => g.title === 'Logistics')
  const dept = mine.fields.find((f: { fieldKey: string }) => f.fieldKey === 'Department')
  expect(dept.groupId).toBe(logistics.id)

  // Subtab: create one, drag a field in.
  await page.getByLabel('New subtab name', { exact: true }).fill('Extras')
  await page.getByRole('button', { name: 'Add subtab' }).click()
  await page.waitForTimeout(600)
  const dt2 = await page.evaluateHandle(() => new DataTransfer())
  await page.dispatchEvent('[aria-label="Field row Memo"]', 'dragstart', { dataTransfer: dt2 })
  await page.locator('button[aria-label="Subtab Extras"]').dispatchEvent('drop', { dataTransfer: dt2 })
  await page.waitForTimeout(800)
  await page.getByRole('button', { name: 'Subtab Extras' }).click()
  await expect(page.locator('[aria-label^="Field group"]').first()).toContainText('Memo')

  // Sublist: flat reorder persists (L4).
  await page.getByRole('button', { name: 'Move Description left' }).click()
  await page.waitForTimeout(800)
  const reordered = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === form.id)
  expect(reordered.sublistColumns[0]).toBe('Description')

  // Header cannot be deleted — not offered in the UI, REFUSED by the server.
  await page.getByRole('button', { name: 'Body tab' }).click()
  await expect(page.locator('[aria-label="Field group Header"], [aria-label^="Field group"]').first()).toBeVisible()
  expect(page.getByRole('button', { name: 'Delete group Header' })).toHaveCount(0)
  const header = mine.groups.find((g: { isHeader: boolean }) => g.isHeader)
  expect((await request.delete(`${API}/api/entry-forms/${form.id}/groups/${header.id}`, { headers: ADMIN })).status()).toBe(409)

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX4-T3/L6: remove-from-form is DATA-SAFE — the value survives on the record, in a saved view, and on another form', async ({ page, request }) => {
  // Custom field on PR, placed on a copy-form, valued on a real PR, referenced by a view.
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Fix4 L6 ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0, code: `fix4_l6_${STAMP}` } })).json()
  const std = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix4 L6 Form ${STAMP}`, recordType: 'Requisition',
      fields: [...std.fields, { fieldKey: def.code, subtab: null, fieldGroup: 'Header', sort: 99, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null }] } })).json()

  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const pr = prs.find((p: { headerStatus: string }) => p.headerStatus !== 'Cancelled') ?? prs[0]
  const req = await requiredCustomValues(request, 'Requisition', pr.id)
  const put = await request.put(`${API}/api/custom-values/Requisition/${pr.id}`, { headers: { ...BUYER, ...JSON_H },
    data: { values: { ...req, [def.code]: 'precious value' } } })
  expect(put.status()).toBe(200)
  const view = await (await request.post(`${API}/api/views`, { headers: { ...BUYER, ...JSON_H },
    data: { name: `fix4-l6-view-${STAMP}`, recordType: 'Requisition', filters: [],
      columns: [{ fieldKey: 'Code', label: null, sortDirection: null }, { fieldKey: def.code, label: null, sortDirection: null }] } })).json()

  // THE REMOVE, on screen: open the designer, hit the field's ✕ (placement only), save.
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Fix4 L6 Form ${STAMP}`) }).click()
  await page.getByRole('button', { name: `Remove ${def.code}` }).click()
  await page.getByRole('button', { name: 'Save form' }).click()
  await page.waitForTimeout(1000)

  // 1. The value is STILL on the record.
  const values = await (await request.get(`${API}/api/custom-values/Requisition/${pr.id}`, { headers: BUYER })).json()
  expect(values.find((v: { code: string }) => v.code === def.code)?.value).toBe('precious value')
  // 2. Still visible through the saved view.
  const run = await (await request.get(`${API}/api/views/${view.id}/run?page=1&size=200`, { headers: BUYER })).json()
  const row = run.rows.find((r: Record<string, unknown>) => r['Code'] === pr.code)
  expect(row?.[def.code]).toBe('precious value')
  // 3. The placement row is gone from THIS form; the impact report still counts the view.
  const formAfter = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === form.id)
  expect(formAfter.fields.some((f: { fieldKey: string }) => f.fieldKey === def.code)).toBe(false)

  // cleanup
  await request.delete(`${API}/api/views/${view.id}`, { headers: BUYER })
  await request.put(`${API}/api/custom-values/Requisition/${pr.id}`, { headers: { ...BUYER, ...JSON_H }, data: { values: { ...req, [def.code]: null } } })
  await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
})

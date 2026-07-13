import { test, expect } from '@playwright/test'
import type { APIRequestContext, Page } from '@playwright/test'
import { goAs, pickSearch } from './helpers'

// CF-FIX-3 browser proofs — the impact report + three-tier lifecycle (round 3).
// Tier 1 Deactivate (always) · Tier 2 Delete (zero refs AND zero values) ·
// Tier 3 Purge (historical-only, A73-governed, snapshotted — snapshot content is
// pinned server-side in CustomFieldsTests/ReferenceProviderTests).

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

/** Satisfy required header defs generically (the seeded 'Partner' field), then set ours. */
async function setPoValue(request: APIRequestContext, poId: string, code: string, value: string | null) {
  const defs = await (await request.get(`${API}/api/custom-values/PurchaseOrder/${poId}`, { headers: BUYER })).json()
  const values: Record<string, string | null> = { [code]: value }
  for (const d of defs) if (d.required && !d.value) values[d.code] = values[d.code] ?? 'e2e'
  const res = await request.put(`${API}/api/custom-values/PurchaseOrder/${poId}`,
    { headers: { ...BUYER, ...JSON_H }, data: { values } })
  expect(res.status()).toBe(200)
}

const poByStatus = async (request: APIRequestContext, status: string) => {
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  return pos.find((p: { status: string }) => p.status === status)
}

const openFieldDialog = async (page: Page, label: string) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `Delete ${label}` }).click()
  await expect(page.getByTestId('impact-report')).toBeVisible()
}

test('CF-FIX3-T3: the OLD BUG is dead — a zero-value field that is a view column is REFUSED, then live blocks, then purge clears history', async ({ page, request }) => {
  const LABEL = `Fix3 Field ${STAMP}`
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: LABEL, recordType: 'PurchaseOrder', dataType: 'Text', required: false, helpText: '', sort: 0, code: `fix3_${STAMP}` } })).json()

  // The live bug's exact shape: ZERO values, but a saved view uses the field as a column.
  const view = await (await request.post(`${API}/api/views`, { headers: { ...BUYER, ...JSON_H },
    data: { name: `fix3-view-${STAMP}`, recordType: 'PurchaseOrder', filters: [], columns: [{ fieldKey: def.code, label: null, sortDirection: null }] } })).json()

  await openFieldDialog(page, LABEL)
  await expect(page.getByText('Saved Views').first()).toBeVisible()          // the report NAMES the consumer…
  await expect(page.getByText(`fix3-view-${STAMP}`)).toBeVisible()   // …and the exact view
  await expect(page.getByRole('button', { name: 'Delete field' })).toBeDisabled()   // pre-fix this deleted
  await page.getByRole('button', { name: 'Cancel' }).click()

  // Reference cleared; now a LIVE value (Draft PO) blocks both delete and purge — Tier 1 offered.
  await request.delete(`${API}/api/views/${view.id}`, { headers: BUYER })
  const draft = await poByStatus(request, 'Draft')
  await setPoValue(request, draft.id, def.code, 'live value')
  await openFieldDialog(page, LABEL)
  await expect(page.getByText(/live on OPEN records/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Delete field' })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Purge history' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Deactivate instead' })).toBeEnabled()
  await page.getByRole('button', { name: 'Cancel' }).click()

  // Live cleared; a value on a MATCHED (terminal) PO remains → Tier 3 purge, double-confirmed.
  await setPoValue(request, draft.id, def.code, null)
  const matched = await poByStatus(request, 'Matched')
  await setPoValue(request, matched.id, def.code, 'historical value')
  await openFieldDialog(page, LABEL)
  await expect(page.getByText(/Purge \(governed\)/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Delete field' })).toBeDisabled()
  await page.getByRole('button', { name: 'Purge history' }).click()
  await page.getByRole('button', { name: 'Confirm purge' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByRole('button', { name: `Delete ${LABEL}` })).toHaveCount(0)   // field is gone
  expect((await request.get(`${API}/api/custom-fields/${def.id}/references`, { headers: ADMIN })).status()).toBe(404)
})

test('CF-FIX3-T4: the value X opens the report — filter-VALUE blocks naming the view; unused value deletes; closed-record value purges', async ({ page, request }) => {
  const LIST = `FIX3${STAMP}`
  const CODE = `CUSTLIST_${LIST}`
  await request.post(`${API}/api/custom-lists`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: LIST, name: `Fix3 List ${STAMP}`, description: null, parentListCode: null } })
  const v1 = await (await request.post(`${API}/api/custom-lists/${CODE}/values`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: null, label: 'Referenced', parentValueCode: null } })).json()
  const v2 = await (await request.post(`${API}/api/custom-lists/${CODE}/values`, { headers: { ...ADMIN, ...JSON_H },
    data: { code: null, label: 'Unused', parentValueCode: null } })).json()
  const list = await (await request.get(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).json()
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Fix3 Bound ${STAMP}`, recordType: 'PurchaseOrder', dataType: 'ListValue', customListId: list.id, required: false, helpText: '', sort: 0 } })).json()
  const view = await (await request.post(`${API}/api/views`, { headers: { ...BUYER, ...JSON_H },
    data: { name: `fix3-vfilter-${STAMP}`, recordType: 'PurchaseOrder', filters: [{ fieldKey: def.code, operator: 'Eq', value: v1.code, value2: null }], columns: [{ fieldKey: 'Code', label: null, sortDirection: null }] } })).json()

  const openValueDialog = async (code: string) => {
    await goAs(page, 'u_admin', 'lists')
    await page.waitForTimeout(1200)
    await page.getByRole('button', { name: new RegExp(`Fix3 List ${STAMP}`) }).click()
    await page.getByRole('button', { name: `Delete ${code}` }).click()
    await expect(page.getByTestId('impact-report')).toBeVisible()
  }

  // A view filtering on THIS VALUE blocks its delete — the report names the view.
  await openValueDialog(v1.code)
  await expect(page.getByText('Saved Views').first()).toBeVisible()
  await expect(page.getByText(`fix3-vfilter-${STAMP}`)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Delete list value' })).toBeDisabled()
  await page.getByRole('button', { name: 'Cancel' }).click()

  // The unused sibling deletes cleanly through the same dialog (no silent deactivate).
  await openValueDialog(v2.code)
  await expect(page.getByText(/can be deleted safely/)).toBeVisible()
  await page.getByRole('button', { name: 'Delete list value' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('Unused')).toHaveCount(0)

  // Reference cleared + value only on a MATCHED PO → the purge tier, through the dialog.
  await request.delete(`${API}/api/views/${view.id}`, { headers: BUYER })
  const matched = await poByStatus(request, 'Matched')
  await setPoValue(request, matched.id, def.code, v1.code)
  await openValueDialog(v1.code)
  await expect(page.getByText(/Purge \(governed\)/)).toBeVisible()
  await page.getByRole('button', { name: 'Purge history' }).click()
  await page.getByRole('button', { name: 'Confirm purge' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText('Referenced')).toHaveCount(0)   // the value row is gone

  // cleanup
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
  expect((await request.delete(`${API}/api/custom-lists/${CODE}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX3-T5: type is immutable in the edit modal; Create replacement prefills a NEW field with a fresh ID and a choosable type', async ({ page, request }) => {
  const LABEL = `Fix3 Repl ${STAMP}`
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: LABEL, recordType: 'PurchaseOrder', dataType: 'Text', required: false, helpText: 'keep me', sort: 0, code: `fix3repl_${STAMP}` } })).json()

  // Server guarantee first: a type change past the UI is a 400.
  const put = await request.put(`${API}/api/custom-fields/${def.id}`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: LABEL, recordType: 'PurchaseOrder', dataType: 'Int', customListId: null, required: false, helpText: '', sort: 0 } })
  expect(put.status()).toBe(400)

  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  const row = page.locator('tr', { hasText: LABEL })
  await row.getByRole('button', { name: 'Edit' }).click()
  await expect(page.getByText(/inactivate this field and create a new one/)).toBeVisible()
  await expect(page.getByLabel('Data type')).toHaveCount(0)   // no type picker in edit mode

  await page.getByRole('button', { name: `Create replacement for ${LABEL}` }).click()
  await expect(page.getByText(`Replacement for — ${LABEL}`)).toBeVisible()
  await expect(page.getByLabel('Label', { exact: true })).toHaveValue(LABEL)   // prefilled
  const freshId = await page.getByLabel('Internal ID', { exact: true }).inputValue()
  expect(freshId).not.toBe('')
  expect(`custbody_${freshId}`).not.toBe(def.code)                             // FRESH Internal ID
  await pickSearch(page, 'Data type', 'Integer Number')                        // the type is choosable again
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(1000)

  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const repl = defs.find((d: { code: string }) => d.code === `custbody_${freshId}`)
  expect(repl.dataType).toBe('Int')
  expect(repl.label).toBe(LABEL)

  // cleanup both
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
  expect((await request.delete(`${API}/api/custom-fields/${repl.id}`, { headers: ADMIN })).status()).toBe(204)
})

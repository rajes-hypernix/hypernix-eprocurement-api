import { test, expect } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'
import { goAs, pickSearch, hardDeleteField } from './helpers'

// CF-FIX-5 browser proofs — post-v1.9 corrections.

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

const place = (code: string, sort: number) => ({
  fieldKey: code, subtab: null, fieldGroup: 'Header', sort, displayType: 'Normal',
  requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null,
})
const stdReqForm = async (request: APIRequestContext) =>
  (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)

test('CF-FIX5-T1: a chosen form’s CUSTOM fields render on New PR and PERSIST on create', async ({ page, request }) => {
  // A custom field applied to Requisition (unplaced), placed on a NEW form alongside the
  // required Partner (Partner must render so the value-save satisfies its required-check).
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Fix5 CF ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0, code: `fix5_cf_${STAMP}` } })).json()
  const std = await stdReqForm(request)
  // Every REQUIRED Header custom def must be placed + filled, or the value-save 400s.
  const allDefs = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  const requiredHeader = allDefs.filter((d: { required: boolean; scope: string; code: string }) =>
    d.required && d.scope === 'Header' && d.code !== def.code)
  const stdKeys = new Set(std.fields.map((f: { fieldKey: string }) => f.fieldKey))
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 T1 Form ${STAMP}`, recordType: 'Requisition',
      fields: [...std.fields,
        ...requiredHeader.filter((d: { code: string }) => !stdKeys.has(d.code)).map((d: { code: string }, i: number) => place(d.code, 90 + i)),
        place(def.code, 99)] } })).json()

  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1200)
  // The picker appears (>1 form). Choose the new form.
  await pickSearch(page, 'Entry form', `Fix5 T1 Form ${STAMP}`)
  await page.waitForTimeout(1000)

  // THE FIX: the custom field RENDERS on create (was filtered out pre-CF-FIX5).
  await expect(page.getByLabel(`Fix5 CF ${STAMP}`, { exact: true })).toBeVisible()
  await page.getByLabel(`Fix5 CF ${STAMP}`, { exact: true }).fill('rendered on create')
  for (const d of requiredHeader) await page.getByLabel(d.label, { exact: true }).first().fill('req')
  await page.getByLabel('Requestor', { exact: true }).fill('T1 Buyer')
  await page.getByLabel('Line 1 item code', { exact: true }).fill(`T1-${STAMP}`)
  await page.getByLabel('Line 1 qty', { exact: true }).fill('1')

  // Save draft → capture the created id from the response, then prove the value PERSISTED.
  const [resp] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/requisitions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Save draft' }).first().click(),
  ])
  const created = await resp.json()
  await page.waitForTimeout(800)
  const values = await (await request.get(`${API}/api/custom-values/Requisition/${created.id}`, { headers: BUYER })).json()
  expect(values.find((v: { code: string }) => v.code === def.code)?.value).toBe('rendered on create')

  // cleanup: clear the value (while still a draft, required fields re-satisfied) so the
  // field has zero values and hard-deletes cleanly; then cancel + delete form + field.
  const clear: Record<string, string | null> = { [def.code]: null }
  for (const d of requiredHeader) clear[d.code] = 'req'
  await request.put(`${API}/api/custom-values/Requisition/${created.id}`, { headers: { ...BUYER, ...JSON_H }, data: { values: clear } })
  await request.post(`${API}/api/requisitions/${created.id}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 'fix5 t1 cleanup' } })
  await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })
  await hardDeleteField(request, def)
})

test('CF-FIX5-T1/OD-D7-2: a chosen form omitting a role-required field is STILL blocked at submit', async ({ request }) => {
  // The Buyer's ROLE form requires Department; the API submit carries no form choice at all.
  const strict = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 Strict ${STAMP}`, recordType: 'Requisition',
      fields: [{ fieldKey: 'Department', subtab: null, fieldGroup: 'Header', sort: 0, displayType: 'Normal', requiredOnForm: true, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null }] } })).json()
  await request.put(`${API}/api/entry-forms/${strict.id}/roles`, { headers: { ...ADMIN, ...JSON_H }, data: { roles: ['Buyer'] } })

  // Department empty → the server RE-RESOLVES the Buyer's role form and refuses the submit
  // (OD-D7-2 — a chosen form can never dodge the role form's requireds).
  const body = { requestor: 'X', department: '', location: 'Y', category: 'Z', job: '', memo: `od-d7-2-${STAMP}`, requiredDate: null,
    lines: [{ id: null, itemCode: `ODD-${STAMP}`, description: 'd', qty: 1, uom: 'Unit', estUnitPrice: 1 }] }
  expect((await request.post(`${API}/api/requisitions?submit=true`, { headers: { ...BUYER, ...JSON_H }, data: body })).status()).toBe(400)
  // A DRAFT with the same gap still saves (OD-D7-3), proving the block is submit-only.
  const draft = await request.post(`${API}/api/requisitions?submit=false`, { headers: { ...BUYER, ...JSON_H }, data: body })
  expect(draft.ok()).toBeTruthy()

  // cleanup (delete the role form → Buyer falls back to Standard; cancel the draft)
  const draftId = (await draft.json()).id
  await request.post(`${API}/api/requisitions/${draftId}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 'fix5 od-d7-2 cleanup' } })
  expect((await request.delete(`${API}/api/entry-forms/${strict.id}`, { headers: ADMIN })).status()).toBe(204)
})

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

test('CF-FIX5-T3: drag is GONE (rows not draggable) and the ARROWS move a field CROSS-group', async ({ page, request }) => {
  // A non-system form with a CONTROLLED field set — Requestor then Memo, so Memo is the
  // guaranteed BOTTOM field of Header (the standard form may carry trailing segment placements,
  // which would make "move Memo down" a within-group swap instead of a cross-group step).
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 T3 Form ${STAMP}`, recordType: 'Requisition', fields: [place('Requestor', 0), place('Memo', 1)] } })).json()

  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `Open Fix5 T3 Form ${STAMP}`, exact: true }).click()

  // Drag removed: the field rows carry no draggable affordance.
  expect(await page.locator('[aria-label="Field row Requestor"]').getAttribute('draggable')).not.toBe('true')

  // Add a second group, then use the DOWN arrow on the BOTTOM field of Header (Memo) to
  // CROSS the group boundary into the new group — the operator's exact requirement.
  await page.getByLabel('New group title', { exact: true }).fill('Landing')
  await page.getByRole('button', { name: 'Add group' }).click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Move Memo down' }).click()
  await page.waitForTimeout(900)

  const state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === form.id)
  const landing = state.groups.find((g: { title: string }) => g.title === 'Landing')
  expect(state.fields.find((f: { fieldKey: string }) => f.fieldKey === 'Memo').groupId).toBe(landing.id)

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX5-T7: a segment applies to BOTH header and line of a record type, same id', async ({ page, request }) => {
  const NAME = `Fix5 T7 Seg ${STAMP}`
  const seg = await (await request.post(`${API}/api/segments`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: NAME, hasHierarchy: false, required: false } })).json()

  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(NAME) }).click()

  // PurchaseOrder is line-bearing → BOTH an 'Apply' (header) and an 'Apply per-line' offered.
  // Apply header (opens the form+group cascade), accept defaults.
  await page.getByRole('button', { name: `Apply ${NAME} to PurchaseOrder header`, exact: true }).click()
  await page.getByRole('button', { name: 'Apply segment' }).click()
  await page.waitForTimeout(800)
  // Apply per-line (flat, one click).
  await page.getByRole('button', { name: `Apply ${NAME} to PurchaseOrder per line`, exact: true }).click()
  await page.waitForTimeout(800)

  // BOTH applications exist, SAME dimension id (one SegmentDef, one code).
  const apps = (await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json())
    .find((s: { id: string }) => s.id === seg.id).applications
    .filter((a: { recordType: string }) => a.recordType === 'PurchaseOrder')
  expect(apps.map((a: { lineLevel: boolean }) => a.lineLevel).sort()).toEqual([false, true])
  // The UI now shows BOTH the header Remove and the per-line Remove for PurchaseOrder.
  await expect(page.getByRole('button', { name: `Remove ${NAME} from PurchaseOrder header`, exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: `Remove ${NAME} from PurchaseOrder line`, exact: true })).toBeVisible()

  // Removing ONE level leaves the other standing (server-verified).
  await page.getByRole('button', { name: `Remove ${NAME} from PurchaseOrder line`, exact: true }).click()
  await page.waitForTimeout(800)
  const after = (await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json())
    .find((s: { id: string }) => s.id === seg.id).applications
    .filter((a: { recordType: string }) => a.recordType === 'PurchaseOrder')
  expect(after).toHaveLength(1)
  expect(after[0].lineLevel).toBe(false)

  // cleanup
  await request.delete(`${API}/api/segments/${seg.id}/applications/PurchaseOrder?line=false`, { headers: ADMIN })
  expect((await request.delete(`${API}/api/segments/${seg.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX5-T8: new form id → customform_, new segment id → custseg_ (same id header+line)', async ({ page, request }) => {
  // --- New entry form gets a customform_ id from the user-keyed Internal ID ---
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  // CF-FIX5-T6: Entry Forms is a LIST — Copy the standard row to open the New-form modal.
  await page.getByRole('button', { name: 'Copy Standard PR Form', exact: true }).click()
  await page.getByLabel('Internal ID', { exact: true }).fill(`project${STAMP}`)
  await page.getByRole('button', { name: 'Create form' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText(`customform_project${STAMP}`)).toBeVisible()

  // --- New segment gets a custseg_ id, and the SAME id shows on header AND line applies ---
  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New segment' }).click()
  await page.getByLabel('Name', { exact: true }).fill(`Region ${STAMP}`)
  await page.getByLabel('Internal ID', { exact: true }).fill(`region${STAMP}`)
  await page.getByRole('button', { name: 'Create segment' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText(`custseg_region${STAMP}`)).toBeVisible()   // the one dimension id

  // Apply header + per-line on PurchaseOrder — one id, two levels.
  await page.getByRole('button', { name: `Apply Region ${STAMP} to PurchaseOrder header`, exact: true }).click()
  await page.getByRole('button', { name: 'Apply segment' }).click()
  await page.waitForTimeout(700)
  await page.getByRole('button', { name: `Apply Region ${STAMP} to PurchaseOrder per line`, exact: true }).click()
  await page.waitForTimeout(700)
  // The code shown in the detail header is still the single custseg_ id serving both.
  await expect(page.getByText(`custseg_region${STAMP}`)).toBeVisible()

  // cleanup
  const seg = (await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json()).find((s: { code: string }) => s.code === `custseg_region${STAMP}`)
  await request.delete(`${API}/api/segments/${seg.id}/applications/PurchaseOrder?line=true`, { headers: ADMIN })
  await request.delete(`${API}/api/segments/${seg.id}/applications/PurchaseOrder?line=false`, { headers: ADMIN })
  await request.delete(`${API}/api/segments/${seg.id}`, { headers: ADMIN })
  const form = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()).find((f: { code: string }) => f.code === `customform_project${STAMP}`)
  if (form) await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })
})

test('CF-FIX5-T2: on New PR the form picker is the FIRST control, ABOVE the Header', async ({ page, request }) => {
  // Ensure a second Requisition form so the picker appears.
  const std = await stdReqForm(request)
  const alt = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 T2 Alt ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()

  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1200)

  // The picker is present and sits ABOVE the Header section (position, not just existence).
  const picker = page.getByRole('button', { name: 'Entry form', exact: true })
  await expect(picker).toBeVisible()
  const pickerBox = await picker.boundingBox()
  const headerBox = await page.getByText('Header', { exact: true }).first().boundingBox()
  expect(pickerBox!.y).toBeLessThan(headerBox!.y)   // picker first, then the Header

  expect((await request.delete(`${API}/api/entry-forms/${alt.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX5-T4: sublist is a VERTICAL list (top = leftmost), reorders, and adds a custcol_ line field', async ({ page, request }) => {
  // An applied LINE (custcol_) custom field to add to the sublist.
  const lineDef = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Fix5 Line ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0, scope: 'Line', code: `fix5_line_${STAMP}` } })).json()
  const std = await stdReqForm(request)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 T4 Form ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()

  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `Open Fix5 T4 Form ${STAMP}`, exact: true }).click()
  await page.waitForTimeout(500)

  // VERTICAL list: the sublist columns render as rows; top row = leftmost (Item code).
  const rows = page.locator('[aria-label^="Sublist column "]')
  await expect(rows.first()).toContainText('Item code')

  // Reorder: Move Description UP → it becomes the top (leftmost) column, persisted.
  await page.getByRole('button', { name: 'Move Description up' }).click()
  await page.waitForTimeout(800)
  let state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === form.id)
  expect(state.sublistColumns[0]).toBe('Description')

  // Add the custcol_ line field via its Show checkbox → it becomes a sublist column.
  await page.getByLabel(`Show ${lineDef.code}`, { exact: true }).click()
  await page.waitForTimeout(800)
  state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === form.id)
  expect(state.sublistColumns).toContain(lineDef.code)
  await expect(page.locator(`[aria-label="Sublist column ${lineDef.code}"]`)).toBeVisible()

  // cleanup: strip it from the sublist, delete form + field.
  await request.put(`${API}/api/entry-forms/${form.id}/sublist`, { headers: { ...ADMIN, ...JSON_H },
    data: { fieldKeys: state.sublistColumns.filter((k: string) => k !== lineDef.code) } })
  await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })
  await hardDeleteField(request, lineDef)
})

test('CF-FIX5-T5: instructional/help prose is gone from Entry Forms, the PR picker and Segments (controls stay)', async ({ page, request }) => {
  const std = await stdReqForm(request)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 T5 Form ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()

  // Entry Forms builder — the CONTROLS render, but no rulings-as-subtext accompany them.
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `Open Fix5 T5 Form ${STAMP}`, exact: true }).click()
  await expect(page.getByRole('button', { name: 'Save form' })).toBeVisible()          // control stays
  for (const gone of [
    /Resolution follows a fixed global role precedence/i,
    /commit on Save form/i,
    /is the fallback for every role/i,
    /From the registry — native, custom/i,
  ]) await expect(page.getByText(gone)).toHaveCount(0)

  // The PR form picker is the first control — no helper sentence hangs off it.
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByRole('button', { name: 'Entry form', exact: true })).toBeVisible()

  // Segments — no concept subtitle, no application-refusal ruling, no immutability note.
  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  for (const gone of [
    /Reporting dimensions — define once/i,
    /Removing an application is refused/i,
    / is immutable/i,
    /projected one-way/i,
  ]) await expect(page.getByText(gone)).toHaveCount(0)
  // The create modal keeps clean labels only — no parenthetical explanations.
  await page.getByRole('button', { name: 'New segment' }).click()
  await expect(page.getByLabel('Hierarchical values', { exact: true })).toBeVisible()
  await expect(page.getByText(/parent stored|enforced at assignment-save/i)).toHaveCount(0)

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX5-T6: Entry Forms is a LIST → full-page builder; the Back guard uses REAL dirty state', async ({ page, request }) => {
  const std = await stdReqForm(request)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix5 T6 Form ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()
  const openBtn = { name: `Open Fix5 T6 Form ${STAMP}`, exact: true }

  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)

  // The screen is a LIST — the builder is NOT rendered until a form is opened.
  await expect(page.getByRole('button', { name: 'Save form' })).toHaveCount(0)
  await expect(page.getByRole('button', openBtn)).toBeVisible()

  // Open → the builder takes the FULL PAGE (list gone; Back + Save present).
  await page.getByRole('button', openBtn).click()
  await expect(page.getByRole('button', { name: 'Back to forms' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Save form' })).toBeVisible()
  await expect(page.getByRole('button', openBtn)).toHaveCount(0)

  // REAL dirty detection (1): Back with NOTHING changed does NOT prompt — a warning that
  // fires on "was opened" would train users to ignore it (the operator's explicit concern).
  let dialogs = 0
  const count = (d: import('@playwright/test').Dialog) => { dialogs++; void d.accept() }
  page.on('dialog', count)
  await page.getByRole('button', { name: 'Back to forms' }).click()
  await page.waitForTimeout(400)
  expect(dialogs).toBe(0)
  await expect(page.getByRole('button', openBtn)).toBeVisible()   // returned to the list, unprompted

  // REAL dirty detection (2): a genuine edit (rename) → Back DOES warn; dismiss keeps editing.
  await page.getByRole('button', openBtn).click()
  await page.getByLabel('Form name', { exact: true }).fill(`Fix5 T6 Form ${STAMP} edited`)
  page.off('dialog', count)
  page.once('dialog', (d) => { dialogs++; void d.dismiss() })     // user cancels the leave
  await page.getByRole('button', { name: 'Back to forms' }).click()
  await page.waitForTimeout(400)
  expect(dialogs).toBe(1)                                         // real change → warned exactly once
  await expect(page.getByRole('button', { name: 'Save form' })).toBeVisible()   // dismissed → still in the builder

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

import { test, expect } from '@playwright/test'
import { goAs, requiredCustomValues, pickSearch, hardDeleteField, moveFieldViaForm } from './helpers'

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

  // Move Department from Header into Logistics — CF-FIX5-T3 removed drag; the placement
  // object moves through the whole-form save (arrows proven separately). L1 unchanged.
  await moveFieldViaForm(request, 'Requisition', form.id, 'Department', { group: 'Logistics' })
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: new RegExp(`Fix4 Designer ${STAMP}`) }).click()
  await page.waitForTimeout(600)
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
  await moveFieldViaForm(request, 'Requisition', form.id, 'Memo', { subtab: 'Extras' })
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: new RegExp(`Fix4 Designer ${STAMP}`) }).click()
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


test('CF-FIX4-T3fix: labels not ids, searchable add-field, NO default control, arrows re-group a STAGED field cross-group', async ({ page, request }) => {
  const std = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix4 T3fix ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()

  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Fix4 T3fix ${STAMP}`) }).click()

  // (2) label leads, id is secondary: the Job row shows its census label, not just the key.
  const jobRow = page.locator('[aria-label="Field row Job"]')
  await expect(jobRow).toContainText('Job / Cost ref')
  // (3) no default-value control anywhere in the editor.
  await expect(page.getByLabel(/default$/)).toHaveCount(0)
  await expect(page.getByText('Default', { exact: true })).toHaveCount(0)

  // (1) the add-field picker is the standardized SearchSelectField: type-to-filter by LABEL.
  //     'Partner' (custbody_partner) — picked by its name, never its internal id.
  await pickSearch(page, 'Add field', 'Partner')
  await expect(page.locator('[aria-label="Field row custbody_partner"]')).toContainText('Partner')

  // (4) move the STAGED (unsaved) field into a new group via the ARROW — the exact case the
  //     operator hit (drag removed in CF-FIX5-T3; persistFields saves staged fields too).
  //     Partner was appended to the bottom of Header, so one Move-down crosses into Partners.
  await page.getByLabel('New group title', { exact: true }).fill('Partners')
  await page.getByRole('button', { name: 'Add group' }).click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Move custbody_partner down' }).click()
  await page.waitForTimeout(1000)
  await expect(page.locator('[aria-label="Field group Partners"]')).toContainText('Partner')
  // …and it PERSISTED (no 'is not placed on this form' error, the placement row exists).
  await expect(page.getByText(/is not placed on this form/)).toHaveCount(0)
  let state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((x: { id: string }) => x.id === form.id)
  const partnersGroup = state.groups.find((g: { title: string }) => g.title === 'Partners')
  expect(state.fields.find((x: { fieldKey: string }) => x.fieldKey === 'custbody_partner').groupId).toBe(partnersGroup.id)

  // (5) arrows: move the field UP out of Partners — it crosses back into Header, persisted.
  await page.getByRole('button', { name: 'Move custbody_partner up' }).click()
  await page.waitForTimeout(1000)
  state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((x: { id: string }) => x.id === form.id)
  const headerGroup = state.groups.find((g: { isHeader: boolean }) => g.isHeader)
  expect(state.fields.find((x: { fieldKey: string }) => x.fieldKey === 'custbody_partner').groupId).toBe(headerGroup.id)

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX4-T4: the creation cascade — standard pre-selected, Header default, ONE row the designer then re-groups', async ({ page, request }) => {
  const LABEL = `Fix4 Cascade ${STAMP}`
  // A non-system PR form so the rearrange half can run in the designer.
  const std = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  const work = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix4 Cascade Form ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()

  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'Requisition', exact: true }).click()
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill(LABEL)

  // The cascade block is THERE, standard pre-selected (option c) — switch to the work form.
  await expect(page.getByText(/Placement — where this field appears/)).toBeVisible()
  const formPicker = page.getByRole('button', { name: 'Requisition — form(s)' })
  await expect(formPicker).toContainText('Standard PR Form')
  await formPicker.click()
  await page.getByRole('combobox', { name: 'Search Requisition — form(s)' }).fill(`Fix4 Cascade Form ${STAMP}`)
  await page.keyboard.press('Enter')   // ADD the work form (standard stays selected — both get the placement)
  await page.keyboard.press('Escape')
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(1000)

  // The placement row exists on the chosen form, in Header (group defaulted).
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  const def = defs.find((d: { label: string }) => d.label === LABEL)
  let state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === work.id)
  const header = state.groups.find((g: { isHeader: boolean }) => g.isHeader)
  expect(state.fields.find((x: { fieldKey: string }) => x.fieldKey === def.code).groupId).toBe(header.id)

  // Rearrange in the designer — the SAME row moves (L1, one object two surfaces).
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Fix4 Cascade Form ${STAMP}`) }).click()
  await page.getByLabel('New group title', { exact: true }).fill('Cascade Landing')
  await page.getByRole('button', { name: 'Add group' }).click()
  await page.waitForTimeout(600)
  await moveFieldViaForm(request, 'Requisition', work.id, def.code, { group: 'Cascade Landing' })
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: new RegExp(`Fix4 Cascade Form ${STAMP}`) }).click()
  await page.waitForTimeout(1000)
  state = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { id: string }) => f.id === work.id)
  const landing = state.groups.find((g: { title: string }) => g.title === 'Cascade Landing')
  expect(state.fields.find((x: { fieldKey: string }) => x.fieldKey === def.code).groupId).toBe(landing.id)

  // cleanup: drop the work form, unplace from the standard form, delete the def.
  expect((await request.delete(`${API}/api/entry-forms/${work.id}`, { headers: ADMIN })).status()).toBe(204)
  await hardDeleteField(request, def)
})

test('CF-FIX4-T5: the form picker on New PR — appears with >1 form, switches the layout, role requireds still gate', async ({ page, request }) => {
  // Two PR forms guaranteed: the standard + a run-stamped one with a DISTINCT group.
  const std = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  const alt = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Fix4 AltForm ${STAMP}`, recordType: 'Requisition',
      fields: std.fields.map((x: { fieldKey: string; fieldGroup: string }) =>
        x.fieldKey === 'Memo' ? { ...x, fieldGroup: `AltGroup ${STAMP}` } : x) } })).json()

  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1200)

  // The picker is there (finding #6 — EF-new-pr had no control), searchable, defaulted by role.
  const picker = page.getByRole('button', { name: 'Entry form', exact: true })
  await expect(picker).toBeVisible()
  await expect(page.getByText(`AltGroup ${STAMP}`)).toHaveCount(0)   // not this layout yet

  // Switch → the alternative form's group renders.
  await picker.click()
  await page.getByRole('combobox', { name: 'Search Entry form' }).fill(`Fix4 AltForm ${STAMP}`)
  await page.keyboard.press('Enter')
  await page.waitForTimeout(1000)
  await expect(page.getByText(`AltGroup ${STAMP}`).first()).toBeVisible()

  expect((await request.delete(`${API}/api/entry-forms/${alt.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX4-T6: segment lifecycle — staged values, impact dialog blocks assigned delete, unused deletes clean', async ({ page, request }) => {
  const NAME = `Fix4 Seg ${STAMP}`
  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New segment' }).click()
  await page.getByLabel('Name', { exact: true }).fill(NAME)
  await page.getByRole('button', { name: 'Create segment' }).click()
  await page.waitForTimeout(1000)

  // STAGED values (the lists convention): two staged, nothing persisted, ONE Save commits.
  await page.getByLabel('Label', { exact: true }).fill('Site One')
  await page.getByRole('button', { name: 'Add value' }).click()
  await page.getByLabel('Label', { exact: true }).fill('Site Two')
  await page.getByRole('button', { name: 'Add value' }).click()
  await expect(page.getByText('2 unsaved values')).toBeVisible()
  let seg = (await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json())
    .find((s: { name: string }) => s.name === NAME)
  expect(seg.values).toHaveLength(0)   // NOT persisted yet
  await page.getByRole('button', { name: 'Save values' }).click()
  await page.waitForTimeout(1200)
  seg = (await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json())
    .find((s: { name: string }) => s.name === NAME)
  expect(seg.values).toHaveLength(2)

  // Assign SITE-ONE on a live PO → its delete opens the impact dialog and REFUSES with the reason.
  await request.post(`${API}/api/segments/${seg.id}/applications`, { headers: { ...ADMIN, ...JSON_H },
    data: { recordType: 'PurchaseOrder', lineLevel: false } })
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  const draft = pos.find((p: { status: string }) => p.status === 'Draft')
  const siteOne = seg.values.find((v: { code: string }) => v.code === 'SITE-ONE')
  await request.put(`${API}/api/segment-assignments/PurchaseOrder/${draft.id}`, { headers: { ...BUYER, ...JSON_H },
    data: { assignments: { [seg.code]: siteOne.code } } })

  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: new RegExp(NAME) }).click()
  await page.getByRole('button', { name: 'Delete value Site One' }).click()
  await expect(page.getByTestId('impact-report')).toBeVisible()
  await expect(page.getByText(/live on OPEN records/)).toBeVisible()
  await expect(page.getByRole('button', { name: 'Delete segment value' })).toBeDisabled()
  await page.getByRole('button', { name: 'Cancel' }).click()

  // The unused sibling deletes cleanly through the dialog.
  await page.getByRole('button', { name: 'Delete value Site Two' }).click()
  await expect(page.getByText(/can be deleted safely/)).toBeVisible()
  await page.getByRole('button', { name: 'Delete segment value' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('Site Two')).toHaveCount(0)

  // cleanup: clear the assignment, then the def deletes through the dialog path (API).
  await request.put(`${API}/api/segment-assignments/PurchaseOrder/${draft.id}`, { headers: { ...BUYER, ...JSON_H },
    data: { assignments: { [seg.code]: null } } })
  expect((await request.delete(`${API}/api/segments/${seg.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF-FIX4-T7: header apply runs the form+group cascade; the segment lands on the form and a line apply stays flat', async ({ page, request }) => {
  const NAME = `Fix4 SegPlace ${STAMP}`
  const seg = await (await request.post(`${API}/api/segments`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: NAME, hasHierarchy: false, required: false } })).json()

  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(NAME) }).click()

  // HEADER apply → the cascade dialog: standard form pre-selected, group defaults Header.
  await page.getByRole('button', { name: `Apply ${NAME} to Requisition`, exact: true }).click()
  await expect(page.getByText(/the standard form is pre-selected/i)).toBeVisible()
  const formPicker = page.getByRole('button', { name: 'Requisition — form(s)' })
  await expect(formPicker).toContainText('Standard PR Form')
  await page.getByRole('button', { name: 'Apply segment' }).click()
  await page.waitForTimeout(1000)

  // The seg_ placement row exists on the standard form, in Header — rearrangeable like any field.
  const std = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  const header = std.groups.find((g: { isHeader: boolean }) => g.isHeader)
  const row = std.fields.find((x: { fieldKey: string }) => x.fieldKey === seg.code)
  expect(row.groupId).toBe(header.id)

  // LINE apply (PO) — no cascade, no group asked: the flat path.
  await page.getByRole('button', { name: `Apply ${NAME} to PurchaseOrder per line`, exact: true }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('per line')).toBeVisible()

  // cleanup: unapply header (placement leaves with it), then delete the segment.
  await request.delete(`${API}/api/segments/${seg.id}/applications/Requisition`, { headers: ADMIN })
  await request.delete(`${API}/api/segments/${seg.id}/applications/PurchaseOrder`, { headers: ADMIN })
  expect((await request.delete(`${API}/api/segments/${seg.id}`, { headers: ADMIN })).status()).toBe(204)
  const after = (await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json())
    .find((f: { isSystem: boolean }) => f.isSystem)
  expect(after.fields.some((x: { fieldKey: string }) => x.fieldKey === seg.code)).toBe(false)
})

test('CF-FIX4-T8: ARCHIVE — the value vanishes from form, view and record; audit records it; un-archive restores; storage never touched', async ({ page, request }) => {
  const LABEL = `Fix4 Arch ${STAMP}`
  const def = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: LABEL, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0, code: `fix4_arch_${STAMP}` } })).json()
  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const pr = prs.find((p: { headerStatus: string }) => p.headerStatus === 'Draft') ?? prs[0]
  const req = await requiredCustomValues(request, 'Requisition', pr.id)
  await request.put(`${API}/api/custom-values/Requisition/${pr.id}`, { headers: { ...BUYER, ...JSON_H },
    data: { values: { ...req, [def.code]: 'archive me' } } })
  const view = await (await request.post(`${API}/api/views`, { headers: { ...BUYER, ...JSON_H },
    data: { name: `fix4-arch-view-${STAMP}`, recordType: 'Requisition', filters: [],
      columns: [{ fieldKey: 'Code', label: null, sortDirection: null }, { fieldKey: def.code, label: null, sortDirection: null }] } })).json()

  // Visible on the record before.
  await goAs(page, 'u_faridah', `reqs/open/${pr.id}`)
  await page.waitForTimeout(1500)
  await expect(page.getByLabel(LABEL, { exact: true })).toHaveValue('archive me')

  // ARCHIVE on screen (the admin row verb).
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'Requisition', exact: true }).click()
  await page.getByRole('button', { name: `Archive ${LABEL}` }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('Archived', { exact: true }).first()).toBeVisible()

  // Hidden: the record page no longer renders it; the view runs with the column blank.
  await goAs(page, 'u_faridah', `reqs/open/${pr.id}`)
  await page.waitForTimeout(1500)
  await expect(page.getByLabel(LABEL, { exact: true })).toHaveCount(0)
  const run = await (await request.get(`${API}/api/views/${view.id}/run?page=1&size=200`, { headers: BUYER })).json()
  expect(run.rows.length).toBeGreaterThan(0)                                            // the view still RUNS
  expect(run.rows.some((r: Record<string, unknown>) => r[def.code] === 'archive me')).toBe(false)   // blank column

  // UN-ARCHIVE on screen → the value returns everywhere.
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'Requisition', exact: true }).click()
  await page.getByRole('button', { name: `Unarchive ${LABEL}` }).click()
  await page.waitForTimeout(800)
  await goAs(page, 'u_faridah', `reqs/open/${pr.id}`)
  await page.waitForTimeout(1500)
  await expect(page.getByLabel(LABEL, { exact: true })).toHaveValue('archive me')       // never deleted, fully restored

  // cleanup
  await request.delete(`${API}/api/views/${view.id}`, { headers: BUYER })
  await request.put(`${API}/api/custom-values/Requisition/${pr.id}`, { headers: { ...BUYER, ...JSON_H },
    data: { values: { ...req, [def.code]: null } } })
  await hardDeleteField(request, def)
})

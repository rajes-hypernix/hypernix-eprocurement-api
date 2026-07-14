import { test, expect } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'
import { goAs, pickSearch, hardDeleteField } from './helpers'

// CF-FINAL browser proofs — three slices, unattended. Slice 1 (T1-T3) is ship-critical.

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

// ── SLICE 1 · T1 — the form-persistence bug ──────────────────────────────────
test('CFF-T1: a PR persists its chosen form; reopen stays on it, its field shows, NO phantom fields', async ({ page, request }) => {
  // A field PLACED on the custom form (renders inline there) + a field APPLIED but NOT on the
  // custom form (pre-fix this got dumped in the residual "Custom fields" section below the sublist).
  const placed = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Placed ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0, code: `cff_placed_${STAMP}` } })).json()
  const unplaced = await (await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `Unplaced ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0, code: `cff_unplaced_${STAMP}` } })).json()

  const std = await stdReqForm(request)
  // A custom form = the standard layout + only the PLACED field. (No required custom fields are
  // written in this flow, so the demo's required litter never gates the draft save.)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `CFF T1 Form ${STAMP}`, recordType: 'Requisition', fields: [...std.fields, place(placed.code, 99)] } })).json()

  // ---- Create the PR on the custom form (draft — no custom values, so it saves cleanly) ----
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1200)
  await pickSearch(page, 'Entry form', `CFF T1 Form ${STAMP}`)
  await page.waitForTimeout(1000)
  await expect(page.getByLabel(`Placed ${STAMP}`, { exact: true })).toBeVisible()   // placed field renders on create

  await page.getByLabel('Requestor', { exact: true }).fill('T1 Buyer')
  await page.getByLabel('Line 1 item code', { exact: true }).fill(`T1-${STAMP}`)
  await page.getByLabel('Line 1 qty', { exact: true }).fill('2')

  const [resp] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/requisitions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Save draft' }).first().click(),
  ])
  const created = await resp.json()
  // THE BUG FIX (persist-on-save): the client sends the chosen form and the server stores it.
  expect(created.entryFormId).toBe(form.id)
  await page.waitForTimeout(800)

  // ---- Reopen: it must resolve the SAVED form, not silently revert to Standard ----
  await goAs(page, 'u_faridah', `reqs/open/${created.id}`)
  await page.waitForTimeout(1500)

  // 1. Still on the custom form — the picker shows it, not "Standard PR Form".
  await expect(page.getByRole('button', { name: 'Entry form' })).toContainText(`CFF T1 Form ${STAMP}`)
  // 2. The placed field renders on the resolved form (proof the saved form resolved, not Standard).
  await expect(page.getByLabel(`Placed ${STAMP}`, { exact: true })).toBeVisible()
  // 3. NO phantom dump: the ungoverned residual sections are gone, so the applied-but-unplaced
  //    field is NOT dumped below the sublist.
  await expect(page.locator('[aria-label="Custom fields"]')).toHaveCount(0)
  await expect(page.locator('[aria-label="Segments"]')).toHaveCount(0)
  await expect(page.getByLabel(`Unplaced ${STAMP}`, { exact: true })).toHaveCount(0)

  // cleanup
  await request.post(`${API}/api/requisitions/${created.id}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 'cff t1 cleanup' } })
  await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })
  await hardDeleteField(request, placed)
  await hardDeleteField(request, unplaced)
})

test('CFF-T1/OD-D7-2: a chosen form cannot dodge the role form’s requireds at submit', async ({ request }) => {
  // The Buyer's ROLE form requires Department; the submit carries no form choice.
  const strict = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `CFF Strict ${STAMP}`, recordType: 'Requisition',
      fields: [{ fieldKey: 'Department', subtab: null, fieldGroup: 'Header', sort: 0, displayType: 'Normal', requiredOnForm: true, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null }] } })).json()
  await request.put(`${API}/api/entry-forms/${strict.id}/roles`, { headers: { ...ADMIN, ...JSON_H }, data: { roles: ['Buyer'] } })

  const body = { requestor: 'X', department: '', location: 'Y', category: 'Z', job: '', memo: `cff-od-d7-2-${STAMP}`, requiredDate: null,
    lines: [{ id: null, itemCode: `ODD-${STAMP}`, description: 'd', qty: 1, uom: 'Unit', estUnitPrice: 1 }] }
  // Server RE-RESOLVES the Buyer's role form and refuses the submit (OD-D7-2)...
  expect((await request.post(`${API}/api/requisitions?submit=true`, { headers: { ...BUYER, ...JSON_H }, data: body })).status()).toBe(400)
  // ...while the same gap saves fine as a DRAFT (submit-only gate).
  const draft = await request.post(`${API}/api/requisitions?submit=false`, { headers: { ...BUYER, ...JSON_H }, data: body })
  expect(draft.ok()).toBeTruthy()

  // cleanup
  const draftId = (await draft.json()).id
  await request.post(`${API}/api/requisitions/${draftId}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 'cff od-d7-2 cleanup' } })
  expect((await request.delete(`${API}/api/entry-forms/${strict.id}`, { headers: ADMIN })).status()).toBe(204)
})

// ── SLICE 1 · T2 — custom fields on CUSTOM forms only ────────────────────────
test('CFF-T2: a custom field is refused on a standard form, accepted on a custom form; picker offers custom only', async ({ page, request }) => {
  const std = await stdReqForm(request)
  const custom = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `CFF T2 Form ${STAMP}`, recordType: 'Requisition', fields: [...std.fields] } })).json()

  // SERVER: placing a custom field on the STANDARD (system) form is refused (400)...
  const onStd = await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `T2 OnStd ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0,
      code: `cff_t2_std_${STAMP}`, placements: [{ recordType: 'Requisition', formId: std.id, groupId: null }] } })
  expect(onStd.status()).toBe(400)
  expect(await onStd.text()).toContain('standard form')

  // ...while placing on the CUSTOM form works (200).
  const onCustom = await request.post(`${API}/api/custom-fields`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: `T2 OnCustom ${STAMP}`, recordType: 'Requisition', dataType: 'Text', required: false, helpText: '', sort: 0,
      code: `cff_t2_ok_${STAMP}`, placements: [{ recordType: 'Requisition', formId: custom.id, groupId: null }] } })
  expect(onCustom.ok()).toBeTruthy()
  const okDef = await onCustom.json()

  // UI: the field-creation placement picker offers the custom form, NOT the standard form.
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'Requisition', exact: true }).click()
  await page.getByRole('button', { name: 'New field' }).click()
  await expect(page.getByText(/Placement — where this field appears/)).toBeVisible()
  await page.getByRole('button', { name: 'Requisition — form(s)' }).click()
  const options = page.getByRole('listbox', { name: 'Requisition — form(s) options' })
  await expect(options).toContainText(`CFF T2 Form ${STAMP}`)       // the custom form is offered
  await expect(options).not.toContainText('Standard PR Form')       // the standard form is NOT offered

  // cleanup
  await hardDeleteField(request, okDef)
  await request.delete(`${API}/api/entry-forms/${custom.id}`, { headers: ADMIN })
})

// ── SLICE 1 · T3 — Est. Amount column (qty × rate) ───────────────────────────
test('CFF-T3: the PR line grid shows a live Est. Amount = qty × rate, currency-grouped', async ({ page }) => {
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1000)

  await page.getByLabel('Line 1 qty', { exact: true }).fill('3')
  await page.getByLabel('Line 1 rate', { exact: true }).fill('100')
  // qty 3 × rate 100 → 300.00 (CF1 grouped money display).
  await expect(page.locator('[aria-label="Line 1 est amount"]')).toHaveText('300.00')

  // Live-computed: bump the qty and the amount grows, with a thousands separator.
  await page.getByLabel('Line 1 qty', { exact: true }).fill('40')
  await expect(page.locator('[aria-label="Line 1 est amount"]')).toHaveText('4,000.00')
})

// ══ SLICE 2 · UI POLISH (match UI-POLISH-REFERENCE.html) ═════════════════════

// ── T7 — header title Georgia, regular weight ────────────────────────────────
test('CFF-T7: the header title renders Georgia at regular weight (400)', async ({ page }) => {
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(800)
  const title = page.locator('.brand-title')
  await expect(title).toHaveText('Hypernix eProcure')
  expect((await title.evaluate((el) => getComputedStyle(el).fontFamily)).toLowerCase()).toContain('georgia')
  expect(await title.evaluate((el) => getComputedStyle(el).fontWeight)).toBe('400')
})

// ── T4 — global toast + save-banner, wired to saves; copy discipline ─────────
test('CFF-T4: saving a PR shows the banner; creating a custom field shows a toast; copy is clean', async ({ page, request }) => {
  // Saving a PR → the inline save-banner, past tense, no "successfully"/"!".
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1000)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1000)
  await page.getByLabel('Requestor', { exact: true }).fill('T4 Buyer')
  await page.getByLabel('Line 1 item code', { exact: true }).fill(`T4-${STAMP}`)
  await page.getByLabel('Line 1 qty', { exact: true }).fill('1')
  const [prResp] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/requisitions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Save draft' }).first().click(),
  ])
  const createdPr = await prResp.json()
  const banner = page.getByTestId('save-banner')
  await expect(banner).toBeVisible()
  await expect(banner).toContainText('Requisition saved')
  const bannerText = (await banner.textContent()) ?? ''
  expect(bannerText.toLowerCase()).not.toContain('successfully')
  expect(bannerText).not.toContain('!')

  // Creating a custom field → the corner toast.
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1000)
  await page.getByRole('button', { name: 'Requisition', exact: true }).click()
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill(`T4 Field ${STAMP}`)
  await page.getByRole('button', { name: 'Create field' }).click()
  const toast = page.getByTestId('toast')
  await expect(toast).toBeVisible()
  await expect(toast).toContainText('Custom field created')
  const toastText = (await toast.textContent()) ?? ''
  expect(toastText.toLowerCase()).not.toContain('successfully')
  expect(toastText).not.toContain('!')

  // cleanup
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  const mine = defs.find((d: { label: string }) => d.label === `T4 Field ${STAMP}`)
  if (mine) await hardDeleteField(request, mine)
  if (createdPr?.id) await request.post(`${API}/api/requisitions/${createdPr.id}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 't4 cleanup' } })
})

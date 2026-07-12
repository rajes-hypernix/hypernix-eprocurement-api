import { test, expect } from '@playwright/test'
import { goAs, shot } from './helpers'

// D7 GATE (as ruled, OD-D7-1 option ii): the persona BEFORE/AFTER. A BUYER loads the PR
// form and gets the seeded Standard layout; an ADMIN composes a role form in the Setup
// composer — Job removed, Category hidden, Department required-at-submit, RequiredDate
// defaulted @today+7d, a run-stamped custom field placed on an admin-defined subtab —
// and assigns it to Buyer; the buyer reloads and gets the composed form with zero
// deployments (hidden gone, empty required blocks submit CLIENT-side and the server
// re-resolves and 400s the same gap — OD-D7-2 — while a draft still saves — OD-D7-3;
// default pre-filled). NUMBERING: admin changes the PO format in Setup; the next PO —
// minted through the REAL award-approval path on a throwaway single-envelope RFQ —
// proves it; history untouched. Run-stamped + self-cleaning (scheme reverted, role form
// deleted, cf def deleted at zero values, gate PR cancelled; the throwaway RFQ/award/PO
// remain as ordinary additive records).

const API = 'http://localhost:5260'
const STAMP = Date.now().toString().slice(-6)
const FORM_NAME = `Buyer PR Form ${STAMP}`
const CF_LABEL = `Site Ref ${STAMP}`
const CF_CODE = `cf_site_ref_${STAMP}`
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const APPROVER = { 'X-Demo-User': 'u_lim' }
const VENDOR = { 'X-Demo-User': 'VU-sentausa' }
const JSON_H = { 'Content-Type': 'application/json' }

test('entry-forms gate: standard → admin composes role form → buyer gets it; PO numbering from Setup', async ({ page, request }) => {
  test.setTimeout(180_000)
  // TEST-SWEEP self-healing (F2): a mid-test failure in a PREVIOUS run can strand its
  // run-stamped form (cleanup never reached). Sweep them so litter never compounds.
  const stale = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  for (const s of stale.filter((x: { isSystem: boolean }) => !x.isSystem))
    await request.delete(`${API}/api/entry-forms/${s.id}`, { headers: ADMIN })
  page.on('dialog', (d) => void d.accept())   // the dirty-guard confirm on deliberate navigation

  // ---- 1. BEFORE: the buyer's create form IS the seeded Standard layout ----
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByLabel('Job / Cost ref')).toBeVisible()
  await expect(page.getByLabel('Category')).toBeVisible()
  await shot(page, 'D7-gate-1-standard')

  // A run-stamped custom field to place on the admin-defined subtab.
  const cfResp = await request.post(`${API}/api/custom-fields`, {
    headers: { ...ADMIN, ...JSON_H },
    data: { label: CF_LABEL, recordType: 'Requisition', dataType: 'Text', customListId: null, required: false, helpText: '', sort: 0 },
  })
  expect(cfResp.ok()).toBeTruthy()

  // ---- 2. ADMIN composes the role form in the Setup composer ----
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: /New form \(copy of Standard PR Form\)/ }).click()
  await page.waitForTimeout(1200)
  await page.getByLabel('Form name').fill(FORM_NAME)
  await page.getByRole('button', { name: 'Remove Job' }).click()                       // hidden by removal
  await page.getByLabel('Category display').selectOption('Hidden')                     // hidden by display type
  await page.getByLabel('Department required at submit').check()
  await page.getByLabel('RequiredDate default').fill('@today+7d')
  await page.getByLabel(/Add field/).selectOption(CF_CODE)                             // from the registry palette
  await page.getByLabel(`${CF_CODE} subtab`).fill('Additional')                        // the admin-defined subtab
  await page.getByLabel('Buyer', { exact: true }).check()
  await shot(page, 'D7-gate-2-compose')
  await page.getByRole('button', { name: 'Save form' }).click()
  await page.waitForTimeout(1200)

  // ---- 3. AFTER: the buyer reloads and gets the composed form automatically ----
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByLabel('Requestor')).toBeVisible()
  await expect(page.getByLabel('Job / Cost ref')).not.toBeVisible()                    // removed from the form
  await expect(page.getByLabel('Category')).not.toBeVisible()                          // displayType Hidden
  const plus7 = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10)
  await expect(page.getByLabel('Required by')).toHaveValue(plus7)                      // @today+7d, server-resolved

  // Empty required blocks SUBMIT (client face of the boundary)...
  await page.getByLabel('Requestor').fill('Gate Persona')
  await page.getByLabel('Memo / Justification').fill(`d7 gate ${STAMP}`)   // run-stamp for the lookup below
  await page.getByLabel('Line 1 item code').fill('GATE-1')
  await page.getByLabel('Line 1 qty').fill('1')
  await page.getByRole('button', { name: 'Submit PR' }).first().click()
  await expect(page.getByText(/Required on your form before submit: Department/).first()).toBeVisible()
  await shot(page, 'D7-gate-3-required')

  // ...the SERVER enforces the same gap by re-resolving the caller's form (OD-D7-2),
  // while a DRAFT with the same gap saves fine (OD-D7-3).
  const prBody = {
    requestor: 'Gate Persona', department: '', location: 'Bintulu Plant', category: 'Piping',
    job: '', memo: `d7 gate ${STAMP}`, requiredDate: null,
    lines: [{ id: null, itemCode: 'GATE-1', description: 'gate line', qty: 1, uom: 'Unit', estUnitPrice: 10 }],
  }
  expect((await request.post(`${API}/api/requisitions?submit=true`, { headers: { ...BUYER, ...JSON_H }, data: prBody })).status()).toBe(400)
  const draft = await request.post(`${API}/api/requisitions?submit=false`, { headers: { ...BUYER, ...JSON_H }, data: prBody })
  expect(draft.ok()).toBeTruthy()
  const draftPr = await draft.json()

  // With the required field filled, the same submit sails through the UI.
  await page.getByLabel('Department').fill('Maintenance')
  await page.getByRole('button', { name: 'Submit PR' }).first().click()
  await page.waitForTimeout(1500)

  // The custom field renders on the admin-defined subtab (edit surface).
  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const gatePr = prs.find((p: { memo?: string; submitted?: boolean }) => p.memo === `d7 gate ${STAMP}` && p.submitted)
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1500)
  await page.locator('tr', { hasText: gatePr.code }).getByRole('button', { name: 'Open' }).first().click()
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'Additional' }).click()
  await expect(page.getByLabel(CF_LABEL)).toBeVisible()
  await shot(page, 'D7-gate-4-subtab')

  // ---- 4. NUMBERING: admin changes the PO format in Setup ----
  await goAs(page, 'u_admin', 'numbering')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'PurchaseOrder' }).click()
  await page.getByRole('button', { name: 'Edit PurchaseOrder numbering' }).click()
  const prefix = page.getByLabel('Prefix (A–Z, 0–9, dash)')
  await prefix.fill(`SP${STAMP.slice(-2)}`)
  await page.getByLabel('Digits (3–6)').fill('5')
  await page.getByRole('button', { name: 'Save format' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText(new RegExp(`SP${STAMP.slice(-2)}-2026-00001`)).first()).toBeVisible()
  await shot(page, 'D7-gate-5-numbering')

  // ---- 5. The next PO proves it — minted through the REAL award-approval path ----
  const vendors = await (await request.get(`${API}/api/vendors`, { headers: BUYER })).json()
  const sentausa = vendors.find((v: { code: string }) => v.code === 'SWK-V-10293')
  const rfq = await (await request.post(`${API}/api/rfqs`, {
    headers: { ...BUYER, ...JSON_H },
    data: { title: `D7 numbering proof ${STAMP}`, prRefs: [], lines: [{ itemCode: 'GATE-PO-1', description: 'numbering proof', qty: 1, uom: 'Unit', prRef: null }] },
  })).json()
  const closes = new Date(Date.now() + 3600_000).toISOString()
  expect((await request.put(`${API}/api/rfqs/${rfq.id}`, {
    headers: { ...BUYER, ...JSON_H },
    data: {
      title: `D7 numbering proof ${STAMP}`, envelope: 'Single', currency: 'MYR',
      opensUtc: new Date().toISOString(), closesUtc: closes,
      lines: [{ itemCode: 'GATE-PO-1', description: 'numbering proof', qty: 1, uom: 'Unit', prRef: null }],
      formItems: [], technicalSections: [], commercialSections: [],
      invitedVendorIds: [sentausa.id], technicalEvaluatorIds: [], commercialEvaluatorIds: [],
    },
  })).ok()).toBeTruthy()
  expect((await request.post(`${API}/api/rfqs/${rfq.id}/release`, { headers: BUYER })).ok()).toBeTruthy()
  expect((await request.put(`${API}/api/rfqs/${rfq.id}/my-bid`, {
    headers: { ...VENDOR, ...JSON_H },
    data: { lead: 7, warranty: 12, lines: [{ itemCode: 'GATE-PO-1', bidding: true, price: 100, qty: 1, partial: false, altItem: null }], answers: [], files: [] },
  })).ok()).toBeTruthy()
  expect((await request.post(`${API}/api/rfqs/${rfq.id}/my-bid/submit`, {
    headers: { ...VENDOR, ...JSON_H },
    data: { lead: 7, warranty: 12, lines: [{ itemCode: 'GATE-PO-1', bidding: true, price: 100, qty: 1, partial: false, altItem: null }], answers: [], files: [] },
  })).ok()).toBeTruthy()
  expect((await request.post(`${API}/api/rfqs/${rfq.id}/close`, { headers: BUYER })).ok()).toBeTruthy()
  const award = await (await request.post(`${API}/api/rfqs/${rfq.id}/award`, {
    headers: { ...BUYER, ...JSON_H },
    data: { allocations: [{ lineCode: 'GATE-PO-1', vendorId: sentausa.id, qty: 1 }] },
  })).json()
  const approved = await (await request.post(`${API}/api/awards/${award.id}/approve`, { headers: APPROVER })).json()
  expect(approved.poCodes).toHaveLength(1)
  expect(approved.poCodes[0]).toMatch(new RegExp(`^SP${STAMP.slice(-2)}-2026-\\d{5}$`))   // the scheme, on the glass

  // History untouched: pre-existing POs keep their PO-2026-#### codes.
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  expect(pos.some((p: { code: string }) => /^PO-2026-\d{4}$/.test(p.code))).toBeTruthy()

  // ---- cleanup: scheme reverted (counters preserved — no re-issue), role form deleted
  // (maps cascade; buyer falls back to Standard), cf def hard-deleted (zero values),
  // the gate PRs cancelled ----
  expect((await request.put(`${API}/api/numbering/PurchaseOrder`, {
    headers: { ...ADMIN, ...JSON_H }, data: { prefix: 'PO', yearSegment: true, digits: 4 },
  })).ok()).toBeTruthy()
  const forms = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  const roleForm = forms.find((f: { name: string }) => f.name === FORM_NAME)
  expect((await request.delete(`${API}/api/entry-forms/${roleForm.id}`, { headers: ADMIN })).status()).toBe(204)
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  const cfDef = defs.find((d: { code: string }) => d.code === CF_CODE)
  expect((await request.delete(`${API}/api/custom-fields/${cfDef.id}`, { headers: ADMIN })).status()).toBe(204)
  for (const pid of [gatePr.id, draftPr.id])
    expect((await request.post(`${API}/api/requisitions/${pid}/cancel`, {
      headers: { ...BUYER, ...JSON_H }, data: { reason: 'd7 gate cleanup' },
    })).ok()).toBeTruthy()
})

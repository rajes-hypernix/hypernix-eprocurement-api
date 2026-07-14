import { test, expect } from '@playwright/test'
import { goAs, pickSearch , requiredCustomValues, fillRequiredCustomFields, hardDeleteField, moveFieldViaForm } from './helpers'

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
  // The PO DETAIL page (fold-independent — the paged list may not show this PO on page 1
  // once numbering-test litter accumulates; the rendering pathway is the same).
  await goAs(page, 'u_faridah', `pos/${big.id}`)
  await page.waitForTimeout(1200)
  const grouped = Number(big.total).toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  await expect(page.getByText(`RM ${grouped}`).first()).toBeVisible()

  // Edit surface: a Money custom field groups on blur and SUBMITS the raw numeric.
  const cf = await (await request.post(`${API}/api/custom-fields`, {
    headers: { ...ADMIN, ...JSON_H },
    data: { label: `Budget Cap ${STAMP}`, recordType: 'PurchaseOrder', dataType: 'Money', customListId: null, required: false, helpText: '', sort: 0 },
  })).json()
  // Same URL as the read-surface visit — a goAs would be a same-document hash nav and the
  // section's cached query would never see the just-created def; force a real reload.
  await page.reload({ waitUntil: 'networkidle' })
  await page.waitForTimeout(1500)
  const field = page.getByLabel(`Budget Cap ${STAMP}`)
  await field.fill('123456.5')
  await field.blur()
  await expect(field).toHaveValue('123,456.50')          // grouped display on blur
  await field.focus()
  await expect(field).toHaveValue('123456.5')            // raw numeric back on focus (round-trip)
  await field.blur()
  await fillRequiredCustomFields(page, request, 'PurchaseOrder', big.id)   // e.g. the operator's required 'Partner'
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
  const listCode = `CUSTLIST_CFLIST${STAMP}`   // CF-FIX2-T1: creates are stored with the contextual prefix
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
  await pickSearch(page, 'Show options in', 'Alphabetical')
  await page.getByRole('button', { name: 'Save list' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText('A→Z')).toBeVisible()                    // order-mode badge on the glass

  // The order mode drives a REAL picker: the bound field's options are now A-then-Z.
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  await goAs(page, 'u_faridah', `pos/${pos[0].id}`)
  await page.waitForTimeout(1500)
  // CF-FIX1-T7: ListValue fields render the searchable select — open it and read the list.
  await page.getByRole('button', { name: `Pick ${STAMP}`, exact: true }).click()
  const opts = await page.getByRole('listbox', { name: `Pick ${STAMP} options` }).getByRole('option').allTextContents()
  await page.keyboard.press('Escape')
  const labels = opts.filter((o) => o.includes('Zebra') || o.includes('Aardvark'))
  expect(labels.map((l) => l.trim())).toEqual(['Aardvark', 'Zebra'])   // alphabetical, not entered order

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

test('CF1-T3: every Administration nav item renders a DISTINCT icon', async ({ page }) => {
  await goAs(page, 'u_admin', 'admin')
  await page.waitForTimeout(1200)
  const names: string[] = []
  for (const label of ['User Management', 'Custom Lists', 'Custom Fields', 'Segments', 'Entry Forms', 'Numbering']) {
    const icon = page.getByRole('button', { name: label }).locator('svg[data-icon]').first()
    names.push((await icon.getAttribute('data-icon'))!)
  }
  expect(new Set(names).size, `admin icons must be pairwise distinct: ${names.join(',')}`).toBe(names.length)
  for (const n of names) {
    // and each glyph actually DRAWS something (the blank-icon regression class)
    const paths = await page.locator(`svg[data-icon="${n}"]`).first().locator('> *').count()
    expect(paths, `glyph ${n} draws`).toBeGreaterThan(0)
  }
})

test('CF1-T4: sidebar collapses to a slim rail, persists across reload, expands back', async ({ page }) => {
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1200)
  const side = page.locator('aside.side')
  const wide = (await side.boundingBox())!.width
  await page.getByRole('button', { name: 'Collapse sidebar' }).click()
  await page.waitForTimeout(400)
  const slim = (await side.boundingBox())!.width
  expect(slim).toBeLessThan(wide / 2)                                   // slim icon-only rail
  await expect(page.locator('aside .nav', { hasText: 'Requisitions' })).toHaveCount(0)  // labels hidden
  await expect(page.locator('aside [aria-label="Requisitions"]')).toBeVisible()          // icon remains, accessible

  await page.reload({ waitUntil: 'networkidle' })
  await page.waitForTimeout(800)
  expect((await side.boundingBox())!.width).toBeLessThan(wide / 2)      // persisted
  await page.getByRole('button', { name: 'Expand sidebar' }).click()
  await page.waitForTimeout(400)
  expect((await side.boundingBox())!.width).toBeGreaterThan(wide / 2)   // and back
})

test('CF1-T5: global search deep-links PR / ASN / Statement to their DETAIL, regressions hold', async ({ page, request }) => {
  // PR: was the list; now the PR's own form.
  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const pr = prs.find((p: { submitted: boolean }) => p.submitted) ?? prs[0]
  await goAs(page, 'u_faridah', 'dashboard')
  const search = page.getByLabel('Global search')
  await search.fill(pr.code)
  await page.waitForTimeout(900)
  await page.locator('.gsr-item', { hasText: pr.code }).first().click()
  await page.waitForTimeout(1200)
  expect(page.url()).toContain(`reqs/open/${pr.id}`)
  await expect(page.getByRole('heading', { name: `Edit ${pr.code}` })).toBeVisible()   // the PR itself, not the list

  // ASN: was a dead click (no hits existed at all).
  const asns = await (await request.get(`${API}/api/asns`, { headers: BUYER })).json()
  await goAs(page, 'u_faridah', 'dashboard')
  await search.fill(asns[0].code)
  await page.waitForTimeout(900)
  await page.locator('.gsr-item', { hasText: asns[0].code }).first().click()
  await page.waitForTimeout(1200)
  expect(page.url()).toContain(`deliveries/asn/${asns[0].id}`)

  // Statement: hit id = vendorId → the statement detail.
  await goAs(page, 'u_faridah', 'dashboard')
  await search.fill('Sentausa')
  await page.waitForTimeout(900)
  await page.locator('.gsr-item', { hasText: /Statement — Sentausa/ }).first().click()
  await page.waitForTimeout(1200)
  expect(page.url()).toContain('statements/')

  // Regression: PO still deep-links.
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  await goAs(page, 'u_faridah', 'dashboard')
  await search.fill(pos[0].code)
  await page.waitForTimeout(900)
  await page.locator('.gsr-item', { hasText: pos[0].code }).first().click()
  await page.waitForTimeout(1200)
  expect(page.url()).toContain(`pos/${pos[0].id}`)
})

test('CF2-T6: uniform lifecycle on screen — segment value edit/delete, def deactivate/delete, entry-form inactivate', async ({ page, request }) => {
  // Stage a user segment with two values.
  const seg = await (await request.post(`${API}/api/segments`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Cost Pool ${STAMP}`, hasHierarchy: false, required: false } })).json()
  for (const l of ['Pool One', 'Pool Two'])
    await request.post(`${API}/api/segments/${seg.id}/values`, { headers: { ...ADMIN, ...JSON_H },
      data: { label: l, parentValueId: null, sort: 0 } })

  await goAs(page, 'u_admin', 'segments')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Cost Pool ${STAMP}.*value`) }).click()   // the rail item (its name carries the hint)

  // EDIT a value on screen (the verb that didn't exist).
  await page.getByRole('button', { name: 'Edit value Pool One' }).click()
  // CF-FIX4-T6: the staged add panel also has a 'Label' input — scope to the dialog.
  await page.getByRole('dialog').getByLabel('Label').fill('Pool One Renamed')
  await page.getByRole('button', { name: 'Save value', exact: true }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('Pool One Renamed')).toBeVisible()
  await expect(page.getByText('POOL-ONE', { exact: true })).toBeVisible()   // the CODE never re-keys

  // DELETE an unused value on screen.
  await page.getByRole('button', { name: 'Delete value Pool Two' }).click()
  // CF-FIX4-T6: the delete verb opens the impact dialog — unused → Delete is offered.
  await expect(page.getByText(/can be deleted safely/)).toBeVisible()
  await page.getByRole('button', { name: 'Delete segment value' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('POOL-TWO')).toHaveCount(0)

  // DEACTIVATE then DELETE the def on screen (clean — cascades).
  await page.getByRole('button', { name: 'Deactivate segment' }).click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Reactivate segment' }).click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Delete segment' }).click()
  // CF-FIX4-T6: def delete goes through the impact dialog too — clean → Delete offered.
  await expect(page.getByText(/can be deleted safely/)).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: 'Delete segment', exact: true }).click()   // the header verb shares the name
  await page.waitForTimeout(800)
  await expect(page.getByRole('button', { name: new RegExp(`Cost Pool ${STAMP}.*value`) })).toHaveCount(0)

  // ENTRY-FORM inactivate (the missing verb): copy Standard, deactivate, verify via API, delete.
  const forms = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  const std = forms.find((f: { isSystem: boolean }) => f.isSystem)
  const copy = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Lifecycle Form ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: `Open Lifecycle Form ${STAMP}`, exact: true }).click()
  await page.getByRole('button', { name: 'Deactivate form' }).click()
  await page.waitForTimeout(800)
  const after = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  expect(after.find((f: { id: string }) => f.id === copy.id).active).toBe(false)
  await expect(page.getByRole('button', { name: 'Reactivate form' })).toBeVisible()
  expect((await request.delete(`${API}/api/entry-forms/${copy.id}`, { headers: ADMIN })).status()).toBe(204)
})

// ── CF3 — Dashboard flexibility ──────────────────────────────────────────────
// All five tests drive u_lim's dashboard (personalize is copy-on-write) and
// reset it via the API afterwards so nothing leaks between tests or personas.

const LIM = { 'X-Demo-User': 'u_lim' }
const resetDash = (request: import('@playwright/test').APIRequestContext) =>
  request.delete(`${API}/api/dashboards/mine`, { headers: LIM })

/** Titles of the rendered portlet cards, in DOM (row/col) order. */
const portletTitles = (page: import('@playwright/test').Page) =>
  page.locator('section.card[aria-label]').evaluateAll((els) =>
    els.map((e) => e.getAttribute('aria-label')))

test('CF3-T7: drag a portlet onto another in Arrange — they swap, and the order survives reload', async ({ page, request }) => {
  await resetDash(request)
  await goAs(page, 'u_lim', 'dashboard')
  await page.getByRole('button', { name: 'Personalize dashboard' }).click()
  await page.getByRole('button', { name: 'Arrange portlets' }).click()

  const before = await portletTitles(page)
  expect(before.length).toBeGreaterThan(1)
  const [a, b] = [before[0]!, before[1]!]

  // HTML5 DnD via dispatched events sharing ONE DataTransfer (the Playwright-documented path).
  const dataTransfer = await page.evaluateHandle(() => new DataTransfer())
  const src = page.locator(`section.card[aria-label="${a}"]`)
  const dst = page.locator(`section.card[aria-label="${b}"]`)
  await src.dispatchEvent('dragstart', { dataTransfer })
  await dst.dispatchEvent('dragover', { dataTransfer })
  await dst.dispatchEvent('drop', { dataTransfer })
  await page.waitForTimeout(400)

  expect((await portletTitles(page)).slice(0, 2)).toEqual([b, a])   // swapped in the draft
  await page.getByRole('button', { name: 'Arrange portlets' }).click()   // Done = save
  await page.waitForTimeout(800)
  await page.reload({ waitUntil: 'networkidle' })
  expect((await portletTitles(page)).slice(0, 2)).toEqual([b, a])   // PERSISTED
  await resetDash(request)
})

test('CF3-T8: remove a portlet in Arrange — gone after reload; Reset brings the role default back', async ({ page, request }) => {
  await resetDash(request)
  await goAs(page, 'u_lim', 'dashboard')
  await page.getByRole('button', { name: 'Personalize dashboard' }).click()
  await page.getByRole('button', { name: 'Arrange portlets' }).click()

  const victim = (await portletTitles(page))[0]!
  await page.getByRole('button', { name: `Remove ${victim}` }).click()
  await page.getByRole('button', { name: 'Arrange portlets' }).click()   // Done = save
  await page.waitForTimeout(800)
  await page.reload({ waitUntil: 'networkidle' })
  expect(await portletTitles(page)).not.toContain(victim)

  await page.getByRole('button', { name: 'Reset to role default' }).click()
  await page.waitForTimeout(1000)
  expect(await portletTitles(page)).toContain(victim)   // reset restores the role default
})

test('CF3-T9: the Add-portlet bucket — SavedViewList and RecentRecords added on screen, KpiMeter routes to the KPI modal, all persist', async ({ page, request }) => {
  await resetDash(request)
  await goAs(page, 'u_lim', 'dashboard')

  // SavedViewList bound to a CF3-T11 seeded example view.
  await page.getByRole('button', { name: 'Add portlet' }).click()
  await pickSearch(page, 'Portlet type', 'Saved-view list')
  await pickSearch(page, 'Record type', 'Requisition')
  await pickSearch(page, 'Saved view', 'PRs pending approval')
  await page.getByRole('button', { name: 'Add portlet' }).last().click()
  await expect(page.locator('section.card[aria-label="PRs pending approval"]')).toBeVisible()

  // RecentRecords — the zero-config type.
  await page.getByRole('button', { name: 'Add portlet' }).first().click()
  await pickSearch(page, 'Portlet type', 'Recent records')
  await page.getByLabel('Title (optional)', { exact: true }).fill(`Recent ${STAMP}`)
  await page.getByRole('button', { name: 'Add portlet' }).last().click()
  await expect(page.locator(`section.card[aria-label="Recent ${STAMP}"]`)).toBeVisible()

  // KpiMeter routes to the existing richer modal (Continue…).
  await page.getByRole('button', { name: 'Add portlet' }).first().click()
  await page.getByRole('button', { name: 'Continue…' }).click()
  await expect(page.getByText('Add KPI from a saved view')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()

  await page.reload({ waitUntil: 'networkidle' })
  const titles = await portletTitles(page)
  expect(titles).toContain('PRs pending approval')
  expect(titles).toContain(`Recent ${STAMP}`)
  await resetDash(request)
})

test('CF3-T10: shortcut tiles are authorable — first tile with colour+target via the bucket, second via Add tile; colour renders; click navigates', async ({ page, request }) => {
  await resetDash(request)
  await goAs(page, 'u_lim', 'dashboard')

  await page.getByRole('button', { name: 'Add portlet' }).click()
  await pickSearch(page, 'Portlet type', 'Shortcuts')
  await page.getByLabel('Title (optional)', { exact: true }).fill(`Tiles ${STAMP}`)
  await page.getByLabel('First tile label', { exact: true }).fill(`Go Views ${STAMP}`)
  await pickSearch(page, 'Tile target page', 'Saved Views')
  await pickSearch(page, 'Tile colour', 'Teal')
  await page.getByRole('button', { name: 'Add portlet' }).last().click()

  const tile = page.getByRole('button', { name: `Go Views ${STAMP}` })
  await expect(tile).toBeVisible()
  await expect(tile).toHaveCSS('background-color', 'rgb(51, 99, 116)')   // Teal #336374 rendered

  // Second tile through MY portlet's own Add tile (the role default has its own Shortcuts card).
  await page.locator(`section.card[aria-label="Tiles ${STAMP}"]`).getByRole('button', { name: 'Add tile' }).click()
  const dlg = page.getByRole('dialog')
  await dlg.getByLabel('Tile label', { exact: true }).fill(`Go POs ${STAMP}`)
  await dlg.getByLabel('Target page', { exact: true }).selectOption({ label: 'Purchase Orders' })
  await dlg.getByLabel('Colour', { exact: true }).selectOption({ label: 'Plum' })
  await dlg.getByRole('button', { name: 'Add tile' }).click()
  const tile2 = page.getByRole('button', { name: `Go POs ${STAMP}` })
  await expect(tile2).toBeVisible()
  await expect(tile2).toHaveCSS('background-color', 'rgb(122, 46, 69)')  // Plum #7A2E45

  await tile.click()   // the tile NAVIGATES
  await expect(page.getByRole('heading', { name: 'Saved Views' })).toBeVisible()
  await resetDash(request)
})

test('CF3-T11: reminder/KPI pickers are populated by seeded example views; an empty picker offers the create-view loop', async ({ page, request }) => {
  await resetDash(request)
  await goAs(page, 'u_lim', 'dashboard')

  // The seeded example views populate the picker (the operator's "reminders don't work" fix).
  await page.getByRole('button', { name: 'Add reminder' }).click()
  await pickSearch(page, 'Saved view', 'PRs pending approval')
  await page.getByRole('button', { name: 'Add reminder' }).last().click()
  await expect(page.getByText('PRs pending approval')).toBeVisible()   // the reminder row landed

  // Create a view ON SCREEN and bind it to a KPI — the full create→bind loop.
  await goAs(page, 'u_lim', 'views')
  await page.getByRole('button', { name: /New view/ }).click()
  await page.getByLabel('Record type', { exact: true }).last().selectOption('Requisition')
  await page.getByRole('button', { name: 'Choose fields…' }).click()
  await page.getByLabel('View name', { exact: true }).fill(`CF View ${STAMP}`)
  await page.getByRole('button', { name: 'Add criterion' }).click()
  await page.getByLabel('Field', { exact: true }).selectOption('HeaderStatus')
  await page.getByLabel('Operator', { exact: true }).selectOption('Eq')
  await page.getByLabel('PR status', { exact: true }).selectOption('Submitted')
  await page.getByRole('button', { name: 'Save view' }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByText(`CF View ${STAMP}`)).toBeVisible()

  await goAs(page, 'u_lim', 'dashboard')
  await page.getByRole('button', { name: 'Add KPI', exact: true }).click()
  await page.getByLabel('KPI title', { exact: true }).fill(`CF KPI ${STAMP}`)
  await pickSearch(page, 'Record type', 'Requisition')
  await pickSearch(page, 'Saved view', `CF View ${STAMP}`)
  await page.getByRole('button', { name: 'Add KPI' }).last().click()
  await expect(page.locator(`section.card[aria-label="CF KPI ${STAMP}"]`)).toBeVisible()   // bound KPI renders

  // An EMPTY picker (a record type with no views) offers "create one in Saved Views".
  const types = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']
  let emptyType: string | null = null
  for (const t of types) {
    const vs = await (await request.get(`${API}/api/views?recordType=${t}`, { headers: LIM })).json()
    if (vs.length === 0) { emptyType = t; break }
  }
  if (emptyType) {
    await page.getByRole('button', { name: 'Add reminder' }).first().click()
    await pickSearch(page, 'Record type', emptyType === 'Rfq' ? 'Request For Quote' : emptyType)
    await page.getByRole('button', { name: 'create one in Saved Views' }).click()
    await expect(page.getByRole('heading', { name: 'Saved Views' })).toBeVisible()   // the loop lands on view authoring
  }
  // cleanup: the run-stamped view goes; the dashboard resets.
  const mine = await (await request.get(`${API}/api/views?recordType=Requisition`, { headers: LIM })).json()
  const stamped = mine.find((v: { name: string }) => v.name === `CF View ${STAMP}`)
  if (stamped) expect((await request.delete(`${API}/api/views/${stamped.id}`, { headers: LIM })).ok()).toBeTruthy()
  await resetDash(request)
})

// ── CF4 — Custom-field authoring parity ──────────────────────────────────────

test('CF4-T12: field authoring — display=Inline renders as text, show-in-list surfaces a system-view column (insert-before removed by CF-FIX1-T2)', async ({ page, request }) => {
  const ANCHOR = `CF4 Anchor ${STAMP}`
  const STAR = `CF4 Star ${STAMP}`

  // Author BOTH fields on screen through the def modal (the CF4 authoring surface).
  await goAs(page, 'u_admin', 'customfields')
  await page.getByRole('button', { name: /Purchase Order \d+ field/ }).click()   // rail item (full name since CF-FIX1-T1)
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill(ANCHOR)
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(600)

  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill(STAR)
  await page.getByLabel('Show On Default List', { exact: true }).check()
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(600)

  // Value while display=Normal (Inline fields are not user-writable — by design).
  const pos = await (await request.get(`${API}/api/pos`, { headers: BUYER })).json()
  const po = pos[0]
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const star = defs.find((d: { label: string }) => d.label === STAR)
  const anchor = defs.find((d: { label: string }) => d.label === ANCHOR)
  expect(anchor, 'both fields authored').toBeTruthy()   // (insert-before ordering removed by CF-FIX1-T2)
  await request.put(`${API}/api/custom-values/PurchaseOrder/${po.id}`, {
    headers: { ...BUYER, ...JSON_H },
    data: { values: { ...(await requiredCustomValues(request, 'PurchaseOrder', po.id)), [star.code]: `starval${STAMP}` } } })

  // Flip Star to Inline ON SCREEN via Edit (display type is def-mutable, unlike code/type).
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('row', { name: new RegExp(STAR) }).getByRole('button', { name: 'Edit' }).click()
  await pickSearch(page, 'Display type', 'Inline')   // searchable since CF-FIX2-T2
  await page.getByRole('button', { name: 'Save changes' }).click()
  await page.waitForTimeout(600)

  // Record surface: Star renders INLINE (plain text, no input) and IN POSITION (before Anchor).
  await goAs(page, 'u_faridah', `pos/${po.id}`)
  await page.waitForTimeout(1500)
  const section = page.locator('[aria-label="Custom fields"]')
  await expect(section.locator(`p[aria-label="${STAR}"]`)).toHaveText(`starval${STAMP}`)   // inline TEXT
  await expect(section.locator(`input[aria-label="${STAR}"]`)).toHaveCount(0)              // no input
  await expect(section.getByLabel(ANCHOR)).toBeVisible()                                   // Anchor still a field
  const labels = await section.locator('label').allTextContents()
  expect(labels.some((l) => l.includes(STAR)) && labels.some((l) => l.includes(ANCHOR))).toBe(true)   // ordering leg removed with insert-before (CF-FIX1-T2)

  // The server (not just the UI) refuses edits to an Inline field.
  const tamper = await request.put(`${API}/api/custom-values/PurchaseOrder/${po.id}`, {
    headers: { ...BUYER, ...JSON_H }, data: { values: { [star.code]: 'tamper' } } })
  expect(tamper.status()).toBe(400)

  // List column: a SavedViewList portlet bound to the SYSTEM PO view shows the Star column.
  await request.delete(`${API}/api/dashboards/mine`, { headers: LIM })
  await goAs(page, 'u_lim', 'dashboard')
  await page.getByRole('button', { name: 'Add portlet' }).click()
  await pickSearch(page, 'Portlet type', 'Saved-view list')
  await pickSearch(page, 'Record type', 'Purchase Order')
  await pickSearch(page, 'Saved view', 'All Purchase Orders')
  await page.getByRole('button', { name: 'Add portlet' }).last().click()
  await page.waitForTimeout(1000)
  const portlet = page.locator('section.card[aria-label="All Purchase Orders"]')
  await expect(portlet.locator('th', { hasText: STAR })).toBeVisible()          // the flagged column
  await expect(portlet.locator('td', { hasText: `starval${STAMP}` })).toBeVisible()   // with its value
  await request.delete(`${API}/api/dashboards/mine`, { headers: LIM })

  // Cleanup: back to Normal, clear the value, delete both defs (zero-value rule).
  await request.put(`${API}/api/custom-fields/${star.id}`, { headers: { ...ADMIN, ...JSON_H },
    data: { label: STAR, recordType: 'PurchaseOrder', dataType: 'Text', customListId: null, required: false, helpText: '', sort: star.sort, displayType: 'Normal', showInList: false } })
  await request.put(`${API}/api/custom-values/PurchaseOrder/${po.id}`, {
    headers: { ...BUYER, ...JSON_H }, data: { values: { [star.code]: null } } })
  await hardDeleteField(request, star)     // CF-FIX4-T4: UI-created fields are placed — unplace, then delete
  await hardDeleteField(request, anchor)
})

// ── CF5 — Entry-form layout editor ───────────────────────────────────────────

/** A run-stamped user form (copy of Standard fields) assigned to Buyer, via API. */
async function makeCf5Form(request: import('@playwright/test').APIRequestContext, name: string) {
  const forms = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  const std = forms.find((f: { isSystem: boolean }) => f.isSystem)
  const form = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name, recordType: 'Requisition', fields: std.fields } })).json()
  await request.put(`${API}/api/entry-forms/${form.id}/roles`, { headers: { ...ADMIN, ...JSON_H }, data: { roles: ['Buyer'] } })
  return form
}

const openForm = async (page: import('@playwright/test').Page, name: string) => {
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  // CF-FIX5-T6: Entry Forms is a LIST — Open the row into the full-page builder.
  await page.getByRole('button', { name: `Open ${name}`, exact: true }).click()
}

/** Buyer opens the first PR — the resolved role form renders there. */
const openPrAsBuyer = async (page: import('@playwright/test').Page, request: import('@playwright/test').APIRequestContext) => {
  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  await goAs(page, 'u_faridah', `reqs/open/${prs[0].id}`)
  await page.waitForTimeout(1500)
}

test('CF5-T2: subtabs are OBJECTS — create empty, drop a field in, hide it; the buyer form follows', async ({ page, request }) => {
  const FORM = `CF5 Tabs ${STAMP}`
  const TAB = `Extra ${STAMP}`
  const form = await makeCf5Form(request, FORM)
  await openForm(page, FORM)

  // Create the subtab as an EMPTY object (impossible when subtabs were just strings).
  await page.getByLabel('New subtab name', { exact: true }).fill(TAB)
  await page.getByRole('button', { name: 'Add subtab' }).click()
  await expect(page.getByLabel(`Subtab ${TAB}`, { exact: true })).toBeVisible()

  // Move Category into the subtab (CF-FIX5-T3 removed drag; whole-form save moves it).
  await moveFieldViaForm(request, 'Requisition', form.id, 'Category', { subtab: TAB })
  await page.waitForTimeout(400)

  // The buyer's PR form gains the tab; Category lives behind it.
  await openPrAsBuyer(page, request)
  await page.getByRole('button', { name: TAB }).click()
  await expect(page.getByRole('textbox', { name: 'Category' })).toBeVisible()   // the FORM field (a Segments card also names Category)

  // Hide it → the tab disappears from the buyer form (fields excluded server-side).
  // CF-FIX4-T3: the hide verb lives under the ACTIVE tab.
  await openForm(page, FORM)
  await page.getByLabel(`Subtab ${TAB}`, { exact: true }).click()
  await page.getByRole('button', { name: `Hide subtab ${TAB}` }).click()
  await page.waitForTimeout(600)
  await openPrAsBuyer(page, request)
  await expect(page.getByRole('button', { name: TAB })).toHaveCount(0)

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF5-T3: column break on a field group — the buyer form renders TWO columns', async ({ page, request }) => {
  const FORM = `CF5 Cols ${STAMP}`
  const GROUP = `Extras ${STAMP}`
  const form = await makeCf5Form(request, FORM)
  await openForm(page, FORM)

  // CF-FIX4-T3: create the group on screen, then DRAG Job + Memo into it (the placement
  // object moves — no more free-text group cells).
  await page.getByLabel('New group title', { exact: true }).fill(GROUP)
  await page.getByRole('button', { name: 'Add group' }).click()
  await page.waitForTimeout(600)
  for (const key of ['Job', 'Memo']) await moveFieldViaForm(request, 'Requisition', form.id, key, { group: GROUP })
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: `Open ${FORM}`, exact: true }).click()
  await page.waitForTimeout(600)

  // Flip the new group's column break (controlled checkbox: click, then the refetch confirms).
  await page.getByLabel(`Column break at ${GROUP}`, { exact: true }).click()
  await page.waitForTimeout(1000)
  await expect(page.getByLabel(`Column break at ${GROUP}`, { exact: true })).toBeChecked()

  // The buyer form renders the two-column canvas with both section titles.
  await openPrAsBuyer(page, request)
  const cols = page.locator('[data-cols="2"]')
  await expect(cols).toBeVisible()
  await expect(cols.getByText('Header', { exact: true })).toBeVisible()
  await expect(cols.getByText(GROUP, { exact: true })).toBeVisible()

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF5-T4: drag a field between containers — placement persists across reload and the buyer form follows', async ({ page, request }) => {
  const FORM = `CF5 Drag ${STAMP}`
  const TAB = `Moved ${STAMP}`
  const form = await makeCf5Form(request, FORM)
  await openForm(page, FORM)
  await page.getByLabel('New subtab name', { exact: true }).fill(TAB)
  await page.getByRole('button', { name: 'Add subtab' }).click()

  await moveFieldViaForm(request, 'Requisition', form.id, 'Job', { subtab: TAB })

  // PERSISTED: reload the designer — Job renders inside the subtab's container.
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: `Open ${FORM}`, exact: true }).click()
  await page.getByLabel(`Subtab ${TAB}`, { exact: true }).click()
  await expect(page.locator('[aria-label="Field row Job"]')).toBeVisible()

  // And back: move Job to Body — the subtab empties but SURVIVES as an object.
  await moveFieldViaForm(request, 'Requisition', form.id, 'Job', { subtab: null })
  await page.reload({ waitUntil: 'networkidle' })
  await page.getByRole('button', { name: `Open ${FORM}`, exact: true }).click()
  await expect(page.getByLabel(`Subtab ${TAB}`, { exact: true })).toBeVisible()   // empty subtab object persists

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

test('CF5-T5: guards — required-on-hidden warns (allow), populated containers refuse deletion', async ({ page, request }) => {
  const FORM = `CF5 Guard ${STAMP}`
  const TAB = `Req ${STAMP}`
  const form = await makeCf5Form(request, FORM)
  // Place a REQUIRED Department on the subtab via API (the composer path is proven in 08).
  const fields = form.fields.map((f: { fieldKey: string }) =>
    f.fieldKey === 'Department' ? { ...f, subtab: TAB, requiredOnForm: true } : f)
  await request.put(`${API}/api/entry-forms/${form.id}`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: FORM, recordType: 'Requisition', fields } })

  await openForm(page, FORM)

  // Hiding warns about the required field (warn-but-allow, ruled D2) — dismiss = no hide.
  // CF-FIX4-T3: activate the tab to reach its verbs.
  await page.getByLabel(`Subtab ${TAB}`, { exact: true }).click()
  let warned = ''
  page.once('dialog', (d) => { warned = d.message(); void d.dismiss() })
  await page.getByRole('button', { name: `Hide subtab ${TAB}` }).click()
  await page.waitForTimeout(400)
  expect(warned).toContain('required')
  expect(warned).toContain('Department')

  // A subtab with groups refuses deletion — the guard surfaces on screen.
  await page.getByRole('button', { name: `Delete subtab ${TAB}` }).click()
  await expect(page.getByText(/still holds field groups/)).toBeVisible()

  expect((await request.delete(`${API}/api/entry-forms/${form.id}`, { headers: ADMIN })).status()).toBe(204)
})

// ── CF6 — Custom LINE fields ─────────────────────────────────────────────────

test('CF6: line field end-to-end — admin authors a Line-scope field on screen, buyer enters a per-line value on a draft PR, it persists', async ({ page, request }) => {
  const LABEL = `Batch Ref ${STAMP}`
  // Self-healing (the 08 pattern): a mid-test failure strands run-stamped line defs whose
  // columns then crowd every later run's lines table — sweep them first.
  const stale = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  for (const d of stale.filter((x: { label: string }) => /^Batch Ref \d+$/.test(x.label))) {
    await request.post(`${API}/api/custom-fields/${d.id}/active`, { headers: { ...ADMIN, ...JSON_H }, data: 'false' })
    await request.delete(`${API}/api/custom-fields/${d.id}`, { headers: ADMIN })
  }

  // Admin authors the LINE field through the def modal (Scope select).
  await goAs(page, 'u_admin', 'customfields')
  await page.getByRole('button', { name: 'Requisition', exact: true }).click()   // the rail item (not the Requisitions nav)
  await page.getByRole('button', { name: 'New field' }).click()
  await page.getByLabel('Label', { exact: true }).fill(LABEL)
  await pickSearch(page, 'Scope (header field or line column)', 'Line')
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(600)
  await expect(page.getByRole('row', { name: new RegExp(LABEL) }).getByText('line', { exact: true })).toBeVisible()

  // Buyer opens a DRAFT PR — the new column renders on the lines table; enter a value on line 1.
  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const draft = prs.find((r: { headerStatus: string; lines?: unknown[] }) => r.headerStatus === 'Draft' && (r.lines?.length ?? 0) > 0)
  expect(draft, 'a seeded draft PR with lines').toBeTruthy()
  await goAs(page, 'u_faridah', `reqs/open/${draft.id}`)
  await page.waitForTimeout(1500)
  await expect(page.locator('th', { hasText: LABEL })).toBeVisible()
  await page.getByLabel(`${LABEL} line 1`, { exact: true }).fill(`LOT-${STAMP}`)
  // Required header defs (e.g. the operator's 'Partner') must be STORED before the PR
  // save's line-values PUT — they ride a different card/save on this page.
  const reqVals = await requiredCustomValues(request, 'Requisition', draft.id)
  if (Object.keys(reqVals).length > 0)
    await request.put(`${API}/api/custom-values/Requisition/${draft.id}`, { headers: { ...BUYER, ...JSON_H }, data: { values: reqVals } })
  await page.getByRole('button', { name: 'Save changes' }).first().click()
  // Saving navigates back to the list (leave()) — wait for THAT before re-opening, or the
  // late navigation yanks the reopened form back to the list.
  await expect(page.getByRole('button', { name: 'Build RFQ' })).toBeVisible()
  await page.waitForTimeout(400)

  // Reload — the per-line value persisted (LineId grain, not header). Fresh document:
  // a hash-only goto after leave() proved flaky (same-document navigation).
  await page.goto('about:blank')
  await goAs(page, 'u_faridah', `reqs/open/${draft.id}`)
  await page.waitForTimeout(1500)
  await expect(page.getByLabel(`${LABEL} line 1`, { exact: true })).toHaveValue(`LOT-${STAMP}`)
  await expect(page.getByLabel(`${LABEL} line 2`, { exact: true })).toHaveValue('')   // line grain — no bleed

  // The header custom-fields section does NOT show the line field.
  await expect(page.locator('[aria-label="Custom fields"]').getByText(LABEL)).toHaveCount(0)

  // Cleanup: clear the value, delete the def (zero-value hard delete).
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  const def = defs.find((d: { label: string }) => d.label === LABEL)
  const lineVals = await (await request.get(`${API}/api/custom-values/Requisition/${draft.id}/lines`, { headers: BUYER })).json()
  const lineId = Object.keys(lineVals)[0]
  await request.put(`${API}/api/custom-values/Requisition/${draft.id}`, { headers: { ...BUYER, ...JSON_H },
    data: { values: {}, lines: { [lineId]: { [def.code]: null } } } })
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
})

// ── CF7 — Saved View → Saved Search: richer criteria ─────────────────────────

test('CF7: builder speaks the new operators and grouped-OR — (Draft OR Submitted) matches the union on screen', async ({ page, request }) => {
  const NAME = `CF7 OrView ${STAMP}`
  // Expected union from the API (PR statuses Draft + Submitted).
  const prs = await (await request.get(`${API}/api/requisitions`, { headers: BUYER })).json()
  const expected = prs.filter((r: { headerStatus: string }) => ['Draft', 'Submitted'].includes(r.headerStatus)).length
  expect(expected).toBeGreaterThan(0)

  await goAs(page, 'u_faridah', 'views')
  await page.getByRole('button', { name: /New view/ }).click()
  await page.getByLabel('Record type', { exact: true }).last().selectOption('Requisition')
  await page.getByRole('button', { name: 'Choose fields…' }).click()
  await page.getByLabel('View name', { exact: true }).fill(NAME)
  await page.getByRole('button', { name: 'Add criterion' }).click()
  await page.getByLabel('Field', { exact: true }).selectOption('HeaderStatus')
  await page.getByLabel('Operator', { exact: true }).selectOption('Eq')
  await page.getByLabel('PR status', { exact: true }).selectOption('Draft')
  await page.getByRole('button', { name: 'Or with criterion 1' }).click()      // the CF7-T2 affordance
  await expect(page.getByText('or-group 1')).toHaveCount(2)                    // both rows carry the group
  await page.getByLabel('PR status', { exact: true }).nth(1).selectOption('Submitted')
  await page.getByRole('button', { name: 'Save view' }).click()
  await page.waitForTimeout(1000)

  // The run agrees with the union.
  const views = await (await request.get(`${API}/api/views?recordType=Requisition`, { headers: BUYER })).json()
  const mine = views.find((v: { name: string }) => v.name === NAME)
  expect(mine.filters.every((f: { groupIndex: number }) => f.groupIndex === 1)).toBe(true)
  const run = await (await request.get(`${API}/api/views/${mine.id}/run`, { headers: BUYER })).json()
  expect(run.total).toBe(expected)

  // A NEW operator drives on screen too: edit the view to IsNotEmpty on Department.
  await page.getByRole('button', { name: `Edit ${NAME}`, exact: true }).click()
  await page.getByRole('button', { name: 'Add criterion' }).click()
  await page.getByLabel('Field', { exact: true }).last().selectOption('Department')
  await page.getByLabel('Operator', { exact: true }).last().selectOption('IsNotEmpty')
  await page.getByRole('button', { name: 'Save changes' }).click()   // edit mode's save label
  await page.waitForTimeout(1000)
  const run2 = await (await request.get(`${API}/api/views/${mine.id}/run`, { headers: BUYER })).json()
  expect(run2.total).toBeGreaterThan(0)
  expect(run2.total).toBeLessThanOrEqual(run.total)

  expect((await request.delete(`${API}/api/views/${mine.id}`, { headers: BUYER })).ok()).toBeTruthy()
})

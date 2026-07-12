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
  await page.getByLabel('Label').fill('Pool One Renamed')
  await page.getByRole('button', { name: 'Save value' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('Pool One Renamed')).toBeVisible()
  await expect(page.getByText('POOL-ONE', { exact: true })).toBeVisible()   // the CODE never re-keys

  // DELETE an unused value on screen.
  await page.getByRole('button', { name: 'Delete value Pool Two' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByText('POOL-TWO')).toHaveCount(0)

  // DEACTIVATE then DELETE the def on screen (clean — cascades).
  await page.getByRole('button', { name: 'Deactivate segment' }).click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Reactivate segment' }).click()
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Delete segment' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByRole('button', { name: new RegExp(`Cost Pool ${STAMP}.*value`) })).toHaveCount(0)

  // ENTRY-FORM inactivate (the missing verb): copy Standard, deactivate, verify via API, delete.
  const forms = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  const std = forms.find((f: { isSystem: boolean }) => f.isSystem)
  const copy = await (await request.post(`${API}/api/entry-forms`, { headers: { ...ADMIN, ...JSON_H },
    data: { name: `Lifecycle Form ${STAMP}`, recordType: 'Requisition', fields: std.fields } })).json()
  await goAs(page, 'u_admin', 'entryforms')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: new RegExp(`Lifecycle Form ${STAMP}`) }).first().click()
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
  await page.getByLabel('Portlet type', { exact: true }).selectOption({ label: 'Saved-view list (top-N rows)' })
  await page.getByLabel('Record type', { exact: true }).selectOption('Requisition')
  await page.getByLabel('Saved view', { exact: true }).selectOption({ label: 'PRs pending approval' })
  await page.getByRole('button', { name: 'Add portlet' }).last().click()
  await expect(page.locator('section.card[aria-label="PRs pending approval"]')).toBeVisible()

  // RecentRecords — the zero-config type.
  await page.getByRole('button', { name: 'Add portlet' }).first().click()
  await page.getByLabel('Portlet type', { exact: true }).selectOption({ label: 'Recent records' })
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
  await page.getByLabel('Portlet type', { exact: true }).selectOption({ label: 'Shortcuts (tiles)' })
  await page.getByLabel('Title (optional)', { exact: true }).fill(`Tiles ${STAMP}`)
  await page.getByLabel('First tile label', { exact: true }).fill(`Go Views ${STAMP}`)
  await page.getByLabel('Tile target page', { exact: true }).selectOption({ label: 'Saved Views' })
  await page.getByLabel('Tile colour', { exact: true }).selectOption({ label: 'Teal' })
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
  await page.getByLabel('Saved view', { exact: true }).selectOption({ label: 'PRs pending approval' })
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
  await page.getByLabel('Record type', { exact: true }).selectOption('Requisition')
  await page.getByLabel('Saved view', { exact: true }).selectOption({ label: `CF View ${STAMP}` })
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
    await page.getByLabel('Record type', { exact: true }).selectOption(emptyType)
    await page.getByRole('button', { name: 'create one in Saved Views' }).click()
    await expect(page.getByRole('heading', { name: 'Saved Views' })).toBeVisible()   // the loop lands on view authoring
  }
  // cleanup: the run-stamped view goes; the dashboard resets.
  const mine = await (await request.get(`${API}/api/views?recordType=Requisition`, { headers: LIM })).json()
  const stamped = mine.find((v: { name: string }) => v.name === `CF View ${STAMP}`)
  if (stamped) expect((await request.delete(`${API}/api/views/${stamped.id}`, { headers: LIM })).ok()).toBeTruthy()
  await resetDash(request)
})

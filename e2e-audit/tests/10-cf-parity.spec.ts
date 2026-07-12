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

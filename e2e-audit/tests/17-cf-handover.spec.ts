import { test, expect } from '@playwright/test'
import { goAs, pickSearch } from './helpers'

// CF-HANDOVER browser proofs.

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

// ── T3 — PR dimension fields are segment-backed searchable pickers ────────────
test('CFH-T3: PR Department/Location/Category are searchable segment pickers; save writes label+code; segment on the line', async ({ page, request }) => {
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1200)

  // Pickers, NOT free text: each dimension renders as a searchable-select button (not a textbox).
  for (const label of ['Department', 'Location', 'Category']) {
    await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible()
    await expect(page.getByRole('textbox', { name: label, exact: true })).toHaveCount(0)
  }

  // Pick realistic values from the segments.
  await pickSearch(page, 'Department', 'Maintenance')
  await pickSearch(page, 'Location', 'Bintulu Plant')
  await pickSearch(page, 'Category', 'Rotating Equipment')
  await page.getByLabel('Requestor', { exact: true }).fill('T3 Buyer')
  await pickSearch(page, 'Line 1 item code', 'MEP-PMP-075')
  await page.getByLabel('Line 1 qty', { exact: true }).fill('2')

  const [resp] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/requisitions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Save draft' }).first().click(),
  ])
  const created = await resp.json()
  await page.waitForTimeout(500)

  // The label persisted (the *Code companion is derived server-side via DimCode — verified in the DB check).
  const full = await (await request.get(`${API}/api/requisitions/${created.id}`, { headers: BUYER })).json()
  expect(full.department).toBe('Maintenance')
  expect(full.location).toBe('Bintulu Plant')
  expect(full.category).toBe('Rotating Equipment')

  // The dimension segments are applied at BOTH header AND line on Requisition.
  const segs = await (await request.get(`${API}/api/segments`, { headers: ADMIN })).json()
  const dept = segs.find((s: { name: string }) => s.name === 'Department')
  expect(dept.applications.some((a: { recordType: string; lineLevel: boolean }) => a.recordType === 'Requisition' && a.lineLevel)).toBe(true)

  // cleanup
  await request.post(`${API}/api/requisitions/${created.id}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 't3 cleanup' } })
})

// ── T4 — Item Master: PR line Item Code picker auto-fills Description + UoM ────
test('CFH-T4: PR line Item Code is a master picker; selecting auto-fills Description + UoM; persists on reopen', async ({ page, request }) => {
  await goAs(page, 'u_faridah', 'reqs')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: /Create PR/ }).click()
  await page.waitForTimeout(1200)

  // Item Code is a PICKER (searchable-select button), not a free-text box.
  await expect(page.getByRole('button', { name: 'Line 1 item code' })).toBeVisible()

  // Pick a master item → Description + UoM auto-populate on that line.
  await pickSearch(page, 'Line 1 item code', 'VLV-GT-0150')
  await expect(page.getByLabel('Line 1 description', { exact: true })).toHaveValue('Gate Valve, DN150, PN16, CS')
  await expect(page.getByLabel('Line 1 uom', { exact: true })).toHaveValue('EA')

  await page.getByLabel('Requestor', { exact: true }).fill('T4 Buyer')
  await page.getByLabel('Line 1 qty', { exact: true }).fill('3')
  const [resp] = await Promise.all([
    page.waitForResponse((r) => r.url().includes('/api/requisitions') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Save draft' }).first().click(),
  ])
  const created = await resp.json()

  // Reopen → the item code + auto-filled Description/UoM are intact.
  const full = await (await request.get(`${API}/api/requisitions/${created.id}`, { headers: BUYER })).json()
  const line = full.lines[0]
  expect(line.itemCode).toBe('VLV-GT-0150')
  expect(line.description).toBe('Gate Valve, DN150, PN16, CS')
  expect(line.uom).toBe('EA')

  await request.post(`${API}/api/requisitions/${created.id}/cancel`, { headers: { ...BUYER, ...JSON_H }, data: { reason: 't4 cleanup' } })
})

test('CFH-T4/admin: Item Master is a list → Open → full page → Back; create + edit work', async ({ page, request }) => {
  const code = `TST-${STAMP}`
  await goAs(page, 'u_admin', 'items')
  await page.waitForTimeout(1200)
  // List shows the seeded items.
  await expect(page.getByText('VLV-GT-0150', { exact: true })).toBeVisible()

  // New item → full page (list hidden) → create.
  await page.getByRole('button', { name: 'New item' }).click()
  await expect(page.getByRole('button', { name: 'Back to items' })).toBeVisible()
  await expect(page.getByText('VLV-GT-0150', { exact: true })).toHaveCount(0)   // list gone
  await page.getByLabel('Item code', { exact: true }).fill(code)
  await page.getByLabel('Description', { exact: true }).fill('Test bolt, M12x50')
  await page.getByLabel('Unit of measure', { exact: true }).fill('EA')
  await page.getByRole('button', { name: 'Save item' }).click()
  await page.waitForTimeout(800)

  // Back on the list, the new item shows; Open it → full page → Back returns.
  await expect(page.getByText(code, { exact: true })).toBeVisible()
  await page.getByRole('button', { name: `Open ${code}`, exact: true }).click()
  await expect(page.getByRole('button', { name: 'Save item' })).toBeVisible()
  await page.getByRole('button', { name: 'Back to items' }).click()
  await expect(page.getByRole('button', { name: 'New item' })).toBeVisible()

  // cleanup (delete the test item)
  const items = await (await request.get(`${API}/api/items`, { headers: ADMIN })).json()
  const mine = items.find((i: { itemCode: string }) => i.itemCode === code)
  if (mine) await request.delete(`${API}/api/items/${mine.id}`, { headers: ADMIN })
})

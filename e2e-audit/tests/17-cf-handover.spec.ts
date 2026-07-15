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
  await page.getByLabel('Line 1 item code', { exact: true }).fill(`T3-${STAMP}`)
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

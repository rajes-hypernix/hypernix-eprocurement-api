import { test, expect } from '@playwright/test'
import { goAs } from './helpers'

// CF-FIX-1 browser proofs — one test per task, mirroring the operator's testing notes
// (Custom_Fields.docx / Custom_List.docx). Names match the CF-FIX1-Tn commits.

const API = 'http://localhost:5260'
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }
const STAMP = Date.now().toString().slice(-6)

test('CF-FIX1-T1: full record-type names, renamed options, no lifecycle prose', async ({ page }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)

  // Full professional names on the rail (display only — storage unchanged).
  await expect(page.getByRole('button', { name: 'Request For Quote' })).toBeVisible()
  await expect(page.getByRole('button', { name: /Purchase Order \d+ field/ })).toBeVisible()   // the rail item (selected → carries its count; 'Purchase Orders' nav is separate)
  await expect(page.getByRole('button', { name: 'Advance Shipment Notice' })).toBeVisible()
  await expect(page.getByText('Rfq', { exact: true })).toHaveCount(0)

  // The subtitle and the lifecycle prose are gone.
  await expect(page.getByText('Fields your team defines')).toHaveCount(0)
  await expect(page.getByText('A field with values is never deleted')).toHaveCount(0)

  // The renamed options live in the modal.
  await page.getByRole('button', { name: 'New field' }).click()
  await expect(page.getByLabel('Show On Default List', { exact: true })).toBeVisible()
  await expect(page.getByLabel('Mandatory', { exact: true })).toBeVisible()
  await expect(page.getByText('Show in list (column on the default list view)')).toHaveCount(0)
  await expect(page.getByText('Required (at value-save only)')).toHaveCount(0)
})

test('CF-FIX1-T2: Insert Before is gone; creating a field still works and lands in the list', async ({ page, request }) => {
  await goAs(page, 'u_admin', 'customfields')
  await page.waitForTimeout(1200)
  await page.getByRole('button', { name: 'New field' }).click()
  await expect(page.getByText('Insert before')).toHaveCount(0)   // the control is removed
  await page.getByLabel('Label', { exact: true }).fill(`Fix1 Plain ${STAMP}`)
  await page.getByRole('button', { name: 'Create field' }).click()
  await page.waitForTimeout(800)
  await expect(page.getByRole('row', { name: new RegExp(`Fix1 Plain ${STAMP}`) })).toBeVisible()
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=PurchaseOrder`, { headers: ADMIN })).json()
  const def = defs.find((d: { label: string }) => d.label === `Fix1 Plain ${STAMP}`)
  expect((await request.delete(`${API}/api/custom-fields/${def.id}`, { headers: ADMIN })).status()).toBe(204)
})

import { test, expect } from '@playwright/test'
import { goAs, shot } from './helpers'

// D7.5 CLOSING GATE — the operator's full journey, ONE persona (the buyer), ONE unbroken
// run: Saved Views home → create "PRs pending approval" with criteria → Personalize: add
// it as a Reminder AND pin a KPI from it → the dashboard shows both LIVE → click the
// reminder → the list opens with the view selected (the id-intersection mount) → open a
// PR → the D7 role form renders with her custom field on its admin-defined subtab.
// Admin preconditions (the D7 machinery: a cf def + a Buyer role form placing it on a
// subtab) are staged via API; the JOURNEY itself is the buyer's unbroken UI run.
// Run-stamped + self-cleaning.

const API = 'http://localhost:5260'
const STAMP = Date.now().toString().slice(-6)
const VIEW_NAME = `PRs pending approval (${STAMP})`
const KPI_TITLE = `Pending PRs (${STAMP})`
const CF_LABEL = `Budget Ref ${STAMP}`
const CF_CODE = `cf_budget_ref_${STAMP}`
const FORM_NAME = `Buyer PR Form ${STAMP}`
const ADMIN = { 'X-Demo-User': 'u_admin' }
const BUYER = { 'X-Demo-User': 'u_faridah' }
const JSON_H = { 'Content-Type': 'application/json' }

test('the journey: home → view → reminder + KPI → dashboard live → filtered list → PR → role form', async ({ page, request }) => {
  test.setTimeout(180_000)
  page.on('dialog', (d) => void d.accept())

  // ---- Preconditions (admin, API): the D7 role form with a custom field on a subtab ----
  expect((await request.post(`${API}/api/custom-fields`, {
    headers: { ...ADMIN, ...JSON_H },
    data: { label: CF_LABEL, recordType: 'Requisition', dataType: 'Text', customListId: null, required: false, helpText: '', sort: 0 },
  })).ok()).toBeTruthy()
  const forms = await (await request.get(`${API}/api/entry-forms?recordType=Requisition`, { headers: ADMIN })).json()
  const standard = forms.find((f: { isSystem: boolean }) => f.isSystem)
  const roleForm = await (await request.post(`${API}/api/entry-forms`, {
    headers: { ...ADMIN, ...JSON_H },
    data: {
      name: FORM_NAME, recordType: 'Requisition',
      fields: [...standard.fields, {
        fieldKey: CF_CODE, subtab: 'Additional', fieldGroup: 'Extra', sort: 10, displayType: 'Normal',
        requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null,
      }],
    },
  })).json()
  expect((await request.put(`${API}/api/entry-forms/${roleForm.id}/roles`, {
    headers: { ...ADMIN, ...JSON_H }, data: { roles: ['Buyer'] },
  })).ok()).toBeTruthy()

  // ---- 1. The buyer opens Saved Views HOME and creates the view — no list screen involved ----
  await goAs(page, 'u_faridah', 'views')
  await page.waitForTimeout(1500)
  await shot(page, 'D75-journey-1-home')
  await page.getByRole('button', { name: /New view/ }).click()
  // Two 'Record type' controls exist here (the home's facet + the modal's select) — take the modal's.
  await page.getByLabel('Record type').last().selectOption('Requisition')
  await page.getByRole('button', { name: 'Choose fields…' }).click()
  await page.getByLabel('View name').fill(VIEW_NAME)
  await page.getByRole('button', { name: 'Add criterion' }).click()
  await page.getByLabel('Field').selectOption('HeaderStatus')
  await page.getByLabel('Operator').selectOption('Eq')
  await page.getByLabel('PR status', { exact: true }).selectOption('Submitted')
  await shot(page, 'D75-journey-2-view')
  await page.getByRole('button', { name: 'Save view' }).click()
  await page.waitForTimeout(1200)
  await expect(page.getByText(VIEW_NAME)).toBeVisible()             // it lands in the home list

  // ---- 2. Personalize: the SAME view becomes a Reminder AND a KPI ----
  await goAs(page, 'u_faridah', 'dashboard')
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'Add reminder' }).click()
  await page.getByLabel('Record type').selectOption('Requisition')
  await page.getByRole('option', { name: VIEW_NAME }).waitFor({ state: 'attached' })   // options in a closed select are never 'visible'
  await page.getByLabel('Saved view').selectOption({ label: VIEW_NAME })
  await page.getByRole('button', { name: 'Add reminder' }).last().click()
  await page.waitForTimeout(1200)

  await page.getByRole('button', { name: 'Add KPI' }).first().click()
  await page.getByLabel('KPI title').fill(KPI_TITLE)
  await page.getByLabel('Record type').selectOption('Requisition')
  await page.getByLabel('Saved view').selectOption({ label: VIEW_NAME })
  await page.getByLabel('Function').selectOption('count')
  await page.getByRole('button', { name: 'Add KPI' }).last().click()
  await page.waitForTimeout(1500)

  // ---- 3. The dashboard shows BOTH, live — and they agree with the API count ----
  const views = await (await request.get(`${API}/api/views?recordType=Requisition`, { headers: BUYER })).json()
  const view = views.find((v: { name: string }) => v.name === VIEW_NAME)
  const agg = await (await request.get(`${API}/api/views/${view.id}/aggregate?fn=count`, { headers: BUYER })).json()
  const count = Number(agg.value)
  expect(count).toBeGreaterThan(0)
  const reminderBtn = page.getByRole('button', { name: new RegExp(`${VIEW_NAME.replace(/[()]/g, '\\$&')}.*${count}`) })
  await expect(reminderBtn).toBeVisible()                           // live count on the reminder
  const kpi = page.locator('section', { hasText: KPI_TITLE })
  await expect(kpi.getByText(String(count), { exact: true })).toBeVisible()   // the KPI agrees
  await shot(page, 'D75-journey-3-dashboard')

  // ---- 4. Click the reminder → the list opens WITH the view selected (id-intersection) ----
  await reminderBtn.click()
  await page.waitForTimeout(1800)
  await expect(page.getByRole('combobox', { name: 'Saved view' })).toHaveValue(view.id)
  const rows = page.locator('table tbody tr')
  await expect(rows).toHaveCount(count)                             // exactly the view's rows
  await shot(page, 'D75-journey-4-list')

  // ---- 5. Open a PR → the D7 role form: her custom field on its admin subtab ----
  await page.locator('table').getByRole('button', { name: 'Open', exact: true }).first().click()   // exact: 'Open' must not match the sidebar's 'Bid Openings'
  await page.waitForTimeout(1500)
  await page.getByRole('button', { name: 'Additional' }).click()    // the admin-defined subtab
  await expect(page.getByLabel(CF_LABEL)).toBeVisible()
  await shot(page, 'D75-journey-5-roleform')

  // ---- cleanup: dashboard reset, view deleted, role form deleted, cf def deleted ----
  expect((await request.delete(`${API}/api/dashboards/mine`, { headers: BUYER })).status()).toBe(204)
  expect((await request.delete(`${API}/api/views/${view.id}`, { headers: BUYER })).status()).toBe(204)
  expect((await request.delete(`${API}/api/entry-forms/${roleForm.id}`, { headers: ADMIN })).status()).toBe(204)
  const defs = await (await request.get(`${API}/api/custom-fields?recordType=Requisition`, { headers: ADMIN })).json()
  const cf = defs.find((d: { code: string }) => d.code === CF_CODE)
  expect((await request.delete(`${API}/api/custom-fields/${cf.id}`, { headers: ADMIN })).status()).toBe(204)
})

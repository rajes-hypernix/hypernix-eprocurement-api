import { test, expect } from '@playwright/test'
import { watch, goAs, shot, reportHealth } from './helpers'

// U1-U48 page-load health crawl: every route, both roles, capture console/network/slow + screenshot.
const BUYER = 'u_faridah'
const ADMIN = 'u_admin'
const APPROVER = 'u_lim'
const TECH = 'u_hafiz'
const VENDOR = 'VU-sentausa'

const buyerRoutes: [string, string][] = [
  ['dashboard', 'U4-dashboard-buyer'],
  ['views', 'U49-saved-views-home'],
  ['vendors', 'U5-vendor-master'],
  ['onboarding', 'U41-onboarding-queue'],
  ['onboarding/invite', 'U43-onboarding-invite'],
  ['reqs', 'U12-requisitions'],
  ['consolidate', 'U14-consolidate'],
  ['rfqs', 'U15-rfq-list'],
  ['forms', 'U19-forms'],
  ['openings', 'U20-bid-openings'],
  ['awards', 'U23-awards'],
  ['pos', 'U25-po-list'],
  ['deliveries', 'U27-deliveries'],
  ['invoices', 'U31-invoices'],
  ['statements', 'U34-statements'],
  ['chats', 'U36-clarifications'],
  ['payments', 'U47-payments-placeholder'],
]

// Admin screens crawl as the ADMIN persona (RM-P1 nav gating: a buyer no longer sees them —
// the buyer-persona steps relied on cross-role nav visibility, same species as the OD-9 fix).
const adminRoutes: [string, string][] = [
  ['admin', 'U10-admin-users'],
  ['lists', 'U11-admin-custom-lists'],
  ['customfields', 'U50-admin-custom-fields'],
  ['segments', 'U51-admin-segments'],
  ['entryforms', 'U52-admin-entry-forms'],
  ['numbering', 'U53-admin-numbering'],
]

const vendorRoutes: [string, string][] = [
  ['dashboard', 'U4-dashboard-vendor'],
  ['bids', 'U39-my-rfqs'],
  ['pos', 'U25-po-list-vendor'],
  ['deliveries', 'U27-deliveries-vendor'],
  ['invoices', 'U31-invoices-vendor'],
  ['statement', 'U35-vendor-statement'],
  ['chats', 'U36-clarifications-vendor'],
]


// A shipped screen must NEVER carry the unimplemented placeholder (the stale
// IMPLEMENTED_BASES list shipped Segments/Entry Forms/Numbering/Views home WITH
// "Coming in a later slice" appended — operator-caught; this pins every crawled page).
async function expectNoPlaceholder(page: import('@playwright/test').Page, route: string) {
  for (const text of ['Coming in a later slice', 'not implemented yet']) {
    expect(await page.getByText(text).count(), `"${text}" visible on ${route}`).toBe(0)
  }
}

for (const [route, name] of buyerRoutes) {
  test(`buyer load: ${route}`, async ({ page }, info) => {
    const w = watch(page)
    await goAs(page, BUYER, route)
    await page.waitForTimeout(1200)
    if (route !== 'payments') await expectNoPlaceholder(page, route)   // payments = the one DELIBERATE placeholder (PV deferred)
    await shot(page, name)
    const issues = reportHealth(w, info, name)
    // Hard-fail only on JS/page errors (a broken screen); net/slow are reported.
    expect(w.pageErrors, `page errors on ${route}: ${issues.join(' ; ')}`).toEqual([])
  })
}

for (const [route, name] of adminRoutes) {
  test(`admin load: ${route}`, async ({ page }, info) => {
    const w = watch(page)
    await goAs(page, ADMIN, route)
    await page.waitForTimeout(1200)
    if (route !== 'payments') await expectNoPlaceholder(page, route)   // payments = the one DELIBERATE placeholder (PV deferred)
    await shot(page, name)
    const issues = reportHealth(w, info, name)
    expect(w.pageErrors, `page errors on ${route}: ${issues.join(' ; ')}`).toEqual([])
  })
}

for (const [route, name] of vendorRoutes) {
  test(`vendor load: ${route}`, async ({ page }, info) => {
    const w = watch(page)
    await goAs(page, VENDOR, route)
    await page.waitForTimeout(1200)
    if (route !== 'payments') await expectNoPlaceholder(page, route)   // payments = the one DELIBERATE placeholder (PV deferred)
    await shot(page, name)
    const issues = reportHealth(w, info, name)
    expect(w.pageErrors, `page errors on ${route}: ${issues.join(' ; ')}`).toEqual([])
  })
}

test('admin + approver + tech dashboards load', async ({ page }, info) => {
  for (const [persona, label] of [[ADMIN, 'admin'], [APPROVER, 'approver'], [TECH, 'tech']] as const) {
    const w = watch(page)
    await goAs(page, persona, 'dashboard')
    await page.waitForTimeout(800)
    await shot(page, `U4-dashboard-${label}`)
    reportHealth(w, info, `dashboard-${label}`)
    expect(w.pageErrors).toEqual([])
  }
})

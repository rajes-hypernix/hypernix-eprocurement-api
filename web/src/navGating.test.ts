import { describe, it, expect } from 'vitest'
import { BUYER_NAV, gateNav } from './nav'

/**
 * RM-P1 pin: the sidebar derives from the SAME server permission list as <Gated> —
 * no second role map. Permission sets below mirror what /api/auth/permissions returns
 * per the ruled ActionCatalog (AUTHORIZATION-MATRIX §4).
 */

const BUYER_PERMISSIONS = [
  'UseDashboards', 'ViewRequisitions', 'ViewRfqs', 'ViewAwards', 'ViewPos', 'ViewAsns',
  'ViewInvoices', 'ViewStatements', 'ViewBidOpenings', 'ViewVendors', 'ViewOnboarding',
  'ViewForms', 'ViewClarifications', 'ViewUsers',
]
const ADMIN_PERMISSIONS = [...BUYER_PERMISSIONS, 'ManageUsers', 'ManageCustomLists']
const EVALUATOR_PERMISSIONS = ['UseDashboards', 'ViewRfqs', 'ViewBidOpenings', 'ViewClarifications', 'ViewUsers']

const keys = (groups: ReturnType<typeof gateNav>) => groups.flatMap((g) => g.items.map((i) => i.key))
const titles = (groups: ReturnType<typeof gateNav>) => groups.map((g) => g.title)

describe('gateNav derives the sidebar from the server permission list', () => {
  it('a buyer no longer sees User Management or Custom Lists; the group drops entirely', () => {
    const nav = gateNav(BUYER_NAV, BUYER_PERMISSIONS)
    expect(keys(nav)).not.toContain('admin')
    expect(keys(nav)).not.toContain('lists')
    expect(titles(nav)).not.toContain('Administration')
    // ViewUsers alone (the name directory) must NOT surface the management screen.
  })

  it('an Admin still sees the Administration group', () => {
    const nav = gateNav(BUYER_NAV, ADMIN_PERMISSIONS)
    expect(keys(nav)).toContain('admin')
    expect(keys(nav)).toContain('lists')
    expect(titles(nav)).toContain('Administration')
  })

  it('an evaluator sees exactly the evaluation surface', () => {
    const nav = gateNav(BUYER_NAV, EVALUATOR_PERMISSIONS)
    expect(keys(nav)).toEqual(['dashboard', 'rfqs', 'openings', 'chats'])
    expect(titles(nav)).toEqual(['Sourcing', 'Evaluation', 'Communication'])
  })

  it('ungated items (dev tooling) survive any permission set', () => {
    const nav = gateNav([{ title: 'Design (dev)', items: [{ key: 'design', icon: 'edit', label: 'Gallery' }] }], [])
    expect(keys(nav)).toEqual(['design'])
  })

  it('an empty permission list (still loading) renders no internal nav', () => {
    expect(gateNav(BUYER_NAV, [])).toEqual([])
  })
})

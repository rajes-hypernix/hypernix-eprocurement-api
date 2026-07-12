import type { NavGroup, NavItem } from './nav'

/**
 * Center-tab navigation configs (D2 Phase 2). The nav DATA is structured the
 * NetSuite way — one center tab per functional area, each carrying its items —
 * so D4 can later data-drive them (roles/personalisation tables). Per the
 * operator ruling, the RENDERER remains the existing sidebar: these configs
 * feed it through tabsToNavGroups, and navigation behaviour is identical (the
 * crawl proves it). A top center-tab SHELL is a deferred operator design
 * decision — see docs/BACKLOG.md.
 */

export interface CenterTab {
  key: string
  label: string
  items: NavItem[]
}

// Each item names the AUTHORIZATION-MATRIX action it requires (RM-P1): the sidebar renders an
// entry only when GET /api/auth/permissions grants the action — the same list <Gated> consumes.
export const BUYER_CENTER_TABS: CenterTab[] = [
  {
    key: 'sourcing', label: 'Sourcing',
    items: [
      { key: 'dashboard', icon: 'dashboard', label: 'Dashboard', action: 'UseDashboards' },
      { key: 'reqs', icon: 'doc', label: 'Requisitions', action: 'ViewRequisitions' },
      { key: 'rfqs', icon: 'rfq', label: 'RFQs', action: 'ViewRfqs' },
      { key: 'awards', icon: 'award', label: 'Awards & POs', action: 'ViewAwards' },
    ],
  },
  {
    key: 'p2p', label: 'Procure to Pay',
    items: [
      { key: 'pos', icon: 'box', label: 'Purchase Orders', action: 'ViewPos' },
      { key: 'deliveries', icon: 'send', label: 'Deliveries', action: 'ViewAsns' },
      { key: 'invoices', icon: 'doc', label: 'Invoices', action: 'ViewInvoices' },
      // The PV placeholder rides the invoices read tier until L5 gives payments real actions.
      { key: 'payments', icon: 'download', label: 'Payments', action: 'ViewInvoices' },
      { key: 'statements', icon: 'clip', label: 'Statements', action: 'ViewStatements' },
    ],
  },
  {
    key: 'evaluation', label: 'Evaluation',
    items: [{ key: 'openings', icon: 'lock', label: 'Bid Openings', action: 'ViewBidOpenings' }],
  },
  {
    key: 'setup', label: 'Setup',
    items: [
      { key: 'vendors', icon: 'vendor', label: 'Vendor Master', action: 'ViewVendors' },
      { key: 'onboarding', icon: 'clip', label: 'Onboarding', action: 'ViewOnboarding' },
      { key: 'forms', icon: 'edit', label: 'Forms', action: 'ViewForms' },
    ],
  },
  {
    key: 'communication', label: 'Communication',
    items: [{ key: 'chats', icon: 'msg', label: 'Clarifications', action: 'ViewClarifications' }],
  },
  {
    key: 'administration', label: 'Administration',
    items: [
      // Management screens gate on the MANAGE action (ViewUsers is just the name directory).
      { key: 'admin', icon: 'vendor', label: 'User Management', action: 'ManageUsers' },
      { key: 'lists', icon: 'clip', label: 'Custom Lists', action: 'ManageCustomLists' },
      { key: 'customfields', icon: 'edit', label: 'Custom Fields', action: 'ManageCustomFields' },
    ],
  },
]

export const VENDOR_CENTER_TABS: CenterTab[] = [
  {
    key: 'portal', label: 'Vendor portal',
    items: [
      { key: 'dashboard', icon: 'dashboard', label: 'My RFQs' },
      { key: 'bids', icon: 'box', label: 'My Bids' },
      { key: 'pos', icon: 'box', label: 'Purchase Orders' },
      { key: 'deliveries', icon: 'send', label: 'Deliveries' },
      { key: 'invoices', icon: 'doc', label: 'Invoices' },
      { key: 'statement', icon: 'clip', label: 'Statement' },
      { key: 'chats', icon: 'msg', label: 'Clarifications' },
    ],
  },
]

/** Adapter: the sidebar renderer keeps consuming NavGroup[] unchanged. */
export const tabsToNavGroups = (tabs: CenterTab[]): NavGroup[] =>
  tabs.map((t) => ({ title: t.label, items: t.items }))

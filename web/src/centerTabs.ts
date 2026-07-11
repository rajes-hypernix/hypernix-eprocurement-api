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

export const BUYER_CENTER_TABS: CenterTab[] = [
  {
    key: 'sourcing', label: 'Sourcing',
    items: [
      { key: 'dashboard', icon: 'dashboard', label: 'Dashboard' },
      { key: 'reqs', icon: 'doc', label: 'Requisitions' },
      { key: 'rfqs', icon: 'rfq', label: 'RFQs' },
      { key: 'awards', icon: 'award', label: 'Awards & POs' },
    ],
  },
  {
    key: 'p2p', label: 'Procure to Pay',
    items: [
      { key: 'pos', icon: 'box', label: 'Purchase Orders' },
      { key: 'deliveries', icon: 'send', label: 'Deliveries' },
      { key: 'invoices', icon: 'doc', label: 'Invoices' },
      { key: 'payments', icon: 'download', label: 'Payments' },
      { key: 'statements', icon: 'clip', label: 'Statements' },
    ],
  },
  {
    key: 'evaluation', label: 'Evaluation',
    items: [{ key: 'openings', icon: 'lock', label: 'Bid Openings' }],
  },
  {
    key: 'setup', label: 'Setup',
    items: [
      { key: 'vendors', icon: 'vendor', label: 'Vendor Master' },
      { key: 'onboarding', icon: 'clip', label: 'Onboarding' },
      { key: 'forms', icon: 'edit', label: 'Forms' },
    ],
  },
  {
    key: 'communication', label: 'Communication',
    items: [{ key: 'chats', icon: 'msg', label: 'Clarifications' }],
  },
  {
    key: 'administration', label: 'Administration',
    items: [
      { key: 'admin', icon: 'vendor', label: 'User Management' },
      { key: 'lists', icon: 'clip', label: 'Custom Lists' },
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

// Buyer (procurement) sidebar, mirroring the prototype's renderSide() groups,
// labels, icons and order. Counts arrive with real data in later slices.
export interface NavItem {
  key: string
  icon: string
  label: string
}

export interface NavGroup {
  title: string
  items: NavItem[]
}

export const BUYER_NAV: NavGroup[] = [
  {
    title: 'Sourcing',
    items: [
      { key: 'dashboard', icon: 'dashboard', label: 'Dashboard' },
      { key: 'reqs', icon: 'doc', label: 'Requisitions' },
      { key: 'rfqs', icon: 'rfq', label: 'RFQs' },
      { key: 'awards', icon: 'award', label: 'Awards & POs' },
    ],
  },
  {
    title: 'Procure to Pay',
    items: [
      { key: 'pos', icon: 'box', label: 'Purchase Orders' },
      { key: 'deliveries', icon: 'send', label: 'Deliveries' },
      { key: 'invoices', icon: 'doc', label: 'Invoices' },
      { key: 'payments', icon: 'download', label: 'Payments' },
      { key: 'statements', icon: 'clip', label: 'Statements' },
    ],
  },
  {
    title: 'Evaluation',
    items: [{ key: 'openings', icon: 'lock', label: 'Bid Openings' }],
  },
  {
    title: 'Setup',
    items: [
      { key: 'vendors', icon: 'vendor', label: 'Vendor Master' },
      { key: 'onboarding', icon: 'clip', label: 'Onboarding' },
      { key: 'forms', icon: 'edit', label: 'Forms' },
    ],
  },
  {
    title: 'Communication',
    items: [{ key: 'chats', icon: 'msg', label: 'Clarifications' }],
  },
  {
    title: 'Administration',
    items: [
      { key: 'admin', icon: 'vendor', label: 'User Management' },
      { key: 'lists', icon: 'clip', label: 'Custom Lists' },
    ],
  },
]

import type { NavGroup } from './nav'

// Vendor-portal sidebar, mirroring the prototype's vendor renderSide().
export const VENDOR_NAV: NavGroup[] = [
  {
    title: 'Vendor portal',
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

import type { NavGroup } from './nav'
import { VENDOR_CENTER_TABS, tabsToNavGroups } from './centerTabs'

// Vendor-portal sidebar, mirroring the prototype's vendor renderSide().
// Data lives in centerTabs.ts since D2 (see the note in nav.ts).
export const VENDOR_NAV: NavGroup[] = tabsToNavGroups(VENDOR_CENTER_TABS)

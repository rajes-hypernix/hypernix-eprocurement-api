// Buyer (procurement) sidebar, mirroring the prototype's renderSide() groups,
// labels, icons and order. Since D2 the nav DATA lives in centerTabs.ts
// (center-tab configs, structured for D4 data-driving); this module derives
// the NavGroup shape the sidebar renderer consumes — behaviour identical.
export interface NavItem {
  key: string
  icon: string
  label: string
}

export interface NavGroup {
  title: string
  items: NavItem[]
}

import { BUYER_CENTER_TABS, tabsToNavGroups } from './centerTabs'

export const BUYER_NAV: NavGroup[] = tabsToNavGroups(BUYER_CENTER_TABS)

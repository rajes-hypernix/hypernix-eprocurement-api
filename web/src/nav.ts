// Buyer (procurement) sidebar, mirroring the prototype's renderSide() groups,
// labels, icons and order. Since D2 the nav DATA lives in centerTabs.ts
// (center-tab configs, structured for D4 data-driving); this module derives
// the NavGroup shape the sidebar renderer consumes — behaviour identical.
export interface NavItem {
  key: string
  icon: string
  label: string
  /** The AUTHORIZATION-MATRIX action this entry requires (RM-P1 nav gating).
   *  Undefined = ungated (dev-only tooling like the design gallery). */
  action?: string
}

export interface NavGroup {
  title: string
  items: NavItem[]
}

import { BUYER_CENTER_TABS, tabsToNavGroups } from './centerTabs'

export const BUYER_NAV: NavGroup[] = tabsToNavGroups(BUYER_CENTER_TABS)

/**
 * RM-P1: filter nav entries by the caller's server-derived permission list —
 * the SAME list <Gated> consumes (GET /api/auth/permissions), no second map.
 * An item without an `action` is ungated; a group with no surviving items is
 * dropped. A buyer no longer sees User Management; evaluators see their
 * evaluation surface. Routes stay URL-reachable — the server's 403 is the
 * control, this is the same courtesy view as <Gated>.
 */
export function gateNav(groups: NavGroup[], permissions: string[]): NavGroup[] {
  return groups
    .map((g) => ({ ...g, items: g.items.filter((i) => !i.action || permissions.includes(i.action)) }))
    .filter((g) => g.items.length > 0)
}

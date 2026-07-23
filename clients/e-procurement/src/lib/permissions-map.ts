import { FshPermissions } from "@/lib/fsh-permissions";
import type { NavGroup } from "@/nav";

/**
 * Maps old eProcure AUTHORIZATION-MATRIX action names (nav `action` / `<Gated>`)
 * to FSH permission strings.
 *
 * `null` = deferred / no FSH equivalent → hidden by `hasOldAction` (except AUTH_ONLY).
 */
export const OLD_ACTION_TO_FSH: Record<string, string | null> = {
  // Sourcing / dashboards
  UseDashboards: null, // authenticated-only — see AUTH_ONLY_ACTIONS
  UseSavedViews: FshPermissions.views.view,
  ViewRequisitions: FshPermissions.requisitions.view,
  ViewRfqs: FshPermissions.rfqs.view,
  ViewMyRfqs: FshPermissions.bids.viewMine,
  ViewAwards: FshPermissions.award.view,
  ViewBidOpenings: FshPermissions.evaluation.viewOpening,
  ViewClarifications: FshPermissions.clarifications.view,

  // Procure-to-pay
  ViewPos: FshPermissions.purchaseOrders.view,
  ViewAsns: FshPermissions.deliveries.view,
  ViewInvoices: FshPermissions.invoices.view,
  ViewStatements: FshPermissions.statements.view,

  // Setup
  ViewVendors: FshPermissions.vendors.view,
  ViewOnboarding: FshPermissions.onboarding.view,
  ViewForms: FshPermissions.formTemplates.view,

  // Administration
  ManageUsers: FshPermissions.users.view,
  ManageCustomLists: FshPermissions.customLists.manage,
  ManageLookups: FshPermissions.lookups.manage,
  ViewAudits: FshPermissions.auditTrails.view,
  ManageCustomFields: null,
  ManageSegments: null,
  ManageItems: null,
  ManageEntryForms: null,
  ManageNumbering: null,
  ViewNotifications: FshPermissions.notifications.view,
};

/** Old actions with no FSH perm that stay visible for any signed-in user. */
const AUTH_ONLY_ACTIONS = new Set([
  "UseDashboards",
  "ManageCustomFields",
  "ManageSegments",
  "ManageItems",
  "ManageEntryForms",
  "ManageNumbering",
]);

/** Resolve FSH permission for an old matrix action (or null if unmapped/deferred). */
export function fshPermissionForOldAction(action: string | undefined): string | null {
  if (!action) return null;
  return Object.prototype.hasOwnProperty.call(OLD_ACTION_TO_FSH, action)
    ? OLD_ACTION_TO_FSH[action]!
    : null;
}

/**
 * Returns true when the user may see a nav item / control gated by an old action name.
 * Deferred (`null`) actions are hidden unless listed in AUTH_ONLY_ACTIONS.
 */
export function hasOldAction(
  granted: readonly string[],
  action: string | undefined,
): boolean {
  if (!action) return true;
  const mapped = fshPermissionForOldAction(action);
  if (mapped === null) return AUTH_ONLY_ACTIONS.has(action);
  if (action === "ViewStatements") {
    return granted.includes(mapped) || granted.includes(FshPermissions.statements.viewMine);
  }
  // Lookups hub covers geo, lists, and org — any of those manage grants unlocks the nav item.
  if (action === "ManageLookups") {
    return (
      granted.includes(mapped) ||
      granted.includes(FshPermissions.customLists.manage) ||
      granted.includes(FshPermissions.org.manage)
    );
  }
  return granted.includes(mapped);
}

/** Filter sidebar groups — drops empty groups after permission filtering. */
export function gateNav(groups: NavGroup[], granted: readonly string[]): NavGroup[] {
  return groups
    .map((g) => ({
      ...g,
      items: g.items.filter((item) => hasOldAction(granted, item.action)),
    }))
    .filter((g) => g.items.length > 0);
}

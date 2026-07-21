import { FshPermissions } from "@/lib/fsh-permissions";

/**
 * Maps old eProcure AUTHORIZATION-MATRIX action names (nav `action` / `<Gated>`)
 * to FSH permission strings.
 *
 * Phase 1: map only — do NOT wire sidebar/`Gated` yet (Phase 6).
 * `null` = no backend equivalent yet / deferred screen — leave ungated or hide in Phase 6 triage.
 */
export const OLD_ACTION_TO_FSH: Record<string, string | null> = {
  // Sourcing / dashboards
  UseDashboards: null, // no dedicated FSH perm; treat as authenticated-only later
  UseSavedViews: null, // deferred POC feature
  ViewRequisitions: FshPermissions.requisitions.view,
  ViewRfqs: FshPermissions.rfqs.view,
  ViewAwards: FshPermissions.award.view,
  ViewBidOpenings: FshPermissions.evaluation.viewOpening,
  ViewClarifications: FshPermissions.clarifications.view,

  // Procure-to-pay
  ViewPos: FshPermissions.purchaseOrders.view,
  ViewAsns: FshPermissions.deliveries.view,
  ViewInvoices: FshPermissions.invoices.view,
  ViewStatements: null, // deferred — no Statements module yet

  // Setup
  ViewVendors: FshPermissions.vendors.view,
  ViewOnboarding: FshPermissions.onboarding.view,
  ViewForms: FshPermissions.formTemplates.view,

  // Administration (old Manage* → Platform/Identity)
  ManageUsers: FshPermissions.users.view,
  ManageCustomLists: FshPermissions.customLists.manage,
  ManageCustomFields: null, // deferred
  ManageSegments: null, // deferred
  ManageItems: null, // deferred
  ManageEntryForms: null, // deferred
  ManageNumbering: null, // deferred
};

/** Resolve FSH permission for an old matrix action (or null if unmapped/deferred). */
export function fshPermissionForOldAction(action: string | undefined): string | null {
  if (!action) return null;
  return Object.prototype.hasOwnProperty.call(OLD_ACTION_TO_FSH, action)
    ? OLD_ACTION_TO_FSH[action]!
    : null;
}

/**
 * Phase 6 helper — returns true when the user holds the mapped FSH permission.
 * Unmapped/deferred actions (`null`) currently return true so we don't hide
 * nav until triage decides; flip that policy when wiring gateNav.
 */
export function hasOldAction(
  granted: readonly string[],
  action: string | undefined,
): boolean {
  if (!action) return true;
  const mapped = fshPermissionForOldAction(action);
  if (mapped === null) return true; // Phase 6: revisit deferred keys
  return granted.includes(mapped);
}

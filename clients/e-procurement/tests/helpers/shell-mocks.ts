import type { Page } from "@playwright/test";
import { mockJsonResponse } from "./api-mocks";

/** Buyer permission set used by Vendor Master Phase 2 screens (+ Phase 6 inbox). */
export const BUYER_PERMS = [
  "Permissions.Suppliers.Vendors.View",
  "Permissions.Suppliers.Vendors.Create",
  "Permissions.Suppliers.Vendors.Update",
  "Permissions.Suppliers.Vendors.ToggleStatus",
  "Permissions.Suppliers.Swec.View",
  "Permissions.Suppliers.Onboarding.View",
  "Permissions.Suppliers.Onboarding.Invite",
  "Permissions.Suppliers.Onboarding.Revoke",
  "Permissions.Suppliers.Onboarding.Review",
  "Permissions.Sourcing.Requisitions.View",
  "Permissions.Notifications.Inbox.View",
  "Permissions.Notifications.Inbox.MarkRead",
] as const;

export function paged<T>(
  items: T[],
  opts: { pageNumber?: number; pageSize?: number; totalCount?: number } = {},
) {
  const pageNumber = opts.pageNumber ?? 1;
  const pageSize = opts.pageSize ?? 20;
  const totalCount = opts.totalCount ?? items.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  return {
    items,
    pageNumber,
    pageSize,
    totalCount,
    totalPages,
    hasNext: pageNumber < totalPages,
    hasPrevious: pageNumber > 1,
  };
}

/**
 * Shell + dashboard smoke GETs. Auth context re-hydrates permissions from
 * /identity/permissions after mount — keep that list aligned with seedAuthedSession.
 */
export async function installEprocShellMocks(
  page: Page,
  perms: readonly string[] = BUYER_PERMS,
): Promise<void> {
  await mockJsonResponse(page, "**/api/v1/identity/permissions", [...perms]);
  await mockJsonResponse(page, "**/api/v1/notifications/unread-count**", 0);
  await mockJsonResponse(page, "**/api/v1/notifications/?*", []);
  await mockJsonResponse(page, "**/api/v1/notifications", []);
  await mockJsonResponse(page, "**/api/v1/sourcing/requisitions**", []);
}

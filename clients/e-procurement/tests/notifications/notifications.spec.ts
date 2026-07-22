import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const NOTIF = {
  id: "11111111-1111-1111-1111-111111111111",
  type: "sourcing.rfq.released",
  title: "RFQ released",
  body: "RFQ-2026-0001 is open for bidding.",
  link: "/sourcing/rfqs/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  source: "Communication",
  metadataJson: "{}",
  readAtUtc: null,
  createdAtUtc: "2026-07-20T08:00:00Z",
};

test.describe("Notifications", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...BUYER_PERMS] });
    await installEprocShellMocks(page);
  });

  test("inbox empty state for unread filter", async ({ page }) => {
    await page.goto("/notifications");
    await expect(page.getByRole("heading", { name: "Notifications", exact: true })).toBeVisible();
    await expect(page.getByText(/Nothing unread/i)).toBeVisible();
  });

  test("renders a notification and rewrites backend links", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/notifications/?*", [NOTIF]);
    await page.goto("/notifications");

    await expect(page.getByText("RFQ released", { exact: true })).toBeVisible();
    await expect(page.getByText("RFQ-2026-0001 is open for bidding.")).toBeVisible();
    await expect(page.getByRole("button", { name: /Mark read/i })).toBeVisible();

    await page.getByRole("link", { name: /Open/i }).click();
    await expect(page).toHaveURL(/\/rfqs\/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/);
  });

  test("topbar bell shows unread badge", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/notifications/unread-count**", 3);
    await page.goto("/dashboard");
    await expect(page.getByRole("button", { name: /3 unread notifications/i })).toBeVisible();
  });
});

test.describe("Nav gating", () => {
  test("hides modules the buyer lacks permission for", async ({ page }) => {
    const limited = [
      "Permissions.Suppliers.Vendors.View",
      "Permissions.Notifications.Inbox.View",
      "Permissions.Notifications.Inbox.MarkRead",
    ];
    await seedAuthedSession(page, { ...TEST_USER, permissions: limited });
    await installEprocShellMocks(page, limited);

    await page.goto("/dashboard");

    await expect(page.locator(".nav", { hasText: "Vendor Master" })).toHaveCount(1);
    await expect(page.locator(".nav", { hasText: "Requisitions" })).toHaveCount(0);
    await expect(page.locator(".nav", { hasText: "Purchase Orders" })).toHaveCount(0);
    await expect(page.locator(".nav", { hasText: "Saved Views" })).toHaveCount(1);
    await expect(page.locator(".nav", { hasText: "User Management" })).toHaveCount(0);
  });
});

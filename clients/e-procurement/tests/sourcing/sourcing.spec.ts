import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const RFQ_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const PR_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

const SOURCING_PERMS = [
  ...BUYER_PERMS,
  "Permissions.Sourcing.Rfqs.View",
  "Permissions.Sourcing.Rfqs.Create",
  "Permissions.Sourcing.Requisitions.Create",
] as const;

const RFQ = {
  id: RFQ_ID,
  code: "RFQ-2026-0001",
  title: "Centrifugal pumps",
  status: "Open",
  envelope: "Dual",
  invitedCount: 3,
  bidCount: 1,
  closesUtc: "2026-08-01T00:00:00Z",
};

const PR = {
  id: PR_ID,
  code: "PR-2026-0001",
  requestor: "Ali Buyer",
  department: "Ops",
  headerStatus: "Submitted",
  lineCount: 2,
  createdUtc: "2026-07-01T00:00:00Z",
};

test.describe("Sourcing lists", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...SOURCING_PERMS] });
    await installEprocShellMocks(page, SOURCING_PERMS);
    await mockJsonResponse(page, "**/api/v1/sourcing/rfqs**", [RFQ]);
    await mockJsonResponse(page, "**/api/v1/sourcing/requisitions**", [PR]);
  });

  test("RFQ list shows table and board toggle", async ({ page }) => {
    await page.goto("/rfqs");
    const main = page.getByRole("main");
    await expect(main.getByRole("heading", { name: "RFQs" })).toBeVisible();
    await expect(main.getByText("RFQ-2026-0001")).toBeVisible();
    await expect(main.getByText("Open · Awaiting bids")).toBeVisible();
    await expect(main.getByText("Partially bid · 1/3")).toBeVisible();
    await expect(main.getByRole("button", { name: "More actions" })).toBeVisible();
    await main.getByRole("button", { name: "More actions" }).click();
    await expect(main.getByRole("button", { name: "Close bids" })).toBeVisible();
    await expect(main.getByRole("button", { name: "Board", exact: true })).toBeVisible();
    await main.getByRole("button", { name: "Board", exact: true }).click();
    await expect(main.locator(".kanban")).toBeVisible();
    await expect(main.locator(".kcard", { hasText: "RFQ-2026-0001" })).toBeVisible();
  });

  test("requisition list shows table and board toggle", async ({ page }) => {
    await page.goto("/reqs");
    const main = page.getByRole("main");
    await expect(main.getByRole("heading", { name: "Requisitions" })).toBeVisible();
    await expect(main.getByText("PR-2026-0001")).toBeVisible();
    await expect(main.getByRole("button", { name: "Board", exact: true })).toBeVisible();
    await main.getByRole("button", { name: "Board", exact: true }).click();
    await expect(main.locator(".kanban")).toBeVisible();
    await expect(main.locator(".kcard", { hasText: "PR-2026-0001" })).toBeVisible();
  });
});

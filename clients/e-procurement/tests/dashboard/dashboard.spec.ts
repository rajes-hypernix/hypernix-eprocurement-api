import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const DASH_PERMS = [
  ...BUYER_PERMS,
  "Permissions.Sourcing.Rfqs.View",
  "Permissions.Procurement.PurchaseOrders.View",
  "Permissions.Procurement.Deliveries.View",
  "Permissions.Procurement.Invoices.View",
] as const;

test.describe("Buyer dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...DASH_PERMS] });
    await installEprocShellMocks(page, DASH_PERMS);
    await mockJsonResponse(page, "**/api/v1/sourcing/requisitions**", [
      {
        id: "r1",
        code: "PR-1",
        requestor: "A",
        department: "Ops",
        headerStatus: "Submitted",
        lineCount: 1,
        createdUtc: "2026-07-01T00:00:00Z",
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/sourcing/rfqs**", [
      {
        id: "rfq1",
        code: "RFQ-1",
        title: "Pumps",
        status: "Open",
        envelope: "Dual",
        invitedCount: 2,
        bidCount: 0,
        closesUtc: null,
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/procurement/purchase-orders**", []);
    await mockJsonResponse(page, "**/api/v1/procurement/asns**", []);
    await mockJsonResponse(page, "**/api/v1/procurement/invoices**", []);
  });

  test("shows KPI tiles and shortcuts", async ({ page }) => {
    await page.goto("/dashboard");
    await expect(page.getByRole("heading", { name: "Dashboard", exact: true })).toBeVisible();
    await expect(page.getByText("Active requisitions")).toBeVisible();
    await expect(page.getByText("Open RFQs")).toBeVisible();
    const main = page.getByRole("main");
    await expect(main.getByRole("button", { name: /New requisition/i })).toBeVisible();
    await expect(main.getByRole("button", { name: /Vendor Master/i })).toBeVisible();
  });
});

const VENDOR_PERMS = [
  "Permissions.Sourcing.Bids.ViewMine",
  "Permissions.Sourcing.Bids.Respond",
  "Permissions.Procurement.PurchaseOrders.View",
  "Permissions.Procurement.PurchaseOrders.Acknowledge",
  "Permissions.Procurement.Deliveries.View",
  "Permissions.Procurement.Invoices.View",
  "Permissions.Notifications.Inbox.View",
] as const;

test.describe("Vendor dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, {
      ...TEST_USER,
      vendorId: "11111111-1111-1111-1111-111111111111",
      permissions: [...VENDOR_PERMS],
    });
    await installEprocShellMocks(page, VENDOR_PERMS);
    await mockJsonResponse(page, "**/api/v1/sourcing/my/rfqs", [
      {
        rfqId: "rfq-mine",
        rfqCode: "RFQ-MINE",
        title: "Only my invitation",
        envelope: "Dual",
        rfqStatus: "Open",
        invitationStatus: "Invited",
        closesUtc: "2026-09-01T00:00:00Z",
        hasSubmittedBid: false,
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/procurement/purchase-orders**", [
      {
        id: "po-mine",
        code: "PO-MINE",
        vendorId: "11111111-1111-1111-1111-111111111111",
        vendorName: "Acme",
        status: "Issued",
        totalValue: 100,
        awardId: null,
        rfqId: "rfq-mine",
      },
    ]);
  });

  test("shows vendor work-queue tiles, not buyer requisition shortcuts", async ({ page }) => {
    await page.goto("/dashboard");
    await expect(page.getByRole("heading", { name: "Dashboard", exact: true })).toBeVisible();
    await expect(page.getByText("RFQs to bid")).toBeVisible();
    await expect(page.getByText("POs to acknowledge")).toBeVisible();
    await expect(page.getByText("RFQ-MINE")).toBeVisible();
    await expect(page.getByText("Active requisitions")).toHaveCount(0);
    await expect(page.getByRole("button", { name: /Vendor Master/i })).toHaveCount(0);
  });

  test("buyer RFQ list route redirects the vendor home", async ({ page }) => {
    await page.goto("/rfqs");
    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.getByText("Your work queue")).toBeVisible();
  });
});

const EVAL_DASH_PERMS = [
  "Permissions.Sourcing.Evaluation.ViewOpening",
  "Permissions.Sourcing.Evaluation.OpenTechnical",
  "Permissions.Sourcing.Evaluation.Score",
  "Permissions.Sourcing.Evaluation.FinalizeTechnical",
  "Permissions.Sourcing.Rfqs.View",
  "Permissions.Notifications.Inbox.View",
] as const;

test.describe("Evaluator dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, {
      ...TEST_USER,
      roles: ["TechEvaluator"],
      permissions: [...EVAL_DASH_PERMS],
    });
    await installEprocShellMocks(page, EVAL_DASH_PERMS);
    await mockJsonResponse(page, "**/api/v1/sourcing/rfqs**", [
      {
        id: "rfq-eval",
        code: "RFQ-EV-1",
        title: "Assigned pumps",
        status: "Closed",
        envelope: "Dual",
        invitedCount: 2,
        bidCount: 1,
        closesUtc: "2026-01-01T00:00:00Z",
        technicalEvaluatorIds: [TEST_USER.sub],
        commercialEvaluatorIds: [],
      },
    ]);
  });

  test("shows evaluation queue and hides buyer sidebar items", async ({ page }) => {
    await page.goto("/dashboard");
    await expect(page.getByRole("heading", { name: "Dashboard", exact: true })).toBeVisible();
    await expect(page.getByText("Ready to evaluate")).toBeVisible();
    await expect(page.getByText("RFQ-EV-1")).toBeVisible();
    await expect(page.getByText("Active requisitions")).toHaveCount(0);
    const nav = page.getByLabel("Main navigation");
    await expect(nav.getByRole("link", { name: "Saved Views", exact: true })).toHaveCount(0);
    await expect(nav.getByRole("link", { name: "Clarifications", exact: true })).toHaveCount(0);
    await expect(nav.getByRole("link", { name: "User Management", exact: true })).toHaveCount(0);
    await expect(nav.getByRole("link", { name: "Bid Openings", exact: true })).toBeVisible();
  });
});

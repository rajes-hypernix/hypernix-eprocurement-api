import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const RFQ_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const PR_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
const VIEW_ID = "cccccccc-cccc-cccc-cccc-cccccccccccc";
const LINE_ID = "dddddddd-dddd-dddd-dddd-dddddddddddd";

const SOURCING_PERMS = [
  ...BUYER_PERMS,
  "Permissions.Sourcing.Rfqs.View",
  "Permissions.Sourcing.Rfqs.ManageDraft",
  "Permissions.Sourcing.Requisitions.View",
  "Permissions.Sourcing.Requisitions.Manage",
  "Permissions.Platform.Views.View",
  "Permissions.Platform.Views.ManageOwn",
] as const;

const PR_LIST = {
  id: PR_ID,
  code: "PR-2026-0001",
  requestor: "Ali Buyer",
  department: "Ops",
  headerStatus: "Submitted",
  submitted: true,
  lineCount: 1,
  createdUtc: "2026-07-01T00:00:00Z",
};

const PR_DETAIL = {
  ...PR_LIST,
  location: "HQ",
  category: "Goods",
  job: "J1",
  memo: "",
  costCentre: "CC",
  currency: "MYR",
  lines: [
    {
      id: LINE_ID,
      itemCode: "PMP-01",
      description: "Pump",
      qty: 2,
      uom: "EA",
      estUnitPrice: 100,
      lifecycleStatus: "Open",
    },
  ],
  createdUtc: "2026-07-01T00:00:00Z",
  updatedUtc: "2026-07-01T00:00:00Z",
};

const SYS_VIEW = {
  id: VIEW_ID,
  code: "SYS-REQ-ALL",
  name: "All Requisitions",
  recordType: "Requisition",
  ownerUserId: null,
  isShared: true,
  isSystem: true,
  filters: [],
  columns: [{ fieldKey: "Code", label: null, sort: 0, sortDirection: null }],
  createdUtc: "2026-01-01T00:00:00Z",
  updatedUtc: "2026-01-01T00:00:00Z",
};

test.describe("Sourcing UI parity", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...SOURCING_PERMS] });
    await installEprocShellMocks(page, SOURCING_PERMS);

    // Detail before list — Playwright last-match wins; keep list glob from swallowing /{id}.
    await page.route(`**/api/v1/sourcing/requisitions/${PR_ID}`, async (route) => {
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(PR_DETAIL),
      });
    });
    await page.route("**/api/v1/sourcing/requisitions", async (route) => {
      if (route.request().method() !== "GET") {
        await route.fallback();
        return;
      }
      const url = route.request().url();
      if (url.includes(`/requisitions/${PR_ID}`)) {
        await route.fulfill({
          status: 200,
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(PR_DETAIL),
        });
        return;
      }
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify([PR_LIST]),
      });
    });
    await mockJsonResponse(page, "**/api/v1/sourcing/rfqs**", []);
    await page.route("**/api/v1/platform/views/fields**", async (route) => {
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify([
          { fieldKey: "Code", kind: "Native", label: "Code", dataType: "Code" },
          { fieldKey: "HeaderStatus", kind: "Native", label: "Status", dataType: "Enum" },
        ]),
      });
    });
    await page.route("**/api/v1/platform/views**", async (route) => {
      const url = route.request().url();
      if (url.includes("/fields") || url.includes("/run") || url.includes("/share")) {
        await route.fallback();
        return;
      }
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify([SYS_VIEW]),
      });
    });
  });

  test("Build RFQ opens consolidate workspace", async ({ page }) => {
    await page.goto("/reqs");
    await expect(page.getByRole("heading", { name: "Requisitions" })).toBeVisible();
    await page.getByRole("button", { name: "Build RFQ" }).click();
    await expect(page).toHaveURL(/\/consolidate$/);
    await expect(page.getByRole("heading", { name: "RFQ Workspace" })).toBeVisible();
  });

  test("rfqs/new redirects to consolidate", async ({ page }) => {
    await page.goto("/rfqs/new");
    await expect(page).toHaveURL(/\/consolidate$/);
    await expect(page.getByRole("heading", { name: "RFQ Workspace" })).toBeVisible();
  });

  test("requisition ViewPicker loads system views", async ({ page }) => {
    await page.route(`**/api/v1/platform/views/${VIEW_ID}/run**`, async (route) => {
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          rows: [{ Id: PR_ID, Code: "PR-2026-0001" }],
          totalCount: 1,
          page: 1,
          size: 200,
        }),
      });
    });
    await page.goto("/reqs");
    const viewSelect = page.locator(".field", { hasText: "Saved view" }).locator("select");
    await expect(viewSelect).toBeVisible();
    await viewSelect.selectOption(VIEW_ID);
    await expect(page.getByText("PR-2026-0001")).toBeVisible();
    await expect(viewSelect.locator("option").first()).toHaveText("Standard list");
  });

  test("Saved Views home lists system views", async ({ page }) => {
    await page.goto("/views");
    await expect(page.getByRole("heading", { name: "Saved Views" })).toBeVisible();
    await expect(page.getByRole("cell", { name: "All Requisitions" })).toBeVisible();
    await expect(page.getByText("SYS-REQ-ALL")).toBeVisible();
  });

  test("RFQ draft wizard shows step rail", async ({ page }) => {
    const draft = {
      id: RFQ_ID,
      code: "RFQ-2026-0001",
      title: "Pumps",
      envelope: "Dual",
      status: "Draft",
      currency: "MYR",
      opensUtc: null,
      closesUtc: null,
      originalClosesUtc: null,
      extensionCount: 0,
      releasedUtc: null,
      closedUtc: null,
      prRefs: [PR_ID],
      lines: [
        {
          lineCode: "L1",
          itemCode: "PMP-01",
          description: "Pump",
          qty: 2,
          uom: "EA",
          prRef: "PR-2026-0001",
          sourcePrLineIds: [LINE_ID],
        },
      ],
      formItems: [],
      technicalSections: [],
      commercialSections: [],
      technicalEvaluatorIds: [],
      commercialEvaluatorIds: [],
      technicalOpened: false,
      techFinalized: false,
      commercialOpened: false,
      invitations: [],
      events: [],
      createdUtc: "2026-07-01T00:00:00Z",
      updatedUtc: "2026-07-01T00:00:00Z",
    };
    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}**`, draft);
    await mockJsonResponse(page, "**/api/v1/platform/form-templates**", []);
    await page.goto(`/rfqs/${RFQ_ID}`);
    await expect(page.getByRole("button", { name: "Items" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Settings" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Questions" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Vendors" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Evaluators" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Review" })).toBeVisible();
    await expect(page.getByText("PMP-01")).toBeVisible();
  });
});

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
      clarificationDeadlineUtc: null,
      bidValidityDays: null,
      partialBidsAllowed: true,
      incotermId: null,
      incotermCode: null,
      incotermSuffix: null,
    };
    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}**`, draft);
    await mockJsonResponse(page, "**/api/v1/platform/form-templates**", []);
    await mockJsonResponse(page, "**/api/v1/platform/incoterms**", [
      {
        id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
        code: "FOB",
        name: "Free On Board",
        isActive: true,
        createdOnUtc: "2026-01-01T00:00:00Z",
      },
      {
        id: "ffffffff-ffff-ffff-ffff-ffffffffffff",
        code: "CIF",
        name: "Cost, Insurance and Freight",
        isActive: true,
        createdOnUtc: "2026-01-01T00:00:00Z",
      },
    ]);
    await page.goto(`/rfqs/${RFQ_ID}`);
    await expect(page.getByRole("button", { name: "Items" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Settings" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Questions" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Vendors" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Evaluators" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Review" })).toBeVisible();
    await expect(page.getByText("PMP-01")).toBeVisible();
  });

  test("RFQ draft Settings / Questions / Evaluators use master data", async ({ page }) => {
    const INCOTERM_ID = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
    const TECH_ROLE_ID = "r1111111-1111-1111-1111-111111111111";
    const COMM_ROLE_ID = "r2222222-2222-2222-2222-222222222222";
    const TECH_USER_ID = "u1111111-1111-1111-1111-111111111111";
    const COMM_USER_ID = "u2222222-2222-2222-2222-222222222222";

    const draft = {
      id: RFQ_ID,
      code: "RFQ-2026-0002",
      title: "",
      envelope: "Dual",
      status: "Draft",
      currency: "MYR",
      opensUtc: null,
      closesUtc: null,
      originalClosesUtc: null,
      extensionCount: 0,
      releasedUtc: null,
      closedUtc: null,
      prRefs: [],
      lines: [],
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
      clarificationDeadlineUtc: null,
      bidValidityDays: null,
      partialBidsAllowed: true,
      incotermId: null,
      incotermCode: null,
      incotermSuffix: null,
    };

    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}**`, draft);
    await mockJsonResponse(page, "**/api/v1/platform/form-templates**", []);
    await mockJsonResponse(page, "**/api/v1/platform/incoterms**", [
      {
        id: INCOTERM_ID,
        code: "FOB",
        name: "Free On Board",
        isActive: true,
        createdOnUtc: "2026-01-01T00:00:00Z",
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/platform/currencies**", [
      {
        id: "c1111111-1111-1111-1111-111111111111",
        code: "MYR",
        name: "Malaysian Ringgit",
        symbol: "RM",
        decimals: 2,
        isActive: true,
        createdOnUtc: "2026-01-01T00:00:00Z",
      },
      {
        id: "c2222222-2222-2222-2222-222222222222",
        code: "USD",
        name: "US Dollar",
        symbol: "$",
        decimals: 2,
        isActive: true,
        createdOnUtc: "2026-01-01T00:00:00Z",
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/platform/exchange-rates/current**", [
      {
        currencyCode: "MYR",
        currencyName: "Malaysian Ringgit",
        rateToBase: null,
        effectiveDate: null,
        isBaseCurrency: true,
      },
      {
        currencyCode: "USD",
        currencyName: "US Dollar",
        rateToBase: 4.2,
        effectiveDate: "2026-07-01",
        isBaseCurrency: false,
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/identity/roles**", [
      { id: TECH_ROLE_ID, name: "TechEvaluator", description: "Tech", createdOnUtc: "2026-01-01T00:00:00Z" },
      { id: COMM_ROLE_ID, name: "CommEvaluator", description: "Comm", createdOnUtc: "2026-01-01T00:00:00Z" },
    ]);
    await page.route("**/api/v1/identity/users/search**", async (route) => {
      const url = new URL(route.request().url());
      const roleId = url.searchParams.get("RoleId") ?? url.searchParams.get("roleId");
      const items =
        roleId === TECH_ROLE_ID
          ? [{ id: TECH_USER_ID, firstName: "Nur", lastName: "Tech", userName: "nur.tech", isActive: true, emailConfirmed: true }]
          : roleId === COMM_ROLE_ID
            ? [{ id: COMM_USER_ID, firstName: "Kai", lastName: "Comm", userName: "kai.comm", isActive: true, emailConfirmed: true }]
            : [];
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          items,
          pageNumber: 1,
          pageSize: 100,
          totalCount: items.length,
          totalPages: 1,
          hasNext: false,
          hasPrevious: false,
        }),
      });
    });
    await mockJsonResponse(page, "**/api/v1/suppliers/vendors/search**", {
      items: [],
      pageNumber: 1,
      pageSize: 200,
      totalCount: 0,
      totalPages: 1,
      hasNext: false,
      hasPrevious: false,
    });
    await mockJsonResponse(page, "**/api/v1/suppliers/swec**", []);

    await page.goto(`/rfqs/${RFQ_ID}`);

    // Settings — Shipping band + master Incoterm as code — name
    await page.getByRole("button", { name: "Settings" }).click();
    await expect(page.getByLabel("Shipping")).toBeVisible();
    await expect(page.locator(".txn-bandbar", { hasText: "Shipping" })).toBeVisible();
    await page.locator(".field", { hasText: "RFQ title" }).locator("input").fill("Pumps RFQ");
    await page.locator(".field", { hasText: "Bid opens" }).locator("input").fill("2026-08-20T09:00");
    await page.locator(".field", { hasText: "Bid closes" }).locator("input").fill("2026-08-30T17:00");
    const currencySelect = page.locator(".field").filter({ has: page.locator("label", { hasText: /^Currency$/ }) }).locator("select");
    await expect(currencySelect.locator("option")).toContainText(["MYR — Malaysian Ringgit", "USD — US Dollar"]);
    await currencySelect.selectOption("USD");
    await expect(page.getByLabel("Exchange rate", { exact: true })).toContainText("1 USD = 4.2 MYR");
    await page.locator(".field", { hasText: "Clarification deadline" }).locator("input").fill("2026-08-15T12:00");
    await expect(page.getByText("Must be between bid open and close dates.")).toBeVisible();
    await page.locator(".field", { hasText: "Clarification deadline" }).locator("input").fill("2026-08-25T12:00");
    const shipping = page.getByLabel("Shipping");
    const incotermSelect = shipping.locator(".field").filter({ has: page.locator("label", { hasText: /^Incoterm$/ }) }).locator("select");
    await expect(incotermSelect).toBeVisible();
    await expect(incotermSelect.locator("option")).toContainText(["FOB — Free On Board"]);
    await incotermSelect.selectOption({ label: "FOB — Free On Board" });
    await expect(page.getByPlaceholder("e.g. Port Klang")).toBeEnabled();
    await page.getByPlaceholder("e.g. Port Klang").fill("Port Klang");

    // Questions — sectioned editor
    await page.getByRole("button", { name: "Questions" }).click();
    await expect(page.getByText("Form library", { exact: true })).toBeVisible();
    await expect(page.locator(".qgtitle", { hasText: "Technical" })).toBeVisible();
    await page.locator(".qgroup").filter({ hasText: "Technical" }).getByRole("button", { name: /Add item/ }).click();
    await page.locator(".qmenu").getByRole("button", { name: "Short text" }).click();
    await expect(page.getByText("Untitled question")).toBeVisible();

    // Evaluators — role pools + early warn
    await page.getByRole("button", { name: "Evaluators" }).click();
    await expect(page.getByText(/At least one technical evaluator is required/)).toBeVisible();
    await expect(page.getByRole("heading", { name: "Technical envelope — evaluators" })).toBeVisible();
    await expect(page.getByText("Nur Tech")).toBeVisible();
    await expect(page.getByText("Kai Comm")).toBeVisible();
    await page.locator("label.ck", { hasText: "Nur Tech" }).locator('input[type="checkbox"]').check();
    await expect(page.getByText(/At least one technical evaluator is required/)).toHaveCount(0);
  });

  test("RFQ draft shows line segment columns and unsaved leave guard", async ({ page }) => {
    const draft = {
      id: RFQ_ID,
      code: "RFQ-2026-0003",
      title: "Pumps",
      envelope: "Single",
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
      clarificationDeadlineUtc: null,
      bidValidityDays: null,
      partialBidsAllowed: true,
      incotermId: null,
      incotermCode: null,
      incotermSuffix: null,
    };

    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}**`, draft);
    await mockJsonResponse(page, "**/api/v1/platform/form-templates**", []);
    await mockJsonResponse(page, "**/api/v1/platform/incoterms**", []);
    await mockJsonResponse(page, "**/api/v1/platform/currencies**", [
      {
        id: "c1111111-1111-1111-1111-111111111111",
        code: "MYR",
        name: "Malaysian Ringgit",
        symbol: "RM",
        decimals: 2,
        isActive: true,
        createdOnUtc: "2026-01-01T00:00:00Z",
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/platform/exchange-rates/current**", [
      {
        currencyCode: "MYR",
        currencyName: "Malaysian Ringgit",
        rateToBase: null,
        effectiveDate: null,
        isBaseCurrency: true,
      },
    ]);
    await mockJsonResponse(page, `**/api/v1/platform/segments/assignments/Requisition/${PR_ID}**`, [
      {
        id: "s1111111-1111-1111-1111-111111111111",
        dimension: "Project",
        orgUnitId: "o1111111-1111-1111-1111-111111111111",
        orgUnitCode: "PRJ-A",
        orgUnitName: "Alpha Project",
        lineId: LINE_ID,
      },
    ]);
    await mockJsonResponse(page, "**/api/v1/suppliers/vendors/search**", {
      items: [],
      pageNumber: 1,
      pageSize: 200,
      totalCount: 0,
      totalPages: 1,
      hasNext: false,
      hasPrevious: false,
    });
    await mockJsonResponse(page, "**/api/v1/suppliers/swec**", []);

    await page.goto(`/rfqs/${RFQ_ID}`);
    await expect(page.getByRole("columnheader", { name: "Project" })).toBeVisible();
    await expect(page.getByRole("cell", { name: "Alpha Project" })).toBeVisible();

    await page.getByRole("button", { name: "Settings" }).click();
    await page.locator(".field", { hasText: "RFQ title" }).locator("input").fill("Dirty title");
    await page.getByRole("main").getByRole("button", { name: "RFQs" }).click();
    await expect(page.getByRole("heading", { name: "Unsaved changes" })).toBeVisible();
    await page.getByRole("button", { name: "Stay on page" }).click();
    await expect(page.getByRole("heading", { name: "Create RFQ" })).toBeVisible();
  });
});

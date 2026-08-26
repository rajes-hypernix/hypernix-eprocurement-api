import { expect, test } from "@playwright/test";
import { mockJsonResponse, mockProblemDetails } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const VENDOR_ID = "11111111-1111-1111-1111-111111111111";

const STATEMENT_LIST = [
  {
    vendorId: VENDOR_ID,
    vendorName: "Acme Supplies",
    invoiced: 5000,
    paid: 0,
    balance: 5000,
    grni: 1200,
  },
];

const STATEMENTS_PERMS = [
  ...BUYER_PERMS,
  "Permissions.Procurement.Statements.View",
] as const;

test.describe("Statements", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...STATEMENTS_PERMS] });
    await installEprocShellMocks(page, STATEMENTS_PERMS);
    await mockJsonResponse(page, "**/api/v1/procurement/statements", STATEMENT_LIST);
  });

  test("lists statement summaries for buyers", async ({ page }) => {
    await page.goto("/statements");
    await expect(page.getByRole("heading", { name: "Statements of Account" })).toBeVisible();
    await expect(page.getByText("Acme Supplies")).toBeVisible();
    await expect(page.getByText(/RM 5,000/).first()).toBeVisible();
    await expect(page.getByText(/RM 1,200/).first()).toBeVisible();
  });

  test("filters the list by vendor name and Reset restores it", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/procurement/statements", [
      ...STATEMENT_LIST,
      {
        vendorId: "22222222-2222-2222-2222-222222222222",
        vendorName: "Borneo Hardware",
        invoiced: 800,
        paid: 0,
        balance: 800,
        grni: 0,
      },
    ]);
    await page.goto("/statements");
    const search = page.getByLabel("Search statements");
    await expect(search).toBeVisible();
    await expect(page.getByText("Borneo Hardware")).toBeVisible();

    await search.fill("zzz-no-match");
    await expect(page.getByText("No statements match this search.")).toBeVisible();
    await expect(page.getByText("Borneo Hardware")).toHaveCount(0);

    await page.getByRole("button", { name: "Reset" }).click();
    await expect(page.getByText("Borneo Hardware")).toBeVisible();
    await expect(page.getByText("Acme Supplies")).toBeVisible();
  });

  test("opens statement detail", async ({ page }) => {
    await mockJsonResponse(page, `**/api/v1/procurement/statements/${VENDOR_ID}`, {
      vendorId: VENDOR_ID,
      vendorName: "Acme Supplies",
      invoiced: 5000,
      paid: 0,
      balance: 5000,
      grni: 1200,
      aging: { current: 5000, d30: 0, d60: 0, d90: 0 },
      ledger: [
        {
          date: "",
          reference: "PO-1",
          type: "PO issued",
          memo: 5000,
          debit: 0,
          credit: 0,
          balance: 0,
        },
      ],
    });
    await page.goto("/statements");
    await page.getByText("Acme Supplies").click();
    await expect(page.getByRole("heading", { name: "Acme Supplies" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Message vendor" })).toBeVisible();
    await expect(page.getByText("Aged payables")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Ledger" })).toBeVisible();
    await expect(page.getByText("Open balance")).toBeVisible();
  });

  test("shows a finished empty page when the statement is missing", async ({ page }) => {
    await mockProblemDetails(page, `**/api/v1/procurement/statements/${VENDOR_ID}`, 404, {
      title: "Not Found",
      detail: "No statement",
    });
    await page.goto(`/statements/${VENDOR_ID}`);
    await expect(page.getByText("No statement for this vendor.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Back" })).toBeVisible();
  });
});

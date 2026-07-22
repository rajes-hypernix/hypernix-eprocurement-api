import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const VENDOR_ID = "11111111-1111-1111-1111-111111111111";

const STATEMENT_LIST = [
  {
    vendorId: VENDOR_ID,
    vendorName: "11111111",
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
    await expect(page.getByText("11111111")).toBeVisible();
    await expect(page.getByText(/RM 5,000/).first()).toBeVisible();
    await expect(page.getByText(/RM 1,200/).first()).toBeVisible();
  });
});

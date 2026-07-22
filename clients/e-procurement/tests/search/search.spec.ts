import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const VENDOR_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

const SEARCH_HITS = [
  {
    type: "Vendor",
    id: VENDOR_ID,
    code: "V-10001",
    title: "Acme Supplies",
    subtitle: "Acme Supplies Sdn Bhd",
  },
];

test.describe("Global search", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...BUYER_PERMS] });
    await installEprocShellMocks(page);
  });

  test("shows grouped hits and navigates on click", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/search**", SEARCH_HITS);
    await page.goto("/dashboard");

    const input = page.getByPlaceholder("Search vendors, reqs, RFQs, POs…");
    await input.fill("acme");
    await expect(page.getByText("Vendors", { exact: true })).toBeVisible();
    await expect(page.getByRole("option", { name: /V-10001/ })).toBeVisible();

    await page.getByRole("option", { name: /V-10001/ }).click();
    await expect(page).toHaveURL(new RegExp(`/vendors/${VENDOR_ID}$`));
  });
});

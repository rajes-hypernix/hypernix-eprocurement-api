import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks, paged } from "../helpers/shell-mocks";

const VENDOR = {
  id: "11111111-1111-1111-1111-111111111111",
  code: "SWK-V-10001",
  name: "Sentausa Engineering Sdn Bhd",
  type: "SWEC",
  categories: ["40101800P"],
  region: "Peninsular",
  state: "Selangor",
  rating: 4.6,
  status: "Registered",
};

const VENDOR_DETAIL = {
  ...VENDOR,
  registeredName: "Sentausa Engineering Sdn Bhd",
  registrationNo: "1234567-A",
  taxId: "SST-001",
  llrcTier: null,
  city: "Petaling Jaya",
  country: "MY",
  paymentTerms: "NET30",
  creditLimit: 50000,
  contacts: [
    {
      name: "Ali Buyer",
      role: "Sales",
      email: "ali@sentausa.example",
      phone: "+60123456789",
      isPrimary: true,
    },
  ],
  addresses: [
    {
      type: "Registered",
      line: "1 Jalan Vendor",
      city: "Petaling Jaya",
      state: "Selangor",
      country: "MY",
      postcode: "46000",
      isPrimary: true,
    },
  ],
  bankAccounts: [],
  certifications: [],
  currencies: [{ code: "MYR", isPrimary: true }],
  createdUtc: "2026-01-01T00:00:00Z",
  updatedUtc: "2026-01-02T00:00:00Z",
};

const SWEC = [
  {
    code: "40",
    name: "Equipment",
    parentCode: null,
    level: 1,
    isLeaf: false,
    pathText: "Equipment",
  },
  {
    code: "40101800P",
    name: "Pumps",
    parentCode: "40",
    level: 3,
    isLeaf: true,
    pathText: "Equipment > Pumps",
  },
];

test.describe("Vendor Master", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...BUYER_PERMS] });
    await installEprocShellMocks(page);
    await mockJsonResponse(page, "**/api/v1/suppliers/swec**", SWEC);
    // One handler: list vs detail. Later `**/vendors**` must not swallow `/vendors/{id}`.
    await page.route("**/api/v1/suppliers/vendors**", async (route) => {
      const url = route.request().url();
      const detail = /\/vendors\/([0-9a-f-]{36})/i.exec(url);
      const body = detail ? VENDOR_DETAIL : paged([VENDOR], { totalCount: 1 });
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
    });
  });

  test("lists mocked vendors from the suppliers API", async ({ page }) => {
    await page.goto("/vendors");

    await expect(page.getByRole("heading", { name: "Vendor Master" })).toBeVisible();
    await expect(page.getByText("Sentausa Engineering Sdn Bhd")).toBeVisible();
    await expect(page.getByText("SWK-V-10001")).toBeVisible();
    await expect(page.getByText("Registered").first()).toBeVisible();
    await expect(page.getByRole("button", { name: /New vendor/i })).toBeVisible();
  });

  test("opens vendor detail from the list", async ({ page }) => {
    await page.goto("/vendors");
    await page.getByText("Sentausa Engineering Sdn Bhd").click();

    await expect(page).toHaveURL(new RegExp(`/vendors/${VENDOR.id}$`));
    await expect(page.getByText("SWK-V-10001").first()).toBeVisible();
    await expect(page.getByText("Identity")).toBeVisible();
    await page.getByRole("button", { name: "Contacts" }).click();
    await expect(page.getByText("Ali Buyer")).toBeVisible();
  });

  test("new vendor chooser offers manual entry and invite", async ({ page }) => {
    await page.goto("/vendors/new");

    await expect(page.getByRole("heading", { name: "New Vendor" })).toBeVisible();
    await expect(page.getByRole("button", { name: /Enter manually/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /Invite vendor/i })).toBeEnabled();
    await page.getByRole("button", { name: /Invite vendor/i }).click();
    await expect(page).toHaveURL(/\/onboarding\/invite$/);
  });
});

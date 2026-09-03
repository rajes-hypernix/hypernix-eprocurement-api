import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const QUEUE = [
  {
    id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    code: "ONB-1001",
    name: "Acme Supply Sdn Bhd",
    type: "NonSwec",
    status: "Submitted",
    source: "SelfService",
    createdOnUtc: "2026-07-01T00:00:00Z",
    submittedUtc: "2026-07-02T00:00:00Z",
    openRoundNo: null,
    roundCount: 0,
    invitationId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  },
];

const TEMPLATES = [
  {
    id: "cccccccc-cccc-cccc-cccc-cccccccccccc",
    key: "ONBOARDING_NONSWEC",
    name: "Non-SWEC pack",
    isActive: true,
    questionCount: 2,
  },
];

const INVITE_RESULT = {
  id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  email: "vendor@example.com",
  type: "NonSwec",
  status: "Invited",
  invitedByName: "Root Buyer",
  createdOnUtc: "2026-07-01T00:00:00Z",
  expiresUtc: "2026-07-15T00:00:00Z",
  applicationId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  applicationCode: "ONB-1001",
  magicLink: "http://localhost:5175/onboard?token=demo-token",
  selectedTemplateIds: ["cccccccc-cccc-cccc-cccc-cccccccccccc"],
};

test.describe("Onboarding (buyer)", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...BUYER_PERMS] });
    await installEprocShellMocks(page);
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/applications", QUEUE);
    await mockJsonResponse(page, "**/api/v1/platform/form-templates**", TEMPLATES);
  });

  test("queue lists applications", async ({ page }) => {
    await page.goto("/onboarding");
    await expect(page.getByRole("heading", { name: "Onboarding" })).toBeVisible();
    await expect(page.getByText("ONB-1001")).toBeVisible();
    await expect(page.getByText("Acme Supply Sdn Bhd")).toBeVisible();
  });

  test("invite emails the vendor and does not show the magic link", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/invitations", INVITE_RESULT, {
      method: "POST",
    });

    await page.goto("/onboarding/invite");
    await expect(page.getByRole("heading", { name: "Invite vendor" })).toBeVisible();
    await page.getByLabel(/Vendor email/i).fill("vendor@example.com");
    await page.getByRole("button", { name: /Generate.*send link/i }).click();
    await expect(page.getByText("Invitation sent")).toBeVisible();
    await expect(page.getByText("vendor@example.com")).toBeVisible();
    await expect(page.getByLabel(/Magic link/i)).toHaveCount(0);
  });
});

test.describe("Onboarding portal (anonymous)", () => {
  test("landing resolves token and starts form", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/resolve", {
      id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      code: "ONB-1001",
      status: "InProgress",
      type: "NonSwec",
      name: "",
      email: "vendor@example.com",
      createdOnUtc: "2026-07-01T00:00:00Z",
      submittedUtc: null,
      selectedTemplateIds: [],
      rounds: [],
    });
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/lookups", {
      swec: [],
      countries: [],
      banks: [],
      formTemplates: [],
    });

    await page.goto("/onboard?token=demo-token");
    await expect(page.getByRole("heading", { name: /Welcome.*onboarding/i })).toBeVisible();
    await expect(page.getByText("ONB-1001")).toBeVisible();
    await page.getByRole("button", { name: /Start onboarding/i }).click();
  });

  test("country filters state and city; other countries fall back to text", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/resolve", {
      id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      code: "ONB-1001",
      status: "InProgress",
      type: "NonSwec",
      name: "",
      email: "vendor@example.com",
      createdOnUtc: "2026-07-01T00:00:00Z",
      submittedUtc: null,
      selectedTemplateIds: [],
      rounds: [],
    });
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/lookups", {
      swec: [],
      countries: [
        {
          id: "c-my",
          code: "MY",
          name: "Malaysia",
          states: [
            {
              id: "s-sgr",
              code: "SGR",
              name: "Selangor",
              cities: [
                { id: "ci-pj", name: "Petaling Jaya" },
                { id: "ci-sa", name: "Shah Alam" },
              ],
            },
            {
              id: "s-swk",
              code: "SWK",
              name: "Sarawak",
              cities: [{ id: "ci-kch", name: "Kuching" }],
            },
          ],
        },
        { id: "c-sg", code: "SG", name: "Singapore", states: [] },
      ],
      banks: [],
      formTemplates: [],
    });
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/draft**", {
      id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      code: "ONB-1001",
      status: "InProgress",
      type: "NonSwec",
      name: "",
      registeredName: "",
      registrationNo: "",
      taxId: "",
      email: "vendor@example.com",
      contactName: "",
      contactPhone: "",
      region: "Peninsular",
      state: "",
      city: "",
      countryCode: "MY",
      stateId: null,
      cityId: null,
      categories: [],
      contacts: [],
      addresses: [],
      bankAccounts: [],
      certifications: [],
      financialYears: [],
      documents: [],
      selectedTemplateIds: [],
      answers: [],
    });

    await page.goto("/onboard?token=demo-token");
    await page.getByRole("button", { name: /Start onboarding/i }).click();
    await expect(page.getByRole("heading", { name: /Supplier — onboarding/i })).toBeVisible();

    const country = page.getByLabel("Country", { exact: true });
    const phoneCountry = page.getByLabel("Phone number country");
    const state = page.getByLabel("State / Region");
    const city = page.getByLabel("City");

    await expect(country).toHaveValue("MY");
    await expect(phoneCountry).toHaveValue("MY");
    await state.selectOption({ label: "Selangor" });
    await expect(city).toContainText("Petaling Jaya");
    await expect(city).toContainText("Shah Alam");
    await expect(city).not.toContainText("Kuching");

    await state.selectOption({ label: "Sarawak" });
    await expect(city).toContainText("Kuching");
    await expect(city).not.toContainText("Petaling Jaya");

    await country.selectOption({ label: "Singapore" });
    await expect(state).toHaveAttribute("type", "text");
    await expect(city).toHaveAttribute("type", "text");
    await expect(city).toBeEnabled();
    await expect(phoneCountry).toHaveValue("SG");

    await country.selectOption({ label: "Malaysia" });
    await expect(phoneCountry).toHaveValue("MY");
  });
});

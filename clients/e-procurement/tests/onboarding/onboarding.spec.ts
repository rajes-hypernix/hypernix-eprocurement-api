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
    createdUtc: "2026-07-01T00:00:00Z",
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
  createdUtc: "2026-07-01T00:00:00Z",
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

  test("invite creates magic link", async ({ page }) => {
    await mockJsonResponse(page, "**/api/v1/suppliers/onboarding/invitations", INVITE_RESULT, {
      method: "POST",
    });

    await page.goto("/onboarding/invite");
    await expect(page.getByRole("heading", { name: "Invite vendor" })).toBeVisible();
    await page.getByLabel(/Vendor email/i).fill("vendor@example.com");
    await page.getByRole("button", { name: /Generate.*send link/i }).click();
    await expect(page.getByText("Invitation sent")).toBeVisible();
    await expect(page.getByLabel(/Magic link/i)).toHaveValue(/\/onboard\?token=/);
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
      createdUtc: "2026-07-01T00:00:00Z",
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
});

import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const RFQ_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

const EVAL_PERMS = [
  ...BUYER_PERMS,
  "Permissions.Sourcing.Evaluation.ViewOpening",
  "Permissions.Sourcing.Evaluation.ViewTechnical",
  "Permissions.Sourcing.Evaluation.OpenTechnical",
  "Permissions.Sourcing.Evaluation.OpenCommercial",
  "Permissions.Sourcing.Evaluation.Score",
  "Permissions.Sourcing.Evaluation.FinalizeTechnical",
  "Permissions.Sourcing.Rfqs.View",
] as const;

const RFQ_LIST = [
  {
    id: RFQ_ID,
    code: "RFQ-2026-0008",
    title: "Office furniture",
    envelope: "Dual",
    status: "Closed",
    currency: "MYR",
    closesUtc: "2026-08-01T00:00:00Z",
    invitedCount: 3,
    lineCount: 2,
    bidCount: 2,
  },
  {
    id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
    code: "RFQ-2026-0009",
    title: "Stationery pack",
    envelope: "Single",
    status: "Evaluation",
    currency: "MYR",
    closesUtc: "2026-08-02T00:00:00Z",
    invitedCount: 1,
    lineCount: 1,
    bidCount: 1,
  },
];

const OPENING = {
  rfqId: RFQ_ID,
  code: "RFQ-2026-0008",
  title: "Office furniture",
  envelope: "Dual",
  rfqStatus: "Closed",
  technicalOpened: false,
  techFinalized: false,
  commercialOpened: false,
  invitedCount: 3,
  submittedBidCount: 2,
  evaluators: [{ id: TEST_USER.sub, name: "Root Buyer" }],
  canOpenTechnical: true,
  canOpenCommercial: false,
};

test.describe("Bid Openings", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...EVAL_PERMS] });
    await installEprocShellMocks(page, EVAL_PERMS);
    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}/bid-opening`, OPENING);
    await mockJsonResponse(page, "**/api/v1/sourcing/rfqs", RFQ_LIST);
  });

  test("lists closed RFQs with search and envelope filters", async ({ page }) => {
    await page.goto("/openings");
    await expect(page.getByRole("heading", { name: "Bid Openings" })).toBeVisible();
    await expect(page.getByText("RFQ-2026-0008")).toBeVisible();
    await expect(page.getByText("RFQ-2026-0009")).toBeVisible();

    const search = page.getByLabel("Search bid openings");
    await search.fill("zzz-no-match");
    await expect(page.getByText("No RFQs match these filters.")).toBeVisible();

    await page.getByRole("button", { name: "Reset" }).click();
    await expect(page.getByText("RFQ-2026-0008")).toBeVisible();

    await page.getByLabel("Envelope").selectOption("Dual");
    await expect(page.getByText("RFQ-2026-0008")).toBeVisible();
    await expect(page.getByText("RFQ-2026-0009")).toHaveCount(0);
  });

  test("opens envelope ceremony with assigned evaluators and unseal confirm", async ({ page }) => {
    await page.goto("/openings");
    await page.getByText("RFQ-2026-0008").click();
    await expect(page.getByRole("heading", { name: /Open Sealed Bids — RFQ-2026-0008/ })).toBeVisible();
    await expect(page.getByText("Technical envelope", { exact: true })).toBeVisible();
    await expect(page.getByText("Commercial envelope", { exact: true })).toBeVisible();
    await expect(page.getByRole("strong")).toHaveText("Root Buyer");
    await expect(page.getByRole("button", { name: "Open commercial envelope" })).toBeDisabled();

    await page.getByRole("button", { name: "Open technical envelope" }).click();
    await expect(page.getByRole("heading", { name: "Authorise — Open Technical Envelope" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Unseal technical" })).toBeVisible();
  });

  test("shows scoring matrix after technical is opened", async ({ page }) => {
    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}/bid-opening`, {
      ...OPENING,
      technicalOpened: true,
    });
    await mockJsonResponse(page, `**/api/v1/sourcing/rfqs/${RFQ_ID}/technical-eval`, [
      {
        vendorId: "11111111-1111-1111-1111-111111111111",
        vendorName: "Acme Supplies",
        vendorCode: "V-1",
        alias: "Bidder A",
        masked: false,
        committeeScore: null,
        pass: false,
        scores: [],
      },
    ]);

    await page.goto(`/openings/${RFQ_ID}/score`);
    await expect(page.getByRole("heading", { name: "Technical Evaluation — RFQ-2026-0008" })).toBeVisible();
    await expect(page.getByText("Scoring matrix")).toBeVisible();
    await expect(page.getByText("Acme Supplies")).toBeVisible();
    await expect(page.getByText("Weight 35%")).toBeVisible();
  });
});

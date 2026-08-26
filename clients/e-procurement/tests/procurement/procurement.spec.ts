import { expect, test } from "@playwright/test";
import { mockJsonResponse } from "../helpers/api-mocks";
import { seedAuthedSession, TEST_USER } from "../helpers/auth-seed";
import { BUYER_PERMS, installEprocShellMocks } from "../helpers/shell-mocks";

const PO_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const ASN_ID = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
const INV_ID = "cccccccc-cccc-cccc-cccc-cccccccccccc";
const VENDOR_ID = "11111111-1111-1111-1111-111111111111";

const PO_LIST = [
  {
    id: PO_ID,
    code: "PO-2026-0001",
    rfqId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
    vendorId: VENDOR_ID,
    vendorName: "Acme Supplies",
    status: "Issued",
    currency: "MYR",
    totalValue: 1500,
    receivedQty: 0,
    totalQty: 2,
    createdUtc: "2026-07-01T00:00:00Z",
  },
];

const PO_DETAIL = {
  ...PO_LIST[0],
  awardId: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
  sourceKind: "FromAward",
  updatedUtc: "2026-07-01T00:00:00Z",
  lines: [
    {
      id: "line-1",
      itemCode: "PUMP-01",
      description: "Centrifugal pump",
      uom: "EA",
      qty: 2,
      unitPrice: 750,
      receivedQty: 0,
      invoicedQty: 0,
      rfqLineCode: "L1",
      priceConfirmed: true,
      lineTotal: 1500,
    },
  ],
};

const ASN = {
  id: ASN_ID,
  code: "ASN-2026-0001",
  poId: PO_ID,
  poCode: "PO-2026-0001",
  vendorName: "Acme Supplies",
  status: "InTransit",
  carrier: "DHL",
  trackingNo: "DHL123",
  shippedDate: "2026-07-10",
  expectedDate: "2026-07-12",
  lines: [{ id: "asn-line-1", itemCode: "PUMP-01", description: "Centrifugal pump", uom: "EA", shippedQty: 2, lotNo: null }],
  createdUtc: "2026-07-10T00:00:00Z",
};

const INVOICE = {
  id: INV_ID,
  code: "INV-2026-0001",
  poId: PO_ID,
  grnId: null,
  invoiceNo: "V-INV-88",
  status: "Submitted",
  invoiceDate: "2026-07-15",
  subtotal: 1500,
  sstAmount: 90,
  whtAmount: 0,
  total: 1590,
  sstRate: 6,
  whtRate: 0,
  exceptionReason: null,
  lines: [{ id: "inv-line-1", itemCode: "PUMP-01", qty: 2, unitPrice: 750, lineTotal: 1500 }],
  createdUtc: "2026-07-15T00:00:00Z",
};

const PROC_PERMS = [
  ...BUYER_PERMS,
  "Permissions.Procurement.PurchaseOrders.View",
  "Permissions.Procurement.PurchaseOrders.Issue",
  "Permissions.Procurement.Deliveries.View",
  "Permissions.Procurement.Deliveries.Receive",
  "Permissions.Procurement.Invoices.View",
  "Permissions.Procurement.Invoices.Approve",
] as const;

test.describe("Procurement", () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthedSession(page, { ...TEST_USER, permissions: [...PROC_PERMS] });
    await installEprocShellMocks(page, PROC_PERMS);

    await page.route("**/api/v1/procurement/purchase-orders**", async (route) => {
      const url = route.request().url();
      const detail = /\/purchase-orders\/([0-9a-f-]{36})/i.exec(url);
      const body = detail ? PO_DETAIL : PO_LIST;
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
    });

    await page.route("**/api/v1/procurement/asns**", async (route) => {
      const url = route.request().url();
      const detail = /\/asns\/([0-9a-f-]{36})/i.exec(url);
      const body = detail ? ASN : [ASN];
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
    });

    await mockJsonResponse(page, "**/api/v1/procurement/grns/**", null);

    await page.route("**/api/v1/procurement/invoices**", async (route) => {
      const url = route.request().url();
      const detail = /\/invoices\/([0-9a-f-]{36})/i.exec(url);
      const body = detail ? INVOICE : [INVOICE];
      await route.fulfill({
        status: 200,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
    });
  });

  test("lists purchase orders", async ({ page }) => {
    await page.goto("/pos");
    await expect(page.getByRole("heading", { name: "Purchase Orders" })).toBeVisible();
    await expect(page.getByText("PO-2026-0001")).toBeVisible();
    await expect(page.getByText("Acme Supplies")).toBeVisible();
    await expect(page.getByText("Issued").first()).toBeVisible();
  });

  test("opens PO record from the list and hops to fulfilment", async ({ page }) => {
    await page.goto("/pos");
    await page.getByText("PO-2026-0001").click();
    await expect(page).toHaveURL(new RegExp(`/pos/${PO_ID}$`));
    await expect(page.getByText("PUMP-01")).toBeVisible();
    await expect(page.getByText("Centrifugal pump")).toBeVisible();
    await expect(page.getByText("Acme Supplies").first()).toBeVisible();
    await page.getByRole("button", { name: /Fulfilment/i }).click();
    await expect(page).toHaveURL(new RegExp(`/pos/detail/${PO_ID}$`));
    await expect(page.getByRole("button", { name: /PO form/i })).toBeVisible();
  });

  test("lists deliveries and opens ASN detail", async ({ page }) => {
    await page.goto("/deliveries");
    await expect(page.getByRole("heading", { name: "Deliveries" })).toBeVisible();
    await expect(page.getByText("ASN-2026-0001")).toBeVisible();
    await expect(page.getByText("PO-2026-0001").first()).toBeVisible();
    await page.getByText("ASN-2026-0001").click();
    await expect(page).toHaveURL(new RegExp(`/deliveries/asn/${ASN_ID}$`));
    await expect(page.getByText("DHL123")).toBeVisible();
    await expect(page.getByText("Centrifugal pump")).toBeVisible();
    await expect(page.getByRole("button", { name: /Receive goods/i })).toBeVisible();
  });

  test("lists invoices and opens detail", async ({ page }) => {
    await page.goto("/invoices");
    await expect(page.getByRole("heading", { name: "Invoices" })).toBeVisible();
    await expect(page.getByText("V-INV-88")).toBeVisible();
    await page.getByText("V-INV-88").click();
    await expect(page).toHaveURL(new RegExp(`/invoices/${INV_ID}$`));
    await expect(page.getByRole("button", { name: /Approve invoice/i })).toBeVisible();
  });
});

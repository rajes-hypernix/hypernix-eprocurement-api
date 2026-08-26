import { apiFetch } from "@/lib/api-client";
import { ApiPaths } from "@/api/types";

const ROOT = ApiPaths.procurement;

// ---------------------------------------------------------------------------
// Purchase Orders
// ---------------------------------------------------------------------------

export type PoLineDto = {
  id: string;
  itemCode: string;
  description: string;
  uom: string;
  qty: number;
  unitPrice: number;
  receivedQty: number;
  invoicedQty: number;
  rfqLineCode?: string | null;
  sourcePrLineId?: string | null;
  taxCodeId?: string | null;
  priceConfirmed: boolean;
  lineTotal: number;
  openQty?: number;
};

export type PurchaseOrderDto = {
  id: string;
  code: string;
  awardId?: string | null;
  rfqId?: string | null;
  sourcePrId?: string | null;
  sourceKind: string;
  vendorId: string;
  status: string;
  currency: string;
  shipToLocationId?: string | null;
  shipToAddressId?: string | null;
  shipToAdhoc?: string | null;
  incotermCode?: string | null;
  incotermSuffix?: string | null;
  incotermId?: string | null;
  memo?: string | null;
  vendorRef?: string | null;
  requiredDate?: string | null;
  deliveryDate?: string | null;
  verifiedUtc?: string | null;
  issuedUtc?: string | null;
  totalValue: number;
  lines: PoLineDto[];
  createdUtc: string;
  updatedUtc: string;
  vendorName?: string;
};

export type PurchaseOrderListItemDto = {
  id: string;
  code: string;
  rfqId?: string | null;
  sourcePrId?: string | null;
  sourceKind: string;
  vendorId: string;
  status: string;
  currency: string;
  totalValue: number;
  createdUtc: string;
  vendorName?: string;
  receivedQty?: number;
  totalQty?: number;
  awardId?: string | null;
};

export type CreatePoFromRequisitionLineInput = {
  prLineId: string;
  qty: number;
  unitPrice: number;
  priceConfirmed?: boolean;
};

export type CreateStandalonePoLineInput = {
  itemCode: string;
  description: string;
  uom: string;
  qty: number;
  unitPrice: number;
};

export function createPurchaseOrdersFromAward(rfqId: string): Promise<string[]> {
  return apiFetch(`${ROOT}/purchase-orders/from-award`, {
    method: "POST",
    body: JSON.stringify({ rfqId }),
  });
}

export function createPurchaseOrderFromRequisition(body: {
  prId: string;
  vendorId: string;
  currency: string;
  lines: CreatePoFromRequisitionLineInput[];
}): Promise<string> {
  return apiFetch(`${ROOT}/purchase-orders/from-requisition`, { method: "POST", body: JSON.stringify(body) });
}

export function createStandalonePurchaseOrder(body: {
  vendorId: string;
  currency: string;
  lines: CreateStandalonePoLineInput[];
}): Promise<string> {
  return apiFetch(`${ROOT}/purchase-orders/standalone`, { method: "POST", body: JSON.stringify(body) });
}

export function getPurchaseOrder(id: string): Promise<PurchaseOrderDto | undefined> {
  return apiFetch(`${ROOT}/purchase-orders/${id}`);
}

export function listPurchaseOrders(): Promise<PurchaseOrderListItemDto[]> {
  return apiFetch(`${ROOT}/purchase-orders`);
}

export function verifyPurchaseOrder(id: string): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/verify`, { method: "POST", body: JSON.stringify({}) });
}

export function reopenPurchaseOrderDraft(id: string): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/reopen-draft`, { method: "POST", body: JSON.stringify({}) });
}

export function issuePurchaseOrder(id: string): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/issue`, { method: "POST", body: JSON.stringify({}) });
}

export function acknowledgePurchaseOrder(id: string): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/acknowledge`, { method: "POST", body: JSON.stringify({}) });
}

export function cancelPurchaseOrder(id: string, reason?: string | null): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/cancel`, { method: "POST", body: JSON.stringify({ reason }) });
}

export function closePurchaseOrder(id: string): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/close`, { method: "POST", body: JSON.stringify({}) });
}

export function setPurchaseOrderShipTo(
  id: string,
  body: { locationId?: string | null; addressId?: string | null; adhoc?: string | null },
): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${id}/ship-to`, { method: "PUT", body: JSON.stringify(body) });
}

export function updatePurchaseOrderLine(
  poId: string,
  lineId: string,
  body: { unitPrice?: number | null; priceConfirmed: boolean },
): Promise<PurchaseOrderDto> {
  return apiFetch(`${ROOT}/purchase-orders/${poId}/lines/${lineId}`, { method: "PUT", body: JSON.stringify(body) });
}

// ---------------------------------------------------------------------------
// ASN (shipping notices)
// ---------------------------------------------------------------------------

export type AsnLineDto = {
  id: string;
  itemCode: string;
  shippedQty: number;
  lotNo?: string | null;
  description?: string | null;
  uom?: string | null;
};

export type AsnDto = {
  id: string;
  code: string;
  poId: string;
  status: string;
  carrier: string;
  trackingNo: string;
  shippedDate?: string | null;
  expectedDate?: string | null;
  lines: AsnLineDto[];
  createdUtc: string;
  poCode?: string | null;
  vendorName?: string | null;
};

export type AsnLineInput = { itemCode: string; shippedQty: number; lotNo?: string | null };

export type CreateAsnRequest = {
  poId: string;
  carrier: string;
  trackingNo: string;
  shippedDate?: string | null;
  expectedDate?: string | null;
  lines: AsnLineInput[];
};

export function createAsn(body: CreateAsnRequest): Promise<AsnDto> {
  return apiFetch(`${ROOT}/asns`, { method: "POST", body: JSON.stringify(body) });
}

export function listAsns(poId?: string): Promise<AsnDto[]> {
  const qs = poId ? `?poId=${encodeURIComponent(poId)}` : "";
  return apiFetch(`${ROOT}/asns${qs}`);
}

export function getAsn(id: string): Promise<AsnDto | undefined> {
  return apiFetch(`${ROOT}/asns/${id}`);
}

// ---------------------------------------------------------------------------
// GRN (goods receipt)
// ---------------------------------------------------------------------------

export type GrnLineDto = {
  id: string;
  itemCode: string;
  expectedQty: number;
  receivedQty: number;
  condition: string;
};

export type GrnDto = {
  id: string;
  code: string;
  asnId: string;
  poId: string;
  lines: GrnLineDto[];
  createdUtc: string;
};

export type ReceiveLineInput = { itemCode: string; receivedQty: number };

export function receiveAsn(asnId: string, lines: ReceiveLineInput[]): Promise<GrnDto> {
  return apiFetch(`${ROOT}/grns/receive`, { method: "POST", body: JSON.stringify({ asnId, lines }) });
}

export function getGrnByAsn(asnId: string): Promise<GrnDto | undefined> {
  return apiFetch(`${ROOT}/grns/by-asn/${asnId}`);
}

// ---------------------------------------------------------------------------
// Invoices
// ---------------------------------------------------------------------------

export type InvoiceLineDto = { id: string; itemCode: string; qty: number; unitPrice: number; lineTotal: number };

export type InvoiceDto = {
  id: string;
  code: string;
  poId: string;
  grnId?: string | null;
  invoiceNo: string;
  status: string;
  invoiceDate?: string | null;
  subtotal: number;
  sstAmount: number;
  whtAmount: number;
  total: number;
  sstRate: number;
  whtRate: number;
  exceptionReason?: string | null;
  lines: InvoiceLineDto[];
  createdUtc: string;
};

export type InvoiceLineInput = { itemCode: string; qty: number; unitPrice: number };

export type SubmitInvoiceRequest = {
  poId: string;
  invoiceNo: string;
  date?: string | null;
  whtRate: number;
  lines: InvoiceLineInput[];
};

export function submitInvoice(body: SubmitInvoiceRequest): Promise<InvoiceDto> {
  return apiFetch(`${ROOT}/invoices`, { method: "POST", body: JSON.stringify(body) });
}

export function getInvoice(id: string): Promise<InvoiceDto | undefined> {
  return apiFetch(`${ROOT}/invoices/${id}`);
}

export function listInvoices(): Promise<InvoiceDto[]> {
  return apiFetch(`${ROOT}/invoices`);
}

export function approveInvoice(id: string): Promise<InvoiceDto> {
  return apiFetch(`${ROOT}/invoices/${id}/approve`, { method: "POST", body: JSON.stringify({}) });
}

export function resolveInvoiceException(id: string): Promise<InvoiceDto> {
  return apiFetch(`${ROOT}/invoices/${id}/resolve-exception`, { method: "POST", body: JSON.stringify({}) });
}

// ---------------------------------------------------------------------------
// Statements of Account
// ---------------------------------------------------------------------------

export type AgingDto = { current: number; d30: number; d60: number; d90: number };

export type LedgerEntryDto = {
  date: string;
  reference: string;
  type: string;
  memo?: number | null;
  debit: number;
  credit: number;
  balance: number;
};

export type StatementSummaryDto = {
  vendorId: string;
  vendorName: string;
  invoiced: number;
  paid: number;
  balance: number;
  grni: number;
};

export type StatementDetailDto = StatementSummaryDto & {
  aging: AgingDto;
  ledger: LedgerEntryDto[];
};

export function listStatements(): Promise<StatementSummaryDto[]> {
  return apiFetch(`${ROOT}/statements`);
}

export function getStatement(vendorId: string): Promise<StatementDetailDto | undefined> {
  return apiFetch(`${ROOT}/statements/${vendorId}`);
}

export function getMyStatement(): Promise<StatementDetailDto> {
  return apiFetch(`${ROOT}/statements/mine`);
}

import { apiFetch } from "@/lib/api-client";
import { ApiPaths } from "@/api/types";

export type PurchaseOrderListItemDto = {
  id: string;
  code: string;
  rfqId: string;
  vendorId: string;
  status: string;
  currency: string;
  totalValue: number;
  createdUtc: string;
};

const ROOT = ApiPaths.procurement;

export function listPurchaseOrders(): Promise<PurchaseOrderListItemDto[]> {
  return apiFetch<PurchaseOrderListItemDto[]>(`${ROOT}/purchase-orders`);
}

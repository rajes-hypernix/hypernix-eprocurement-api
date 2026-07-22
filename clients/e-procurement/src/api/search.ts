import { ApiPaths, toQuery } from "@/api/types";
import { apiFetch } from "@/lib/api-client";

export type SearchHitType =
  | "Vendor"
  | "Requisition"
  | "Rfq"
  | "PurchaseOrder"
  | "Invoice"
  | "Asn"
  | "Statement";

export type SearchHitDto = {
  type: SearchHitType;
  id: string;
  code: string;
  title: string;
  subtitle?: string | null;
};

export async function searchGlobal(q: string): Promise<SearchHitDto[]> {
  const trimmed = q.trim();
  if (trimmed.length < 2) return [];
  return (await apiFetch<SearchHitDto[]>(`${ApiPaths.search}${toQuery({ q: trimmed })}`)) ?? [];
}

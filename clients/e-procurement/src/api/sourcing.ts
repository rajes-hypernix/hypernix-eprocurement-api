import { apiFetch } from "@/lib/api-client";
import { ApiPaths } from "@/api/types";

export type RequisitionListItemDto = {
  id: string;
  code: string;
  requestor: string;
  department: string;
  headerStatus: string;
  submitted: boolean;
  lineCount: number;
  createdUtc: string;
};

export type RfqListItemDto = {
  id: string;
  code: string;
  title: string;
  envelope: string;
  status: string;
  currency: string;
  closesUtc?: string | null;
  invitedCount: number;
  lineCount: number;
};

const ROOT = ApiPaths.sourcing;

export function listRequisitions(): Promise<RequisitionListItemDto[]> {
  return apiFetch<RequisitionListItemDto[]>(`${ROOT}/requisitions`);
}

export function listRfqs(): Promise<RfqListItemDto[]> {
  return apiFetch<RfqListItemDto[]>(`${ROOT}/rfqs`);
}

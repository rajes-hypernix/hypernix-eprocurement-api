import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery } from "@/api/types";

const ROOT = ApiPaths.platform;

/** Record types the saved-view field registry currently understands. */
export const VIEW_RECORD_TYPES = ["Requisition", "Rfq", "PurchaseOrder", "Vendor"] as const;
export type ViewRecordType = (typeof VIEW_RECORD_TYPES)[number];

/** Filter operators the in-memory executor understands — matches `SavedViewFilterExecutor`. */
export type ViewOperator =
  | "Eq"
  | "Neq"
  | "In"
  | "Contains"
  | "StartsWith"
  | "IsEmpty"
  | "IsNotEmpty"
  | "Gte"
  | "Lte"
  | "Between";

export type ViewFieldDataType =
  | "Code"
  | "Text"
  | "Enum"
  | "Date"
  | "Instant"
  | "Money"
  | "Number"
  | "Bool"
  | "Tags";

export type ViewSortDirection = "Asc" | "Desc";

export type SavedViewFilterDto = {
  fieldKey: string;
  operator: string;
  groupIndex: number;
  value: string | null;
  value2?: string | null;
  sort: number;
};

export type SavedViewColumnDto = {
  fieldKey: string;
  label?: string | null;
  sort: number;
  sortDirection?: string | null;
};

export type SavedViewDto = {
  id: string;
  code: string;
  name: string;
  recordType: string;
  ownerUserId?: string | null;
  isShared: boolean;
  isSystem: boolean;
  filters: SavedViewFilterDto[];
  columns: SavedViewColumnDto[];
  createdOnUtc: string;
  lastModifiedOnUtc?: string | null;
};

export type ViewFieldDto = {
  fieldKey: string;
  kind: string;
  label: string;
  dataType: string;
};

/** Rows are keyed by field key (PascalCase from the server) and always include an "Id". */
export type ViewRunResult = {
  rows: Record<string, unknown>[];
  totalCount: number;
  page: number;
  size: number;
};

export type SaveViewRequest = {
  name: string;
  recordType: string;
  filters: SavedViewFilterDto[];
  columns: SavedViewColumnDto[];
};

export function listViews(recordType?: string): Promise<SavedViewDto[]> {
  return apiFetch(`${ROOT}/views${toQuery({ recordType })}`);
}

export function getViewFields(recordType: string): Promise<ViewFieldDto[]> {
  return apiFetch(`${ROOT}/views/fields${toQuery({ recordType })}`);
}

export function createView(body: SaveViewRequest): Promise<SavedViewDto> {
  return apiFetch(`${ROOT}/views`, { method: "POST", body: JSON.stringify(body) });
}

export function updateView(id: string, body: SaveViewRequest): Promise<SavedViewDto> {
  return apiFetch(`${ROOT}/views/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(body) });
}

export function deleteView(id: string): Promise<void> {
  return apiFetch(`${ROOT}/views/${encodeURIComponent(id)}`, { method: "DELETE" });
}

export function shareView(id: string, isShared: boolean): Promise<SavedViewDto> {
  return apiFetch(`${ROOT}/views/${encodeURIComponent(id)}/share`, {
    method: "POST",
    body: JSON.stringify({ isShared }),
  });
}

export function runView(id: string, page = 1, size = 50): Promise<ViewRunResult> {
  return apiFetch(`${ROOT}/views/${encodeURIComponent(id)}/run${toQuery({ page, size })}`);
}

/** Every row from RunSavedView carries "Id" — but the server may cast it PascalCase either way. */
export function rowId(row: Record<string, unknown>): string {
  const v = row.Id ?? row.id;
  return v === null || v === undefined ? "" : String(v);
}

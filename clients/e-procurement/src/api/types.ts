/** Shared API path prefixes for the eProcure modules. */
export const ApiPaths = {
  platform: "/api/v1/platform",
  suppliers: "/api/v1/suppliers",
  sourcing: "/api/v1/sourcing",
  procurement: "/api/v1/procurement",
  notifications: "/api/v1/notifications",
  identity: "/api/v1/identity",
  search: "/api/v1/search",
} as const;

export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
};

export function toQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    qs.set(key, String(value));
  }
  const s = qs.toString();
  return s ? `?${s}` : "";
}

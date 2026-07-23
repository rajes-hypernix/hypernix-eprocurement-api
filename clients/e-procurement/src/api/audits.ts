import { apiFetch } from "@/lib/api-client";
import { ApiPaths, toQuery, type PagedResponse } from "@/api/types";

export type AuditEventType = "None" | "EntityChange" | "Security" | "Activity" | "Exception";

export type AuditSeverity =
  | "None"
  | "Trace"
  | "Debug"
  | "Information"
  | "Warning"
  | "Error"
  | "Critical";

export const AUDIT_EVENT_TYPES: AuditEventType[] = [
  "EntityChange",
  "Security",
  "Activity",
  "Exception",
];

export type AuditSummaryDto = {
  id: string;
  occurredAtUtc: string;
  eventType: AuditEventType;
  severity: AuditSeverity;
  tenantId?: string | null;
  userId?: string | null;
  userName?: string | null;
  traceId?: string | null;
  correlationId?: string | null;
  requestId?: string | null;
  source?: string | null;
  tags: string | number;
};

export type AuditDetailDto = AuditSummaryDto & {
  receivedAtUtc: string;
  spanId?: string | null;
  payload: unknown;
};

export type ListAuditsParams = {
  pageNumber?: number;
  pageSize?: number;
  sort?: string;
  fromUtc?: string;
  toUtc?: string;
  tenantId?: string;
  userId?: string;
  eventType?: AuditEventType;
  severity?: AuditSeverity;
  source?: string;
  correlationId?: string;
  traceId?: string;
  search?: string;
};

const ROOT = ApiPaths.audits;

const EVENT_TYPE_BY_INT: readonly AuditEventType[] = [
  "None",
  "EntityChange",
  "Security",
  "Activity",
  "Exception",
];

const SEVERITY_BY_INT: readonly AuditSeverity[] = [
  "None",
  "Trace",
  "Debug",
  "Information",
  "Warning",
  "Error",
  "Critical",
];

function coerceEventType(raw: unknown): AuditEventType {
  if (typeof raw === "string") return raw as AuditEventType;
  if (typeof raw === "number") return EVENT_TYPE_BY_INT[raw] ?? "None";
  return "None";
}

function coerceSeverity(raw: unknown): AuditSeverity {
  if (typeof raw === "string") return raw as AuditSeverity;
  if (typeof raw === "number") return SEVERITY_BY_INT[raw] ?? "None";
  return "None";
}

function normalizeSummary<T extends AuditSummaryDto>(dto: T): T {
  return {
    ...dto,
    eventType: coerceEventType((dto as { eventType: unknown }).eventType),
    severity: coerceSeverity((dto as { severity: unknown }).severity),
  };
}

export async function listAudits(params: ListAuditsParams = {}): Promise<PagedResponse<AuditSummaryDto>> {
  const page = await apiFetch<PagedResponse<AuditSummaryDto>>(
    `${ROOT}/${toQuery({
      PageNumber: params.pageNumber ?? 1,
      PageSize: params.pageSize ?? 25,
      Sort: params.sort,
      FromUtc: params.fromUtc,
      ToUtc: params.toUtc,
      TenantId: params.tenantId,
      UserId: params.userId,
      EventType: params.eventType,
      Severity: params.severity,
      Source: params.source,
      CorrelationId: params.correlationId,
      TraceId: params.traceId,
      Search: params.search?.trim(),
    })}`,
  );
  return { ...page, items: page.items.map(normalizeSummary) };
}

export async function getAudit(id: string): Promise<AuditDetailDto> {
  const dto = await apiFetch<AuditDetailDto>(`${ROOT}/${encodeURIComponent(id)}`);
  return normalizeSummary(dto);
}

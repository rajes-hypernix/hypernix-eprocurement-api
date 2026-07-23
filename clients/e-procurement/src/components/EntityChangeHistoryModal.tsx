import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { getEntityChangeHistory, type AuditDetailDto } from "@/api/audits";
import { Modal, Spinner, EmptyState, Notice } from "@/components/ui";
import { dateTimeMY } from "@/lib/format";

type PropertyChange = {
  name?: string;
  Name?: string;
  oldValue?: unknown;
  OldValue?: unknown;
  newValue?: unknown;
  NewValue?: unknown;
};

type EntityChangePayload = {
  operation?: string | number;
  Operation?: string | number;
  changes?: PropertyChange[];
  Changes?: PropertyChange[];
};

/** Audit stamp noise — keep business field changes front-and-centre. */
const NOISE_FIELDS = new Set([
  "CreatedOnUtc",
  "CreatedBy",
  "LastModifiedOnUtc",
  "LastModifiedBy",
  "DeletedOnUtc",
  "DeletedBy",
  "IsDeleted",
  "DomainEvents",
]);

type ParsedChange = { name: string; oldValue: string; newValue: string };
type ParsedEvent = {
  operation: string;
  actionLabel: string;
  badgeClass: string;
  changes: ParsedChange[];
  businessChanges: ParsedChange[];
};

function fmtVal(v: unknown): string {
  if (v === null || v === undefined) return "—";
  if (typeof v === "boolean") return v ? "true" : "false";
  if (typeof v === "string") return v.trim() === "" ? "—" : v;
  if (typeof v === "object") return JSON.stringify(v);
  return String(v);
}

function operationMeta(raw: unknown): { operation: string; actionLabel: string; badgeClass: string } {
  const op =
    typeof raw === "string"
      ? raw
      : raw === 1
        ? "Insert"
        : raw === 2
          ? "Update"
          : raw === 3
            ? "Delete"
            : raw === 4
              ? "SoftDelete"
              : raw === 5
                ? "Restore"
                : "Change";

  switch (op) {
    case "Insert":
      return { operation: op, actionLabel: "Created", badgeClass: "badge b-green" };
    case "Update":
      return { operation: op, actionLabel: "Updated", badgeClass: "badge b-blue" };
    case "Delete":
    case "SoftDelete":
      return { operation: op, actionLabel: op === "SoftDelete" ? "Soft deleted" : "Deleted", badgeClass: "badge b-red" };
    case "Restore":
      return { operation: op, actionLabel: "Restored", badgeClass: "badge b-teal" };
    default:
      return { operation: op, actionLabel: op, badgeClass: "badge b-grey" };
  }
}

function parseEvent(payload: unknown): ParsedEvent {
  const p = (payload ?? {}) as EntityChangePayload;
  const meta = operationMeta(p.operation ?? p.Operation);
  const list = p.changes ?? p.Changes ?? [];
  const changes = list.map((c) => ({
    name: String(c.name ?? c.Name ?? "Field"),
    oldValue: fmtVal(c.oldValue ?? c.OldValue),
    newValue: fmtVal(c.newValue ?? c.NewValue),
  }));
  const businessChanges = changes.filter((c) => !NOISE_FIELDS.has(c.name));
  return { ...meta, changes, businessChanges };
}

function ChangeLines({ changes, emptyHint }: { changes: ParsedChange[]; emptyHint: string }) {
  if (changes.length === 0) {
    return <p className="hint" style={{ margin: "6px 0 0" }}>{emptyHint}</p>;
  }
  return (
    <ul style={{ margin: "8px 0 0", padding: 0, listStyle: "none", display: "grid", gap: 6 }}>
      {changes.map((c) => (
        <li
          key={`${c.name}-${c.oldValue}-${c.newValue}`}
          style={{
            fontSize: 12.5,
            lineHeight: 1.45,
            padding: "6px 8px",
            background: "#f8f6f0",
            border: "1px solid var(--line)",
          }}
        >
          <div style={{ fontWeight: 600, marginBottom: 2 }}>{c.name}</div>
          <div className="hint">
            <span style={{ textDecoration: c.oldValue === "—" ? undefined : "line-through" }}>{c.oldValue}</span>
            <span style={{ margin: "0 6px", color: "var(--muted)" }}>→</span>
            <span style={{ color: "var(--ink)", fontWeight: 500 }}>{c.newValue}</span>
          </div>
        </li>
      ))}
    </ul>
  );
}

/**
 * Loads Auditing EntityChange history only when opened (standalone API).
 * Do not call listAudits from domain list pages.
 */
export function EntityChangeHistoryModal({
  title,
  entityId,
  onClose,
}: {
  title: string;
  entityId: string;
  onClose: () => void;
}) {
  const historyQuery = useQuery({
    queryKey: ["entity-change-history", entityId],
    queryFn: () => getEntityChangeHistory(entityId, 50),
    enabled: !!entityId,
  });

  const rows = useMemo(() => {
    const items = historyQuery.data ?? [];
    return items.map((detail: AuditDetailDto) => ({
      detail,
      parsed: parseEvent(detail.payload),
    }));
  }, [historyQuery.data]);

  return (
    <Modal
      title={`History · ${title}`}
      icon="clock"
      footer={
        <button type="button" className="btn btn-out" onClick={onClose}>
          Close
        </button>
      }
    >
      <p className="hint" style={{ marginTop: 0 }}>
        Loaded on demand from Auditing — not part of the list API. Shows created / updated / deleted and
        each field&apos;s old → new value.
      </p>

      {historyQuery.isPending ? <Spinner label="Loading history…" /> : null}
      {historyQuery.isError ? <Notice tone="error">Could not load history.</Notice> : null}

      {!historyQuery.isPending && rows.length === 0 ? (
        <EmptyState icon="clock">No change history for this record yet.</EmptyState>
      ) : null}

      {rows.length > 0 ? (
        <div style={{ display: "grid", gap: 10, maxHeight: "55vh", overflow: "auto" }}>
          {rows.map(({ detail, parsed }) => (
            <div
              key={detail.id}
              style={{
                border: "1px solid var(--line)",
                padding: "10px 12px",
                background: "#fff",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                <span className={parsed.badgeClass}>{parsed.actionLabel}</span>
                <span style={{ fontSize: 13, fontWeight: 500 }}>{dateTimeMY(detail.occurredAtUtc)}</span>
                <span className="hint" style={{ marginLeft: "auto" }}>
                  {detail.userName ?? detail.userId ?? "—"}
                </span>
              </div>

              <ChangeLines
                changes={parsed.businessChanges.length > 0 ? parsed.businessChanges : parsed.changes}
                emptyHint={
                  parsed.operation === "Insert"
                    ? "Record created (no field deltas)."
                    : "No field values changed in this event."
                }
              />
            </div>
          ))}
        </div>
      ) : null}
    </Modal>
  );
}

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
  entityName?: string;
  EntityName?: string;
  table?: string;
  Table?: string;
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

/** Identity / AspNet internals — never show these labels or hash values to admins. */
const USER_HIDDEN_FIELDS = new Set([
  ...NOISE_FIELDS,
  "PasswordHash",
  "SecurityStamp",
  "ConcurrencyStamp",
  "NormalizedUserName",
  "NormalizedEmail",
  "RefreshToken",
  "RefreshTokenExpiryTime",
  "ObjectId",
  "AccessFailedCount",
  "LockoutEnd",
  "LockoutEnabled",
  "TwoFactorEnabled",
  "PhoneNumberConfirmed",
  "ImageUrl",
  "VendorId",
  "PasswordHistories",
  "Id",
  "UserId",
  "RoleId",
  "ChangedAtUtc",
  "PasswordHashSnapshot",
]);

const USER_FIELD_LABELS: Record<string, string> = {
  FirstName: "First name",
  LastName: "Last name",
  UserName: "Username",
  Email: "Email",
  PhoneNumber: "Phone",
  EmailConfirmed: "Email confirmed",
  IsActive: "Active",
  LastPasswordChangeDate: "Password last changed",
  RolesAdded: "Roles added",
  RolesRemoved: "Roles removed",
  PasswordChanged: "Password",
};

const USER_KEEP_TABLES = new Set(["users", "userroles"]);
const USER_KEEP_ENTITIES = new Set(["fshuser", "userrole"]);

type ParsedChange = { name: string; oldValue: string; newValue: string };
type ParsedEvent = {
  operation: string;
  actionLabel: string;
  badgeClass: string;
  entityName: string;
  table: string;
  changes: ParsedChange[];
  businessChanges: ParsedChange[];
};

export type HistoryPreset = "default" | "user";

function fmtVal(v: unknown, field?: string): string {
  if (v === null || v === undefined) return "—";
  if (typeof v === "boolean") {
    if (field === "IsActive") return v ? "Active" : "Inactive";
    return v ? "Yes" : "No";
  }
  if (typeof v === "string") {
    const t = v.trim();
    if (t === "") return "—";
    if (/^\d{4}-\d{2}-\d{2}T/.test(t)) {
      try {
        return dateTimeMY(t);
      } catch {
        return t;
      }
    }
    return t;
  }
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

function parseEvent(payload: unknown, preset: HistoryPreset): ParsedEvent | null {
  const p = (payload ?? {}) as EntityChangePayload;
  const entityName = String(p.entityName ?? p.EntityName ?? "");
  const table = String(p.table ?? p.Table ?? "");
  const meta = operationMeta(p.operation ?? p.Operation);
  const list = p.changes ?? p.Changes ?? [];

  const rawChanges = list.map((c) => ({
    name: String(c.name ?? c.Name ?? "Field"),
    oldValue: c.oldValue ?? c.OldValue,
    newValue: c.newValue ?? c.NewValue,
  }));

  if (preset === "user") {
    const tableKey = table.toLowerCase();
    const entityKey = entityName.toLowerCase().replace(/`/g, "");
    const isUserRow =
      USER_KEEP_TABLES.has(tableKey) ||
      USER_KEEP_ENTITIES.has(entityKey) ||
      entityKey.startsWith("identityuserrole");

    // Password history / sessions / tokens often match the user id fuzzy search — skip them.
    if (!isUserRow) return null;

    const friendly: ParsedChange[] = [];
    let passwordNoted = false;

    for (const c of rawChanges) {
      if (c.name === "PasswordHash" || c.name.toLowerCase().includes("password")) {
        if (!passwordNoted) {
          friendly.push({
            name: "Password",
            oldValue: "—",
            newValue: "Changed",
          });
          passwordNoted = true;
        }
        continue;
      }
      if (USER_HIDDEN_FIELDS.has(c.name)) continue;

      const label = USER_FIELD_LABELS[c.name] ?? c.name.replace(/([a-z])([A-Z])/g, "$1 $2");
      friendly.push({
        name: label,
        oldValue: fmtVal(c.oldValue, c.name),
        newValue: fmtVal(c.newValue, c.name),
      });
    }

    // Role-only events
    const isRoleEvent = tableKey === "userroles" || entityKey === "userrole";
    let actionLabel = meta.actionLabel;
    let badgeClass = meta.badgeClass;
    if (isRoleEvent) {
      const added = friendly.some((f) => f.name === "Roles added");
      const removed = friendly.some((f) => f.name === "Roles removed");
      if (added && removed) actionLabel = "Roles updated";
      else if (added) actionLabel = "Roles added";
      else if (removed) actionLabel = "Roles removed";
      badgeClass = "badge b-blue";
    } else if (passwordNoted && friendly.length === 1) {
      actionLabel = "Password changed";
    } else if (meta.operation === "Insert") {
      actionLabel = "User created";
    }

    if (friendly.length === 0 && meta.operation !== "Insert") return null;

    return {
      ...meta,
      actionLabel,
      badgeClass,
      entityName,
      table,
      changes: friendly,
      businessChanges: friendly,
    };
  }

  const changes = rawChanges.map((c) => ({
    name: c.name,
    oldValue: fmtVal(c.oldValue),
    newValue: fmtVal(c.newValue),
  }));
  const businessChanges = changes.filter((c) => !NOISE_FIELDS.has(c.name));
  return { ...meta, entityName, table, changes, businessChanges };
}

function ChangeLines({ changes, emptyHint }: { changes: ParsedChange[]; emptyHint: string }) {
  if (changes.length === 0) {
    return (
      <p className="hint" style={{ margin: "6px 0 0" }}>
        {emptyHint}
      </p>
    );
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
            {c.oldValue === "—" && c.newValue !== "—" ? (
              <span style={{ color: "var(--ink)", fontWeight: 500 }}>{c.newValue}</span>
            ) : c.newValue === "—" && c.oldValue !== "—" ? (
              <span style={{ textDecoration: "line-through" }}>{c.oldValue}</span>
            ) : (
              <>
                <span style={{ textDecoration: c.oldValue === "—" ? undefined : "line-through" }}>
                  {c.oldValue}
                </span>
                <span style={{ margin: "0 6px", color: "var(--muted)" }}>→</span>
                <span style={{ color: "var(--ink)", fontWeight: 500 }}>{c.newValue}</span>
              </>
            )}
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
  preset = "default",
}: {
  title: string;
  entityId: string;
  onClose: () => void;
  /** `user` = plain-language profile / role / password history (hides Identity internals). */
  preset?: HistoryPreset;
}) {
  const historyQuery = useQuery({
    queryKey: ["entity-change-history", entityId, preset],
    queryFn: () => getEntityChangeHistory(entityId, 50),
    enabled: !!entityId,
  });

  const rows = useMemo(() => {
    const items = historyQuery.data ?? [];
    return items
      .map((detail: AuditDetailDto) => {
        const parsed = parseEvent(detail.payload, preset);
        return parsed ? { detail, parsed } : null;
      })
      .filter((x): x is { detail: AuditDetailDto; parsed: ParsedEvent } => x != null);
  }, [historyQuery.data, preset]);

  const intro =
    preset === "user"
      ? "Shows profile updates, role changes, and password resets for this user."
      : "Loaded on demand from Auditing — not part of the list API. Shows created / updated / deleted and each field's old → new value.";

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
        {intro}
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
                    ? preset === "user"
                      ? "User account created."
                      : "Record created (no field deltas)."
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

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  AUDIT_EVENT_TYPES,
  getAudit,
  listAudits,
  type AuditEventType,
  type AuditSummaryDto,
} from "@/api/audits";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";

export function AuditsPage() {
  const [q, setQ] = useState("");
  const [search, setSearch] = useState("");
  const [eventType, setEventType] = useState<AuditEventType | "">("");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const pageSize = 25;

  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(q);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const auditsQuery = useQuery({
    queryKey: ["audits", search, eventType, page, pageSize],
    queryFn: () =>
      listAudits({
        search: search.trim() || undefined,
        eventType: eventType || undefined,
        pageNumber: page,
        pageSize,
      }),
  });

  const detailQuery = useQuery({
    queryKey: ["audit", selectedId],
    queryFn: () => getAudit(selectedId!),
    enabled: !!selectedId,
  });

  const data = auditsQuery.data;
  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  if (selectedId && detailQuery.data) {
    const d = detailQuery.data;
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={() => setSelectedId(null)}>
            Audit log
          </button>{" "}
          <Icon name="chev" size={12} /> {d.id.slice(0, 8)}…
        </div>
        <div className="pagehead">
          <div>
            <h1>Audit detail</h1>
            <p>
              {d.eventType} · {d.severity} · {new Date(d.occurredAtUtc).toLocaleString()}
            </p>
          </div>
        </div>

        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div className="grid2">
              <div>
                <div className="hint">User</div>
                <div>{d.userName ?? d.userId ?? "—"}</div>
              </div>
              <div>
                <div className="hint">Source</div>
                <div>{d.source ?? "—"}</div>
              </div>
              <div>
                <div className="hint">Trace ID</div>
                <div>{d.traceId ?? "—"}</div>
              </div>
              <div>
                <div className="hint">Correlation ID</div>
                <div>{d.correlationId ?? "—"}</div>
              </div>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="chead">
            <h3>Payload</h3>
          </div>
          <div className="cbody">
            <pre
              style={{
                margin: 0,
                padding: 12,
                background: "#f8f6f0",
                border: "1px solid var(--line)",
                fontSize: 12,
                overflow: "auto",
                maxHeight: 480,
              }}
            >
              {JSON.stringify(d.payload, null, 2)}
            </pre>
          </div>
        </div>
      </>
    );
  }

  if (selectedId && detailQuery.isPending) {
    return <Spinner label="Loading audit…" />;
  }

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Audit log</h1>
          <p>Security and activity events recorded by the platform.</p>
        </div>
      </div>

      <div className="ribbon">{data?.totalCount ?? 0} event(s)</div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search</label>
              <input value={q} placeholder="user, source, trace…" onChange={(e) => setQ(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>Event type</label>
              <select
                value={eventType}
                onChange={(e) => {
                  setEventType(e.target.value as AuditEventType | "");
                  setPage(1);
                }}
              >
                <option value="">All</option>
                {AUDIT_EVENT_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {auditsQuery.isPending ? (
          <Spinner label="Loading audits…" />
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>When</th>
                  <th>Type</th>
                  <th>Severity</th>
                  <th>User</th>
                  <th>Source</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((a: AuditSummaryDto) => (
                  <tr key={a.id} className="drillrow" onClick={() => setSelectedId(a.id)}>
                    <td>{new Date(a.occurredAtUtc).toLocaleString()}</td>
                    <td>{a.eventType}</td>
                    <td>{a.severity}</td>
                    <td>{a.userName ?? a.userId ?? "—"}</td>
                    <td>{a.source ?? "—"}</td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Open <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
                {items.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      <EmptyState>No audit events match the filter.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
            {totalPages > 1 ? (
              <div className="actionbar" style={{ padding: "12px 16px" }}>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </button>
                <span className="hint" style={{ margin: "0 12px" }}>
                  Page {page} of {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </button>
              </div>
            ) : null}
          </>
        )}
      </div>
    </>
  );
}

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listRequisitions } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";

const PR_KANBAN_COLUMNS: { key: string; label: string }[] = [
  { key: "Draft", label: "Draft" },
  { key: "Submitted", label: "Submitted" },
  { key: "PartiallySourced", label: "Partially sourced" },
  { key: "Sourced", label: "Sourced" },
  { key: "Cancelled", label: "Cancelled" },
];

export function RequisitionListPage({
  onOpen,
  onNew,
  onNavigate,
}: {
  onOpen: (id: string) => void;
  onNew: () => void;
  onNavigate: (key: string) => void;
}) {
  const [status, setStatus] = useState("all");
  const [q, setQ] = useState("");
  const [view, setView] = useState<"table" | "board">("table");

  const { data, isPending } = useQuery({
    queryKey: ["requisitions"],
    queryFn: listRequisitions,
  });

  const rows = useMemo(() => {
    const items = data ?? [];
    return items.filter((r) => {
      if (status !== "all" && r.headerStatus !== status) return false;
      if (q.trim() && !`${r.code} ${r.requestor} ${r.department}`.toLowerCase().includes(q.trim().toLowerCase())) {
        return false;
      }
      return true;
    });
  }, [data, status, q]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Requisitions</h1>
          <p>Raise demand, then release lines into an RFQ once ready to source.</p>
        </div>
        <div className="spacer" />
        <div className="viewtoggle">
          <button type="button" className={view === "table" ? "on" : ""} onClick={() => setView("table")}>
            <Icon name="doc" size={14} /> Table
          </button>
          <button type="button" className={view === "board" ? "on" : ""} onClick={() => setView("board")}>
            <Icon name="dashboard" size={14} /> Board
          </button>
        </div>
        <button type="button" className="btn btn-out btn-sm" onClick={() => onNavigate("rfqs/new")}>
          <Icon name="rfq" size={15} /> Consolidate to RFQ
        </button>
        <button type="button" className="btn btn-pri btn-sm" onClick={onNew}>
          <Icon name="plus" size={15} /> New requisition
        </button>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search code / requestor / department</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="PR-2026-…" />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>Status</label>
              <select value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="all">All</option>
                <option value="Draft">Draft</option>
                <option value="Submitted">Submitted</option>
                <option value="PartiallySourced">Partially sourced</option>
                <option value="Sourced">Sourced</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      {view === "board" ? (
        isPending ? (
          <Spinner label="Loading requisitions…" />
        ) : (
          <div className="kanban">
            {PR_KANBAN_COLUMNS.map((col) => {
              const cards = rows.filter((r) => r.headerStatus === col.key);
              return (
                <div className="kcol" key={col.key}>
                  <div className="khead">
                    <span className={`kdot ${col.key.toLowerCase()}`} />
                    {col.label}
                    <span className="kcount">{cards.length}</span>
                  </div>
                  <div className="kbody">
                    {cards.map((r) => (
                      <div className="kcard" key={r.id} onClick={() => onOpen(r.id)}>
                        <div className="kc-top">
                          <span className="kc-code">{r.code}</span>
                          <SourcingStatusBadge status={r.headerStatus} />
                        </div>
                        <div className="kc-meta">
                          {r.requestor} · {r.department} · {r.lineCount} line(s)
                        </div>
                      </div>
                    ))}
                    {cards.length === 0 ? <div className="kempty">—</div> : null}
                  </div>
                </div>
              );
            })}
          </div>
        )
      ) : (
        <div className="card">
          {isPending ? (
            <Spinner label="Loading requisitions…" />
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Requestor</th>
                  <th>Department</th>
                  <th className="amt">Lines</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                    <td style={{ fontWeight: 700 }}>{r.code}</td>
                    <td>{r.requestor}</td>
                    <td>{r.department}</td>
                    <td className="amt">{r.lineCount}</td>
                    <td>
                      <SourcingStatusBadge status={r.headerStatus} />
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Open <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      <EmptyState>No requisitions match the filter.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          )}
        </div>
      )}
    </>
  );
}

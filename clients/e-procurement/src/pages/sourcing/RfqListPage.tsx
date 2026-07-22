import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listRfqs } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { SourcingStatusBadge, EnvelopeTag } from "@/components/sourcing/badges";
import { dateTimeMY } from "@/lib/format";

const BOARD_COLS: { status: string; label: string }[] = [
  { status: "Draft", label: "Draft" },
  { status: "Open", label: "Open" },
  { status: "Closed", label: "Closed" },
  { status: "Evaluation", label: "Evaluation" },
  { status: "Awarded", label: "Awarded" },
  { status: "Cancelled", label: "Cancelled" },
];

export function RfqListPage({ onOpen, onNew }: { onOpen: (id: string) => void; onNew: () => void }) {
  const [status, setStatus] = useState("all");
  const [view, setView] = useState<"table" | "board">("table");
  const { data, isPending } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs });

  const rows = useMemo(() => {
    const items = data ?? [];
    return status === "all" ? items : items.filter((r) => r.status === status);
  }, [data, status]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>RFQs</h1>
          <p>Requests for quotation — invite vendors, seal envelopes, evaluate and award.</p>
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
        <button type="button" className="btn btn-pri btn-sm" onClick={onNew}>
          <Icon name="plus" size={15} /> New RFQ
        </button>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>Status</label>
              <select value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="all">All</option>
                <option value="Draft">Draft</option>
                <option value="Open">Open</option>
                <option value="Closed">Closed</option>
                <option value="Evaluation">Evaluation</option>
                <option value="Awarded">Awarded</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      {view === "board" ? (
        isPending ? (
          <Spinner label="Loading RFQs…" />
        ) : (
          <div className="kanban">
            {BOARD_COLS.map((col) => {
              const cards = rows.filter((r) => r.status === col.status);
              return (
                <div className="kcol" key={col.status}>
                  <div className="khead">
                    <span className={`kdot ${col.status.toLowerCase()}`} />
                    {col.label}
                    <span className="kcount">{cards.length}</span>
                  </div>
                  <div className="kbody">
                    {cards.map((r) => (
                      <div className="kcard" key={r.id} onClick={() => onOpen(r.id)}>
                        <div className="kc-top">
                          <span className="kc-code">{r.code}</span>
                          <EnvelopeTag envelope={r.envelope} />
                        </div>
                        <div className="kc-title">{r.title || "—"}</div>
                        <div className="kc-meta">
                          <span className="hint">
                            {r.invitedCount} vendor(s)
                            {r.status !== "Draft" ? ` · closes ${dateTimeMY(r.closesUtc)}` : ""}
                          </span>
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
            <Spinner label="Loading RFQs…" />
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Title</th>
                  <th>Envelope</th>
                  <th className="amt">Vendors</th>
                  <th>Closes</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                    <td style={{ fontWeight: 700 }}>{r.code}</td>
                    <td>{r.title || <span className="hint">(untitled)</span>}</td>
                    <td>
                      <EnvelopeTag envelope={r.envelope} />
                    </td>
                    <td className="amt">{r.invitedCount}</td>
                    <td>{dateTimeMY(r.closesUtc)}</td>
                    <td>
                      <SourcingStatusBadge status={r.status} />
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        {r.status === "Draft" ? "Continue" : "Open"} <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={7}>
                      <EmptyState>No RFQs match the filter.</EmptyState>
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

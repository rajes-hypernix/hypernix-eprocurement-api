import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listRfqs, type RfqListItemDto } from "@/api/sourcing";
import { getViewFields, listViews, runView, type SavedViewDto } from "@/api/views";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { SourcingStatusBadge, EnvelopeTag } from "@/components/sourcing/badges";
import { ViewBuilder, ViewPicker } from "@/components/views/SavedViewControls";
import { dateTimeMY } from "@/lib/format";

const BOARD_COLS: { status: string; label: string }[] = [
  { status: "Draft", label: "Draft" },
  { status: "Open", label: "Open" },
  { status: "Closed", label: "Closed" },
  { status: "Evaluation", label: "Evaluation" },
  { status: "Awarded", label: "Awarded" },
  { status: "Cancelled", label: "Cancelled" },
];

/** Maps one `ViewRunResult` row (server casing may be PascalCase or camelCase) to the list DTO shape. */
function rowToRfqListItem(r: Record<string, unknown>): RfqListItemDto {
  return {
    id: String(r.Id ?? r.id ?? ""),
    code: String(r.Code ?? r.code ?? ""),
    title: String(r.Title ?? r.title ?? ""),
    envelope: String(r.Envelope ?? r.envelope ?? ""),
    status: String(r.Status ?? r.status ?? ""),
    currency: String(r.Currency ?? r.currency ?? ""),
    closesUtc: (r.ClosesUtc ?? r.closesUtc ?? null) as string | null,
    invitedCount: Number(r.InvitedCount ?? r.invitedCount ?? 0),
    lineCount: Number(r.LineCount ?? r.lineCount ?? 0),
  };
}

export function RfqListPage({ onOpen, onNew }: { onOpen: (id: string) => void; onNew: () => void }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const [status, setStatus] = useState("all");
  const [view, setView] = useState<"table" | "board">("table");
  const [selectedViewId, setSelectedViewId] = useState<string | null>(searchParams.get("view"));
  const [viewBuilder, setViewBuilder] = useState<{ existing: SavedViewDto | null } | null>(null);

  const { data, isPending: listIsPending } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs });

  const { data: savedViews = [] } = useQuery({ queryKey: ["views", "Rfq"], queryFn: () => listViews("Rfq") });
  const { data: rfqFields = [] } = useQuery({
    queryKey: ["view-fields", "Rfq"],
    queryFn: () => getViewFields("Rfq"),
    staleTime: Infinity,
  });
  const { data: viewRun, isPending: viewIsPending } = useQuery({
    queryKey: ["view-run", selectedViewId],
    queryFn: () => runView(selectedViewId!, 1, 200),
    enabled: !!selectedViewId,
  });

  const selectView = (id: string) => {
    setSelectedViewId(id);
    const next = new URLSearchParams(searchParams);
    next.set("view", id);
    setSearchParams(next, { replace: true });
  };
  const clearView = () => {
    setSelectedViewId(null);
    const next = new URLSearchParams(searchParams);
    next.delete("view");
    setSearchParams(next, { replace: true });
  };

  const isPending = selectedViewId ? viewIsPending : listIsPending;

  const rows = useMemo(() => {
    const items = selectedViewId && viewRun ? viewRun.rows.map(rowToRfqListItem) : data ?? [];
    return status === "all" ? items : items.filter((r) => r.status === status);
  }, [data, selectedViewId, viewRun, status]);

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
          <ViewPicker
            views={savedViews}
            selectedId={selectedViewId}
            onSelect={selectView}
            onClear={clearView}
            clearLabel="All RFQs"
            onNew={() => setViewBuilder({ existing: null })}
            onEdit={(v) => setViewBuilder({ existing: v })}
          />
        </div>
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

      {viewBuilder ? (
        <ViewBuilder
          recordType="Rfq"
          existing={viewBuilder.existing}
          defaultColumns={rfqFields.slice(0, 5).map((f) => f.fieldKey)}
          onClose={() => setViewBuilder(null)}
          onSaved={(savedView) => {
            setViewBuilder(null);
            selectView(savedView.id);
          }}
        />
      ) : null}
    </>
  );
}

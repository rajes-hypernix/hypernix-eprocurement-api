import { useEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { cancelRfq, closeRfq, listRfqs, type RfqListItemDto } from "@/api/sourcing";
import { getViewFields, listViews, runView, type SavedViewDto } from "@/api/views";
import { Icon } from "@/components/Icon";
import { ConfirmModal, EmptyState, Spinner } from "@/components/ui";
import {
  BidProgressBadge,
  EnvelopeTag,
  RfqStatusBadge,
} from "@/components/sourcing/badges";
import { ViewBuilder, ViewPicker } from "@/components/views/SavedViewControls";
import { dateMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

const BOARD_COLS: { status: string; label: string }[] = [
  { status: "Draft", label: "Draft" },
  { status: "Open", label: "Open · awaiting bids" },
  { status: "Closed", label: "Closed · ready to open" },
  { status: "Evaluation", label: "Under evaluation" },
  { status: "Awarded", label: "Awarded" },
  { status: "Cancelled", label: "Cancelled" },
];

const STATUS_LABELS: Record<string, string> = Object.fromEntries(BOARD_COLS.map((c) => [c.status, c.label]));

type FacetKey = "envelope" | "closes" | "status";
const FACETS: { key: FacetKey; label: string }[] = [
  { key: "envelope", label: "Envelope" },
  { key: "closes", label: "Closes" },
  { key: "status", label: "Status" },
];
const EMPTY_FACETS: Record<FacetKey, string> = { envelope: "all", closes: "all", status: "all" };

const closesDay = (iso: string | null | undefined) => (iso ? dateMY(iso) : "");

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
    bidCount: Number(r.BidCount ?? r.bidCount ?? 0),
  };
}

type ConfirmKind = { kind: "close" | "cancel"; rfq: RfqListItemDto };

export function RfqListPage({ onOpen, onNew }: { onOpen: (id: string) => void; onNew: () => void }) {
  const qc = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [q, setQ] = useState("");
  const [facets, setFacets] = useState<Record<FacetKey, string>>({ ...EMPTY_FACETS });
  const [view, setView] = useState<"table" | "board">("table");
  const [selectedViewId, setSelectedViewId] = useState<string | null>(searchParams.get("view"));
  const [viewBuilder, setViewBuilder] = useState<{ existing: SavedViewDto | null } | null>(null);
  const [confirm, setConfirm] = useState<ConfirmKind | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [menuFor, setMenuFor] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);

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

  useEffect(() => {
    if (!menuFor) return;
    const onDoc = (e: globalThis.MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuFor(null);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [menuFor]);

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

  const scoped = useMemo(
    () => (selectedViewId && viewRun ? viewRun.rows.map(rowToRfqListItem) : data ?? []),
    [data, selectedViewId, viewRun],
  );

  const facetOptions = useMemo(() => {
    const opts: Record<FacetKey, string[]> = { envelope: [], closes: [], status: [] };
    const sets: Record<FacetKey, Set<string>> = { envelope: new Set(), closes: new Set(), status: new Set() };
    for (const r of scoped) {
      if (r.envelope) sets.envelope.add(r.envelope);
      const day = closesDay(r.closesUtc);
      if (day && day !== "—") sets.closes.add(day);
      if (r.status) sets.status.add(r.status);
    }
    for (const key of FACETS.map((f) => f.key)) {
      opts[key] = [...sets[key]].sort((a, b) => a.localeCompare(b));
    }
    return opts;
  }, [scoped]);

  const rows = useMemo(() => {
    const needle = q.trim().toLowerCase();
    return scoped.filter((r) => {
      if (needle && ![r.code, r.title].some((v) => (v ?? "").toLowerCase().includes(needle))) return false;
      if (facets.envelope !== "all" && r.envelope !== facets.envelope) return false;
      if (facets.closes !== "all" && closesDay(r.closesUtc) !== facets.closes) return false;
      if (facets.status !== "all" && r.status !== facets.status) return false;
      return true;
    });
  }, [scoped, q, facets]);

  const resetFilters = () => {
    setQ("");
    setFacets({ ...EMPTY_FACETS });
  };

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["rfqs"] });
    if (selectedViewId) void qc.invalidateQueries({ queryKey: ["view-run", selectedViewId] });
  };

  const close = useMutation({
    mutationFn: (id: string) => closeRfq(id),
    onSuccess: () => {
      setConfirm(null);
      refresh();
    },
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });
  const cancel = useMutation({
    mutationFn: (id: string) => cancelRfq(id),
    onSuccess: () => {
      setConfirm(null);
      refresh();
    },
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const stop = (e: ReactMouseEvent) => e.stopPropagation();

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>RFQs</h1>
          <p>All sourcing events and their stage.</p>
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

      {err ? (
        <p className="ferr" style={{ marginBottom: 10 }}>
          {err}
        </p>
      ) : null}

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
          <div className="filterbar filterbar-auto">
            <div className="field" style={{ margin: 0, minWidth: 200 }}>
              <label>RFQ #</label>
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="RFQ number or title…"
                aria-label="Search RFQs"
              />
            </div>
            {FACETS.map((f) => (
              <div className="field" style={{ margin: 0 }} key={f.key}>
                <label>{f.label}</label>
                <select
                  value={facets[f.key]}
                  onChange={(e) => setFacets((prev) => ({ ...prev, [f.key]: e.target.value }))}
                >
                  <option value="all">All</option>
                  {facetOptions[f.key].map((v) => (
                    <option key={v} value={v}>
                      {f.key === "status" ? (STATUS_LABELS[v] ?? v) : v}
                    </option>
                  ))}
                </select>
              </div>
            ))}
            <div className="field" style={{ margin: 0 }}>
              <label>&nbsp;</label>
              <button type="button" className="freset" onClick={resetFilters}>
                Reset
              </button>
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
                          {r.status === "Open" ? (
                            <BidProgressBadge bidCount={r.bidCount} invitedCount={r.invitedCount} />
                          ) : (
                            <span className="hint">
                              {r.invitedCount} vendor(s)
                              {r.status !== "Draft" ? ` · ${r.bidCount}/${r.invitedCount} bids` : ""}
                            </span>
                          )}
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
                  <th>Bid progress</th>
                  <th>Closes</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => {
                  const draft = r.status === "Draft";
                  const open = r.status === "Open";
                  const showMenu = open || draft;
                  return (
                    <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                      <td style={{ fontWeight: 700 }}>{r.code}</td>
                      <td>{r.title || <span className="hint">(untitled)</span>}</td>
                      <td>
                        <EnvelopeTag envelope={r.envelope} />
                      </td>
                      <td className="amt">{r.invitedCount}</td>
                      <td>
                        {open ? (
                          <BidProgressBadge bidCount={r.bidCount} invitedCount={r.invitedCount} />
                        ) : r.status === "Draft" ? (
                          <span className="hint">—</span>
                        ) : (
                          <span className="hint">
                            {r.bidCount}/{r.invitedCount}
                          </span>
                        )}
                      </td>
                      <td>{draft ? "—" : dateMY(r.closesUtc)}</td>
                      <td>
                        <RfqStatusBadge status={r.status} />
                      </td>
                      <td className="amt" onClick={stop}>
                        <div className="rowed" style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            onClick={() => onOpen(r.id)}
                          >
                            {draft ? "Continue" : "Open"} <Icon name="chev" size={13} />
                          </button>
                          {showMenu ? (
                            <div style={{ position: "relative" }} ref={menuFor === r.id ? menuRef : undefined}>
                              <button
                                type="button"
                                className="btn btn-ghost btn-sm"
                                aria-label="More actions"
                                title={`More actions ${r.code}`}
                                onClick={() => setMenuFor((cur) => (cur === r.id ? null : r.id))}
                              >
                                <Icon name="menu" size={14} />
                              </button>
                              {menuFor === r.id ? (
                                <div className="qmenu" style={{ right: 0, left: "auto", minWidth: 140 }}>
                                  {open ? (
                                    <button
                                      type="button"
                                      onClick={() => {
                                        setMenuFor(null);
                                        setConfirm({ kind: "close", rfq: r });
                                      }}
                                    >
                                      Close bids
                                    </button>
                                  ) : null}
                                  <button
                                    type="button"
                                    style={{ color: "var(--red)" }}
                                    onClick={() => {
                                      setMenuFor(null);
                                      setConfirm({ kind: "cancel", rfq: r });
                                    }}
                                  >
                                    Cancel
                                  </button>
                                </div>
                              ) : null}
                            </div>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={8}>
                      <EmptyState>
                        {scoped.length === 0 ? "No RFQs yet — create one from Requisitions." : "No RFQs match these filters."}
                      </EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          )}
        </div>
      )}

      {confirm?.kind === "close" ? (
        <ConfirmModal
          title={`Close bids for ${confirm.rfq.code}?`}
          icon="clock"
          body="Vendors will no longer be able to submit or revise bids. You can proceed to bid opening after closing."
          cancelLabel="Not yet"
          confirmLabel="Close bids now"
          busy={close.isPending}
          onCancel={() => setConfirm(null)}
          onConfirm={() => close.mutate(confirm.rfq.id)}
        />
      ) : null}
      {confirm?.kind === "cancel" ? (
        <ConfirmModal
          title={`Cancel ${confirm.rfq.code}?`}
          icon="x"
          body="This cancels the RFQ. Invited vendors will no longer be able to respond."
          cancelLabel="Keep RFQ"
          confirmLabel="Cancel RFQ"
          danger
          busy={cancel.isPending}
          onCancel={() => setConfirm(null)}
          onConfirm={() => cancel.mutate(confirm.rfq.id)}
        />
      ) : null}

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

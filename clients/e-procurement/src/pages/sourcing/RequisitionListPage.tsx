import { useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  listRequisitions,
  getRequisition,
  cancelRequisitionLine,
  releaseRequisitionLine,
  reopenRequisitionLine,
  reserveRequisitionLine,
  createRfqDraft,
  type PrLineDto,
  type RequisitionDto,
  type RfqLineInput,
} from "@/api/sourcing";
import { getViewFields, listViews, rowId, runView, type SavedViewDto } from "@/api/views";
import { Icon } from "@/components/Icon";
import { EmptyState, Modal, Notice, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { ViewBuilder, ViewPicker } from "@/components/views/SavedViewControls";
import { ApiRequestError } from "@/lib/api-client";
import { dateMY } from "@/lib/format";

const PR_KANBAN_COLUMNS: { key: string; label: string }[] = [
  { key: "Draft", label: "Draft" },
  { key: "Submitted", label: "Submitted" },
  { key: "PartiallySourced", label: "Partially sourced" },
  { key: "Sourced", label: "Sourced" },
  { key: "Cancelled", label: "Cancelled" },
];

const STATUS_LABELS: Record<string, string> = Object.fromEntries(PR_KANBAN_COLUMNS.map((c) => [c.key, c.label]));

type FacetKey = "requestor" | "department" | "location" | "job" | "category" | "headerStatus";
const FACETS: { key: FacetKey; label: string }[] = [
  { key: "requestor", label: "Requestor" },
  { key: "department", label: "Dept" },
  { key: "location", label: "Location" },
  { key: "job", label: "Job" },
  { key: "category", label: "Category" },
  { key: "headerStatus", label: "Status" },
];
const EMPTY_FACETS: Record<FacetKey, string> = {
  requestor: "all",
  department: "all",
  location: "all",
  job: "all",
  category: "all",
  headerStatus: "all",
};

/** Header-gate for grouping — line-level lifecycle (Open) is checked via openLineCount. */
const SOURCE_ELIGIBLE_HEADER = new Set(["Submitted", "PartiallySourced"]);
const isLineSourceable = (l: PrLineDto) => l.lifecycleStatus === "Open";
const hasAvailableLines = (r: { headerStatus: string; openLineCount: number }) =>
  SOURCE_ELIGIBLE_HEADER.has(r.headerStatus) && r.openLineCount > 0;

type LineModal = { kind: "cancel" | "release"; prId: string; lineId: string; item: string };

const TABLE_COLS = 10;

export function RequisitionListPage({
  onOpen,
  onNew,
  onNavigate,
  onOpenRfq,
}: {
  onOpen: (id: string) => void;
  onNew: () => void;
  onNavigate: (key: string) => void;
  /** Navigate to the newly-built RFQ draft. Falls back to onNavigate(`rfqs/{id}`) when omitted. */
  onOpenRfq?: (id: string) => void;
}) {
  const qc = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [facets, setFacets] = useState<Record<FacetKey, string>>({ ...EMPTY_FACETS });
  const [show, setShow] = useState<"all" | "avail">("all");
  const [view, setView] = useState<"table" | "board">("table");
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [grouped, setGrouped] = useState<Set<string>>(new Set());
  const [drawer, setDrawer] = useState(false);
  const [lineModal, setLineModal] = useState<LineModal | null>(null);
  const [reason, setReason] = useState("");
  const [selectedViewId, setSelectedViewId] = useState<string | null>(searchParams.get("view"));
  const [viewBuilder, setViewBuilder] = useState<{ existing: SavedViewDto | null } | null>(null);

  const { data, isPending } = useQuery({ queryKey: ["requisitions"], queryFn: listRequisitions });
  const rows = data ?? [];

  const { data: savedViews = [] } = useQuery({
    queryKey: ["views", "Requisition"],
    queryFn: () => listViews("Requisition"),
  });
  const { data: reqFields = [] } = useQuery({
    queryKey: ["view-fields", "Requisition"],
    queryFn: () => getViewFields("Requisition"),
    staleTime: Infinity,
  });
  const { data: viewRun } = useQuery({
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

  const viewIds = useMemo(
    () => (selectedViewId && viewRun ? new Set(viewRun.rows.map((r) => rowId(r))) : null),
    [selectedViewId, viewRun],
  );

  const scoped = useMemo(
    () => (viewIds ? rows.filter((r) => viewIds.has(r.id)) : rows),
    [rows, viewIds],
  );

  const facetOptions = useMemo(() => {
    const opts: Record<FacetKey, string[]> = {
      requestor: [],
      department: [],
      location: [],
      job: [],
      category: [],
      headerStatus: [],
    };
    for (const key of FACETS.map((f) => f.key)) {
      const set = new Set<string>();
      for (const r of scoped) {
        const v =
          key === "requestor"
            ? r.requestor
            : key === "department"
              ? r.department
              : key === "location"
                ? r.location
                : key === "job"
                  ? r.job
                  : key === "category"
                    ? r.category
                    : r.headerStatus;
        if (v?.trim()) set.add(v);
      }
      opts[key] = [...set].sort((a, b) => a.localeCompare(b));
    }
    return opts;
  }, [scoped]);

  const filtered = useMemo(() => {
    return scoped.filter((r) => {
      if (facets.requestor !== "all" && r.requestor !== facets.requestor) return false;
      if (facets.department !== "all" && r.department !== facets.department) return false;
      if (facets.location !== "all" && r.location !== facets.location) return false;
      if (facets.job !== "all" && r.job !== facets.job) return false;
      if (facets.category !== "all" && r.category !== facets.category) return false;
      if (facets.headerStatus !== "all" && r.headerStatus !== facets.headerStatus) return false;
      if (show === "avail" && !hasAvailableLines(r)) return false;
      return true;
    });
  }, [scoped, facets, show]);

  const resetFilters = () => {
    setFacets({ ...EMPTY_FACETS });
    setShow("all");
  };

  const groupable = filtered.filter(hasAvailableLines);
  const allGrouped = groupable.length > 0 && groupable.every((r) => grouped.has(r.id));
  const someGrouped = groupable.some((r) => grouped.has(r.id));

  const setMember = (s: Set<string>, id: string, on: boolean) => {
    const n = new Set(s);
    if (on) n.add(id);
    else n.delete(id);
    return n;
  };
  const toggleExpand = (id: string) => setExpanded((p) => setMember(p, id, !p.has(id)));
  const expandAll = () => setExpanded(new Set(filtered.map((r) => r.id)));
  const collapseAll = () => setExpanded(new Set());
  const setGroup = (id: string, on: boolean) => setGrouped((p) => setMember(p, id, on));
  const selectAllGroupable = (on: boolean) =>
    setGrouped((p) => {
      const n = new Set(p);
      groupable.forEach((r) => (on ? n.add(r.id) : n.delete(r.id)));
      return n;
    });

  const invalidate = () => void qc.invalidateQueries({ queryKey: ["requisitions"] });
  const lineMut = useMutation({
    mutationFn: (m: LineModal & { reason: string }) =>
      m.kind === "cancel"
        ? cancelRequisitionLine(m.prId, m.lineId, m.reason)
        : releaseRequisitionLine(m.prId, m.lineId, m.reason),
    onSuccess: () => {
      invalidate();
      qc.invalidateQueries({ queryKey: ["requisition"] });
      setLineModal(null);
      setReason("");
    },
  });
  const reopenMut = useMutation({
    mutationFn: (v: { prId: string; lineId: string }) => reopenRequisitionLine(v.prId, v.lineId),
    onSuccess: () => {
      invalidate();
      qc.invalidateQueries({ queryKey: ["requisition"] });
    },
  });

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Requisitions</h1>
          <p>All requisitions. Expand one to act on its lines; group them to source.</p>
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
        <button type="button" className="btn btn-out btn-sm" onClick={() => onNavigate("consolidate")}>
          <Icon name="box" size={15} /> Build RFQ
        </button>
        <button type="button" className="btn btn-pri btn-sm" onClick={onNew}>
          <Icon name="plus" size={15} /> Create Requisition
        </button>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <ViewPicker
            views={savedViews}
            selectedId={selectedViewId}
            onSelect={selectView}
            onClear={clearView}
            clearLabel="Standard list"
            onNew={() => setViewBuilder({ existing: null })}
            onEdit={(v) => setViewBuilder({ existing: v })}
          />
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar filterbar-auto">
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
                      {f.key === "headerStatus" ? (STATUS_LABELS[v] ?? v) : v}
                    </option>
                  ))}
                </select>
              </div>
            ))}
            <div className="field" style={{ margin: 0 }}>
              <label>Show</label>
              <select value={show} onChange={(e) => setShow(e.target.value as "all" | "avail")}>
                <option value="all">All PRs</option>
                <option value="avail">With available lines</option>
              </select>
            </div>
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
          <Spinner label="Loading requisitions…" />
        ) : (
          <div className="kanban">
            {PR_KANBAN_COLUMNS.map((col) => {
              const cards = filtered.filter((r) => r.headerStatus === col.key);
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
        <>
          {grouped.size > 0 ? (
            <div className="abar2 live">
              <span className="info">
                <b>{grouped.size}</b> requisition{grouped.size === 1 ? "" : "s"} grouped
              </span>
              <div style={{ flex: 1 }} />
              <button type="button" className="btn btn-pri" onClick={() => setDrawer(true)}>
                <Icon name="rfq" size={15} /> Confirm lines
              </button>
            </div>
          ) : (
            <div className="abar2">
              <span className="info">Tick the requisitions you want to source, then confirm which lines go into the RFQ.</span>
              <div style={{ flex: 1 }} />
              <button type="button" className="btn btn-pri" disabled>
                <Icon name="rfq" size={15} /> Confirm lines
              </button>
            </div>
          )}

          <div className="actbar" style={{ justifyContent: "flex-end", marginBottom: 8 }}>
            <button type="button" className="freset" onClick={expandAll}>
              Expand all
            </button>
            <button type="button" className="freset" onClick={collapseAll}>
              Collapse all
            </button>
          </div>

          <div className="card">
            {isPending ? (
              <Spinner label="Loading requisitions…" />
            ) : (
              <table>
                <thead>
                  <tr>
                    <th style={{ width: 34 }}>
                      <input
                        type="checkbox"
                        style={{ width: "auto" }}
                        checked={allGrouped}
                        ref={(el) => {
                          if (el) el.indeterminate = someGrouped && !allGrouped;
                        }}
                        onChange={(e) => selectAllGroupable(e.target.checked)}
                        aria-label="Select all source-eligible requisitions"
                      />
                    </th>
                    <th style={{ width: 132 }}>PR #</th>
                    <th>Requestor</th>
                    <th>Dept</th>
                    <th>Location</th>
                    <th>Job</th>
                    <th>Category</th>
                    <th>Required by</th>
                    <th>PR status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((r) => {
                    const open = expanded.has(r.id);
                    const canGroup = hasAvailableLines(r);
                    return (
                      <FragmentRows key={r.id}>
                        <tr
                          className={`prrow rowlink ${open ? "open" : ""} ${grouped.has(r.id) ? "grp" : ""}`}
                          onClick={() => toggleExpand(r.id)}
                        >
                          <td onClick={(e) => e.stopPropagation()}>
                            <input
                              type="checkbox"
                              style={{ width: "auto" }}
                              disabled={!canGroup}
                              checked={grouped.has(r.id)}
                              onChange={(e) => setGroup(r.id, e.target.checked)}
                              aria-label={`Group ${r.code}`}
                            />
                          </td>
                          <td style={{ whiteSpace: "nowrap" }}>
                            <span className="chev">
                              <Icon name="chev" size={13} />
                            </span>{" "}
                            <span style={{ fontWeight: 700, color: "var(--teal)" }}>{r.code}</span>
                          </td>
                          <td>{r.requestor || "—"}</td>
                          <td>{r.department || "—"}</td>
                          <td>{r.location || "—"}</td>
                          <td>{r.job || "—"}</td>
                          <td>{r.category || "—"}</td>
                          <td>{dateMY(r.requiredOn)}</td>
                          <td>
                            <SourcingStatusBadge status={r.headerStatus} />
                          </td>
                          <td className="amt" onClick={(e) => e.stopPropagation()}>
                            <div className="rowactions">
                              <button type="button" className="btn btn-ghost btn-sm" onClick={() => onOpen(r.id)}>
                                <Icon name="edit" size={14} /> Open
                              </button>
                            </div>
                          </td>
                        </tr>
                        {open ? (
                          <tr>
                            <td colSpan={TABLE_COLS} style={{ padding: 0, background: "#fbfbf9" }}>
                              <ExpandedLines
                                prId={r.id}
                                onCancel={(lineId, item) => {
                                  setReason("");
                                  setLineModal({ kind: "cancel", prId: r.id, lineId, item });
                                }}
                                onRelease={(lineId, item) => {
                                  setReason("");
                                  setLineModal({ kind: "release", prId: r.id, lineId, item });
                                }}
                                onReopen={(lineId) => reopenMut.mutate({ prId: r.id, lineId })}
                              />
                            </td>
                          </tr>
                        ) : null}
                      </FragmentRows>
                    );
                  })}
                  {filtered.length === 0 ? (
                    <tr>
                      <td colSpan={TABLE_COLS}>
                        <EmptyState>No PRs match these filters.</EmptyState>
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}

      {drawer ? (
        <ConfirmLinesDrawer
          prIds={[...grouped]}
          onClose={() => setDrawer(false)}
          onCreated={(id) => {
            setDrawer(false);
            setGrouped(new Set());
            invalidate();
            if (onOpenRfq) onOpenRfq(id);
            else onNavigate(`rfqs/${id}`);
          }}
        />
      ) : null}

      {lineModal ? (
        <Modal
          title={lineModal.kind === "cancel" ? "Cancel line" : "Release for re-sourcing"}
          icon={lineModal.kind === "cancel" ? "x" : "rfq"}
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setLineModal(null)}>
                {lineModal.kind === "cancel" ? "Keep line" : "Cancel"}
              </button>
              <button
                type="button"
                className={`btn btn-pri${lineModal.kind === "cancel" ? " btn-danger" : ""}`}
                disabled={lineMut.isPending}
                onClick={() => lineMut.mutate({ ...lineModal, reason })}
              >
                {lineModal.kind === "cancel" ? "Cancel line" : "Release to Open"}
              </button>
            </>
          }
        >
          <p style={{ marginTop: 0 }}>
            {lineModal.kind === "cancel" ? (
              <>
                Cancel <b>{lineModal.item}</b>? The demand is withdrawn — this is terminal for the line.
              </>
            ) : (
              <>
                Release <b>{lineModal.item}</b> back to <b>Open</b> so it can go into a new RFQ? Its sourcing link is
                marked <b>Returned</b>; provenance is preserved.
              </>
            )}
          </p>
          <div className="field">
            <label>Reason</label>
            <textarea
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={lineModal.kind === "cancel" ? "e.g. Duplicate / no longer required" : "e.g. No vendor quoted this line"}
            />
          </div>
        </Modal>
      ) : null}

      {viewBuilder ? (
        <ViewBuilder
          recordType="Requisition"
          existing={viewBuilder.existing}
          defaultColumns={reqFields.slice(0, 5).map((f) => f.fieldKey)}
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

/** React fragment wrapper that accepts a key, so we can return paired table rows. */
function FragmentRows({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

function ExpandedLines({
  prId,
  onCancel,
  onRelease,
  onReopen,
}: {
  prId: string;
  onCancel: (lineId: string, item: string) => void;
  onRelease: (lineId: string, item: string) => void;
  onReopen: (lineId: string) => void;
}) {
  const { data, isPending } = useQuery({ queryKey: ["requisition", prId], queryFn: () => getRequisition(prId) });

  if (isPending || !data) {
    return (
      <div style={{ padding: 16 }}>
        <Spinner label="Loading lines…" />
      </div>
    );
  }

  return (
    <table>
      <thead>
        <tr>
          <th>Item</th>
          <th>Description</th>
          <th className="amt">Qty</th>
          <th>UoM</th>
          <th className="amt">Est. amount</th>
          <th>Line status</th>
          <th>Action</th>
        </tr>
      </thead>
      <tbody>
        {data.lines.map((l) => (
          <tr key={l.id}>
            <td>{l.itemCode}</td>
            <td>{l.description}</td>
            <td className="amt">{l.qty}</td>
            <td>{l.uom}</td>
            <td className="amt">{(l.qty * l.estUnitPrice).toLocaleString()}</td>
            <td>
              <SourcingStatusBadge status={l.lifecycleStatus} />
            </td>
            <td className="amt">
              <div className="rowactions">
                {l.lifecycleStatus === "Open" ? (
                  <>
                    <button type="button" className="btn btn-out btn-sm" onClick={() => onRelease(l.id, l.description)}>
                      Release for re-sourcing
                    </button>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      style={{ color: "var(--red)" }}
                      onClick={() => onCancel(l.id, l.description)}
                    >
                      Cancel line
                    </button>
                  </>
                ) : l.lifecycleStatus === "InDraftRfq" ? (
                  <span className="hint">Reserved for a draft RFQ</span>
                ) : l.lifecycleStatus === "InRfq" ? (
                  <span className="hint">Locked to {l.ref ?? "RFQ"} (live)</span>
                ) : l.lifecycleStatus === "Awarded" ? (
                  <span className="hint">Awarded — PO issued</span>
                ) : l.lifecycleStatus === "Cancelled" ? (
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => onReopen(l.id)}>
                    Re-open
                  </button>
                ) : null}
              </div>
            </td>
          </tr>
        ))}
        {data.lines.length === 0 ? (
          <tr>
            <td colSpan={7} style={{ padding: 16, textAlign: "center", color: "var(--muted)" }}>
              No lines.
            </td>
          </tr>
        ) : null}
      </tbody>
    </table>
  );
}

function ConfirmLinesDrawer({
  prIds,
  onClose,
  onCreated,
}: {
  prIds: string[];
  onClose: () => void;
  onCreated: (id: string) => void;
}) {
  const results = useQueries({
    queries: prIds.map((id) => ({ queryKey: ["requisition", id], queryFn: () => getRequisition(id) })),
  });
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [err, setErr] = useState<string | null>(null);
  const seeded = useRef<Set<string>>(new Set());

  // Seed selection with every sourceable line the first time each requisition's detail resolves,
  // mirroring the old "everything ticked, uncheck what you don't want" drawer default.
  useEffect(() => {
    results.forEach((res, i) => {
      const prId = prIds[i];
      if (res.data && prId && !seeded.current.has(prId)) {
        seeded.current.add(prId);
        const ids = res.data.lines.filter(isLineSourceable).map((l) => l.id);
        if (ids.length) setSelected((prev) => new Set([...prev, ...ids]));
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [results, prIds]);

  const details = prIds
    .map((id, i) => ({ id, data: results[i]?.data }))
    .filter((x): x is { id: string; data: RequisitionDto } => !!x.data);
  const loading = results.some((r) => r.isPending);

  const toggleLine = (id: string, on: boolean) =>
    setSelected((p) => {
      const n = new Set(p);
      if (on) n.add(id);
      else n.delete(id);
      return n;
    });
  const selAllForPr = (detail: RequisitionDto) => {
    const ids = detail.lines.filter(isLineSourceable).map((l) => l.id);
    const allOn = ids.every((id) => selected.has(id));
    setSelected((p) => {
      const n = new Set(p);
      ids.forEach((id) => (allOn ? n.delete(id) : n.add(id)));
      return n;
    });
  };
  const markAll = (on: boolean) => {
    const ids = details.flatMap((d) => d.data.lines.filter(isLineSourceable).map((l) => l.id));
    setSelected(new Set(on ? ids : []));
  };

  const selCount = selected.size;
  const totalAvail = details.reduce((n, d) => n + d.data.lines.filter(isLineSourceable).length, 0);

  const create = useMutation({
    mutationFn: async () => {
      const lines: RfqLineInput[] = [];
      const reserveCalls: { prId: string; lineId: string }[] = [];
      const prRefs = new Set<string>();
      let n = 1;
      details.forEach(({ id: prId, data: detail }) => {
        detail.lines
          .filter((l) => isLineSourceable(l) && selected.has(l.id))
          .forEach((l) => {
            prRefs.add(prId);
            reserveCalls.push({ prId, lineId: l.id });
            lines.push({
              lineCode: `L${n++}`,
              itemCode: l.itemCode,
              description: l.description,
              qty: l.qty,
              uom: l.uom,
              prRef: detail.code,
              sourcePrLineIds: [l.id],
            });
          });
      });
      await Promise.all(reserveCalls.map((rc) => reserveRequisitionLine(rc.prId, rc.lineId)));
      return createRfqDraft({ title: null, envelope: "Dual", currency: "MYR", prRefs: [...prRefs], lines });
    },
    onSuccess: (id) => onCreated(id),
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  return (
    <>
      <div className="rqscrim" onClick={onClose} />
      <div className="rqdrawer">
        <div className="dh">
          <Icon name="rfq" size={18} />
          <h3>Select lines for RFQ</h3>
          <button type="button" className="x" onClick={onClose}>
            ×
          </button>
        </div>
        <div className="dsub">
          <span className="lbl">
            {totalAvail} available {totalAvail === 1 ? "line" : "lines"} across {details.length} requisition
            {details.length === 1 ? "" : "s"}
          </span>
          <div style={{ flex: 1 }} />
          <button type="button" onClick={() => markAll(true)}>
            Mark all
          </button>
          <button type="button" onClick={() => markAll(false)}>
            Unmark all
          </button>
        </div>
        <div className="db">
          {err ? (
            <Notice tone="error" icon="x" style={{ margin: 12 }}>
              {err}
            </Notice>
          ) : null}
          {details.map(({ data: pr }) => (
            <div className="psec2" key={pr.id}>
              <div className="ph">
                <span className="pid">{pr.code}</span>
                <span className="pc">
                  {pr.requestor} · {pr.category}
                </span>
                <button type="button" className="selall" onClick={() => selAllForPr(pr)}>
                  Select all available
                </button>
              </div>
              {pr.lines.map((l) => {
                if (isLineSourceable(l)) {
                  return (
                    <label className="lrow2" key={l.id}>
                      <input
                        type="checkbox"
                        checked={selected.has(l.id)}
                        onChange={(e) => toggleLine(l.id, e.target.checked)}
                        aria-label={`Select ${l.itemCode}`}
                      />
                      <span className="lc">
                        <div className="desc">{l.description}</div>
                        <div className="sub">
                          {l.itemCode} · {l.qty} {l.uom}
                        </div>
                      </span>
                    </label>
                  );
                }
                return (
                  <div className="lrow2 lock" key={l.id}>
                    <span style={{ width: 15 }} />
                    <span className="lc">
                      <div className="desc">{l.description}</div>
                      <div className="lockchip">
                        <Icon name="lock" size={12} /> {l.ref ?? l.lifecycleStatus}
                      </div>
                    </span>
                  </div>
                );
              })}
            </div>
          ))}
          {loading ? (
            <div style={{ padding: 24 }}>
              <Spinner label="Loading lines…" />
            </div>
          ) : null}
          {!loading && details.length === 0 ? (
            <div style={{ padding: 24, color: "var(--muted)" }}>No grouped requisitions.</div>
          ) : null}
        </div>
        <div className="df">
          <div className="note">
            <Icon name="eye" size={13} /> Unticked lines stay available for future RFQs.
          </div>
          <div className="frow">
            <span className="tot">
              {selCount} {selCount === 1 ? "line" : "lines"} selected
            </span>
            <div style={{ flex: 1 }} />
            <button type="button" className="btn btn-out" onClick={onClose}>
              Cancel
            </button>
            <button type="button" className="btn btn-pri" disabled={selCount === 0 || create.isPending} onClick={() => create.mutate()}>
              Create RFQ <Icon name="chev" size={14} />
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

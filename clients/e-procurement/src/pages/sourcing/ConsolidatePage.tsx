import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQueries, useQuery } from "@tanstack/react-query";
import {
  listRequisitions,
  getRequisition,
  reserveRequisitionLine,
  unreserveRequisitionLine,
  createRfqDraft,
  type PrLineDto,
  type RequisitionDto,
  type RfqLineInput,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { Modal, Notice } from "@/components/ui";
import { fmt, dateMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type Source = { prId: string; prCode: string; lineId: string; qty: number };
type BasketLine = { itemCode: string; description: string; uom: string; rate: number; sources: Source[] };
type MergePrompt = { existingIdx: number; incoming: BasketLine };

const lineQty = (b: BasketLine) => b.sources.reduce((s, x) => s + x.qty, 0);
const isLineSourceable = (l: PrLineDto) => l.lifecycleStatus === "Open";

type FKey = "department" | "category" | "location";
const FILTERS: { key: FKey; label: string }[] = [
  { key: "department", label: "Dept" },
  { key: "category", label: "Category" },
  { key: "location", label: "Location" },
];

/**
 * Phase 2 — multi-PR RFQ consolidation basket, ported from tmp-old-port/Consolidate.tsx.
 * The list endpoint doesn't include lines, so Submitted/PartiallySourced PRs are hydrated in
 * parallel via useQueries before the basket UI (left pane) can render open lines.
 */
export function ConsolidatePage({ onOpenRfq, onBack }: { onOpenRfq: (id: string) => void; onBack: () => void }) {
  const { data: prList = [] } = useQuery({ queryKey: ["requisitions"], queryFn: listRequisitions });
  const sourceablePrs = useMemo(
    () => prList.filter((p) => p.headerStatus === "Submitted" || p.headerStatus === "PartiallySourced"),
    [prList],
  );
  const detailResults = useQueries({
    queries: sourceablePrs.map((p) => ({ queryKey: ["requisition", p.id], queryFn: () => getRequisition(p.id) })),
  });
  const detailsLoading = detailResults.some((r) => r.isPending);
  const prs = useMemo(() => detailResults.map((r) => r.data).filter((d): d is RequisitionDto => !!d), [detailResults]);

  const [basket, setBasket] = useState<BasketLine[]>([]);
  const [merge, setMerge] = useState<MergePrompt | null>(null);
  const [uomNote, setUomNote] = useState<{ code: string; a: string; b: string } | null>(null);
  const [mergeAll, setMergeAll] = useState<{
    toAdd: { pr: RequisitionDto; l: PrLineDto }[];
    groups: { itemCode: string; uom: string; sources: { prCode: string; qty: number }[] }[];
  } | null>(null);
  const [provOpen, setProvOpen] = useState<Record<number, boolean>>({});
  const [filters, setFilters] = useState<Record<FKey, string[]>>({ department: [], category: [], location: [] });
  const [search, setSearch] = useState("");
  const [openFilter, setOpenFilter] = useState<FKey | null>(null);
  const [expand, setExpand] = useState<Record<string, boolean>>({});
  const [err, setErr] = useState<string | null>(null);

  // basketRef is the synchronous source of truth so a burst of add()s (e.g. "+ N open") each see
  // the latest basket — avoids a stale-closure race where two same-item lines miss the merge check.
  const basketRef = useRef(basket);
  basketRef.current = basket;
  const commit = (next: BasketLine[]) => {
    basketRef.current = next;
    setBasket(next);
  };
  const proceeding = useRef(false);
  useEffect(
    () => () => {
      if (proceeding.current) return;
      basketRef.current.forEach((b) => b.sources.forEach((s) => void unreserveRequisitionLine(s.prId, s.lineId).catch(() => {})));
    },
    [],
  );

  const reserve = useMutation({ mutationFn: (v: { prId: string; lineId: string }) => reserveRequisitionLine(v.prId, v.lineId) });
  const unreserve = useMutation({ mutationFn: (v: { prId: string; lineId: string }) => unreserveRequisitionLine(v.prId, v.lineId) });

  // A line is "claimed by this basket" if it appears in a basket source or the pending merge incoming.
  const claimed = new Set<string>([
    ...basket.flatMap((b) => b.sources.map((s) => s.lineId)),
    ...(merge ? merge.incoming.sources.map((s) => s.lineId) : []),
  ]);
  const showLine = (l: PrLineDto) => isLineSourceable(l) || claimed.has(l.id);

  const uniq = (key: FKey) => [...new Set(prs.map((p) => (p[key] ?? "") as string).filter(Boolean))];
  const matchSearch = (pr: RequisitionDto) => {
    const s = search.trim().toLowerCase();
    if (!s) return true;
    const lines = pr.lines ?? [];
    return [pr.code, pr.requestor, ...lines.flatMap((l) => [l.itemCode, l.description])]
      .some((v) => (v ?? "").toLowerCase().includes(s));
  };
  const leftPrs = prs
    .filter((p) => FILTERS.every((f) => filters[f.key].length === 0 || filters[f.key].includes((p[f.key] ?? "") as string)))
    .filter(matchSearch)
    .map((pr) => ({ pr, lines: (pr.lines ?? []).filter(showLine) }))
    .filter((g) => g.lines.length > 0);

  const inBasket = (lineId: string) => claimed.has(lineId);

  type AddMode = "ask" | "merge" | "separate";
  const addLocal = (pr: RequisitionDto, l: PrLineDto, mode: AddMode = "ask") => {
    const source: Source = { prId: pr.id, prCode: pr.code, lineId: l.id, qty: l.qty };
    const incoming: BasketLine = { itemCode: l.itemCode, description: l.description, uom: l.uom, rate: l.estUnitPrice, sources: [source] };
    const cur = basketRef.current;
    const exIdx = cur.findIndex((b) => b.itemCode === l.itemCode);
    if (exIdx >= 0) {
      if (cur[exIdx]!.uom !== incoming.uom) {
        commit([...cur, incoming]);
        setUomNote({ code: l.itemCode, a: cur[exIdx]!.uom, b: incoming.uom });
        return;
      }
      if (mode === "merge") {
        commit(cur.map((x, i) => (i === exIdx ? { ...x, sources: [...x.sources, source] } : x)));
        return;
      }
      if (mode === "ask") {
        setMerge({ existingIdx: exIdx, incoming });
        return;
      }
    }
    commit([...cur, incoming]);
  };

  const add = (pr: RequisitionDto, l: PrLineDto) => {
    if (inBasket(l.id)) return;
    setErr(null);
    reserve.mutate(
      { prId: pr.id, lineId: l.id },
      { onSuccess: () => addLocal(pr, l), onError: (e: Error) => setErr(e.message) },
    );
  };
  const addBulk = (pr: RequisitionDto, l: PrLineDto, mode: AddMode) => {
    if (inBasket(l.id)) return;
    addLocal(pr, l, mode);
    void reserveRequisitionLine(pr.id, l.id).catch((e: unknown) => setErr(e instanceof Error ? e.message : "Could not reserve a line."));
  };
  const mergeGroupsFor = (toAdd: { pr: RequisitionDto; l: PrLineDto }[]) => {
    const groups = new Map<string, { itemCode: string; uom: string; sources: { prCode: string; qty: number }[]; hasNew: boolean }>();
    basketRef.current.forEach((b) =>
      groups.set(`${b.itemCode} ${b.uom}`, { itemCode: b.itemCode, uom: b.uom, sources: b.sources.map((s) => ({ prCode: s.prCode, qty: s.qty })), hasNew: false }),
    );
    toAdd.forEach(({ pr, l }) => {
      const key = `${l.itemCode} ${l.uom}`;
      const g = groups.get(key) ?? { itemCode: l.itemCode, uom: l.uom, sources: [], hasNew: false };
      g.sources.push({ prCode: pr.code, qty: l.qty });
      g.hasNew = true;
      groups.set(key, g);
    });
    return [...groups.values()].filter((g) => g.hasNew && g.sources.length > 1).map(({ itemCode, uom, sources }) => ({ itemCode, uom, sources }));
  };
  const doBulkAdd = (toAdd: { pr: RequisitionDto; l: PrLineDto }[], mode: AddMode) => {
    toAdd.forEach(({ pr, l }) => addBulk(pr, l, mode));
    setMergeAll(null);
  };
  const startBulkAdd = (toAdd: { pr: RequisitionDto; l: PrLineDto }[]) => {
    const pending = toAdd.filter(({ l }) => !inBasket(l.id));
    if (pending.length === 0) return;
    const groups = mergeGroupsFor(pending);
    if (groups.length > 0) setMergeAll({ toAdd: pending, groups });
    else doBulkAdd(pending, "merge");
  };
  const addAll = (g: { pr: RequisitionDto; lines: PrLineDto[] }) =>
    startBulkAdd(g.lines.filter((l) => l.lifecycleStatus === "Open").map((l) => ({ pr: g.pr, l })));
  const addAllShown = () =>
    startBulkAdd(leftPrs.flatMap((g) => g.lines.filter((l) => l.lifecycleStatus === "Open").map((l) => ({ pr: g.pr, l }))));
  const addableCount = leftPrs.reduce((n, g) => n + g.lines.filter((l) => l.lifecycleStatus === "Open" && !inBasket(l.id)).length, 0);
  const expandAll = () => setExpand({});
  const collapseAll = () => setExpand(Object.fromEntries(leftPrs.map((g) => [g.pr.id, false])));

  const doMerge = () => {
    if (!merge) return;
    commit(basketRef.current.map((x, i) => (i === merge.existingIdx ? { ...x, sources: [...x.sources, ...merge.incoming.sources] } : x)));
    setMerge(null);
  };
  const keepSeparate = () => {
    if (!merge) return;
    commit([...basketRef.current, merge.incoming]);
    setMerge(null);
  };

  const removeAt = (idx: number) => {
    const removed = basketRef.current[idx];
    removed?.sources.forEach((s) => unreserve.mutate({ prId: s.prId, lineId: s.lineId }));
    commit(basketRef.current.filter((_, i) => i !== idx));
    setProvOpen({});
  };
  const clearBasket = () => {
    basketRef.current.forEach((b) => b.sources.forEach((s) => unreserve.mutate({ prId: s.prId, lineId: s.lineId })));
    commit([]);
    setMerge(null);
    setMergeAll(null);
    setUomNote(null);
    setProvOpen({});
  };
  const move = (idx: number, dir: -1 | 1) => {
    const j = idx + dir;
    if (j < 0 || j >= basketRef.current.length) return;
    const n = [...basketRef.current];
    [n[idx], n[j]] = [n[j]!, n[idx]!];
    commit(n);
    setProvOpen({});
  };

  const build = useMutation({
    mutationFn: () => {
      const lines: RfqLineInput[] = basket.map((b, i) => ({
        lineCode: `L${i + 1}`,
        itemCode: b.itemCode,
        description: b.description,
        qty: lineQty(b),
        uom: b.uom,
        prRef: b.sources[0]!.prCode,
        sourcePrLineIds: b.sources.map((s) => s.lineId),
      }));
      const prRefs = [...new Set(basket.flatMap((b) => b.sources.map((s) => s.prId)))];
      return createRfqDraft({ title: null, envelope: "Dual", currency: "MYR", prRefs, lines });
    },
    onSuccess: (id) => {
      proceeding.current = true;
      onOpenRfq(id);
    },
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const nLines = basket.length;
  const nPrs = new Set(basket.flatMap((b) => b.sources.map((s) => s.prCode))).size;
  const total = basket.reduce((s, b) => s + lineQty(b) * b.rate, 0);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Requisitions
        </button>{" "}
        <Icon name="chev" size={13} /> <span>RFQ Workspace</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>RFQ Workspace</h1>
          <p>
            Pull open PR lines from across multiple requisitions into one RFQ. The one-click <b>Confirm lines</b> flow
            on the requisitions list still works for simple single-PR sourcing — this is for multi-PR consolidation.
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>
          {err}
        </Notice>
      ) : null}

      {openFilter ? <div className="mscrim" onClick={() => setOpenFilter(null)} /> : null}
      <div className="filterbar2">
        {FILTERS.map((f) => {
          const sel = filters[f.key];
          const summ = sel.length === 0 ? "All" : sel.length === 1 ? sel[0] : `${sel.length} selected`;
          const open = openFilter === f.key;
          return (
            <div className="msel" key={f.key}>
              <label>{f.label}</label>
              <button
                type="button"
                className={`mbtn ${sel.length ? "has" : ""}`}
                aria-label={`Filter by ${f.label}`}
                onClick={(e) => {
                  e.stopPropagation();
                  setOpenFilter(open ? null : f.key);
                }}
              >
                <span>{summ}</span>
                <span className={`mchev ${open ? "up" : ""}`}>
                  <Icon name="chev" size={12} />
                </span>
              </button>
              {open ? (
                <div className="mpop" onClick={(e) => e.stopPropagation()}>
                  {uniq(f.key).map((v) => (
                    <label className="mopt" key={v}>
                      <input
                        type="checkbox"
                        checked={sel.includes(v)}
                        onChange={(e) =>
                          setFilters((x) => ({ ...x, [f.key]: e.target.checked ? [...x[f.key], v] : x[f.key].filter((y) => y !== v) }))
                        }
                      />{" "}
                      {v}
                    </label>
                  ))}
                </div>
              ) : null}
            </div>
          );
        })}
        <div className="msel" style={{ flex: 1, minWidth: 220 }}>
          <label>Search</label>
          <input type="text" value={search} placeholder="item, code, PR, requestor…" onChange={(e) => setSearch(e.target.value)} aria-label="Search open lines" />
        </div>
        <button
          type="button"
          className="freset"
          onClick={() => {
            setFilters({ department: [], category: [], location: [] });
            setSearch("");
          }}
        >
          Reset
        </button>
      </div>

      <div className="cl-wrap">
        <div className="cl-pane">
          <div className="cl-paneh">
            <h3>Open PR lines</h3>
            <span className="ct">
              {leftPrs.length} PR{leftPrs.length === 1 ? "" : "s"}
            </span>
            <div className="cl-paneh-actions">
              <button type="button" className="freset" disabled={leftPrs.length === 0} onClick={expandAll}>
                Expand all
              </button>
              <button type="button" className="freset" disabled={leftPrs.length === 0} onClick={collapseAll}>
                Collapse all
              </button>
              <button type="button" className="btn btn-out btn-sm" disabled={addableCount === 0} onClick={addAllShown}>
                <Icon name="plus" size={13} /> Add all{addableCount > 0 ? ` (${addableCount})` : ""}
              </button>
            </div>
          </div>
          <div className="cl-body">
            {detailsLoading ? <div className="cl-empty">Loading open PR lines…</div> : null}
            {!detailsLoading && leftPrs.length === 0 ? <div className="cl-empty">No PRs with open lines match these filters.</div> : null}
            {leftPrs.map(({ pr, lines }) => {
              const open = expand[pr.id] !== false;
              const openCount = lines.filter((l) => l.lifecycleStatus === "Open" && !inBasket(l.id)).length;
              return (
                <div className="prg" key={pr.id}>
                  <div className="prg-h" onClick={() => setExpand((x) => ({ ...x, [pr.id]: x[pr.id] === false }))}>
                    <span className="chev" style={{ transform: open ? "rotate(90deg)" : "" }}>
                      <Icon name="chev" size={13} />
                    </span>
                    <span className="prg-meta">
                      <span className="prg-1">
                        <span className="prid">{pr.code}</span>
                        <span className="needby">
                          <Icon name="clock" size={11} /> {dateMY(pr.requiredOn)}
                        </span>
                      </span>
                      <span className="prg-2">
                        {pr.requestor} · {pr.department} · {pr.category}
                      </span>
                    </span>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      disabled={openCount === 0}
                      onClick={(e) => {
                        e.stopPropagation();
                        addAll({ pr, lines });
                      }}
                    >
                      + {openCount} open
                    </button>
                  </div>
                  {open
                    ? lines.map((l) => {
                        const used = inBasket(l.id);
                        return (
                          <div className={`prg-line ${used ? "used" : ""}`} key={l.id}>
                            <span className="lc">
                              <div className="desc">{l.description}</div>
                              <div className="sub">
                                {l.itemCode} · {l.qty} {l.uom} · {fmt(l.qty * l.estUnitPrice)}
                              </div>
                            </span>
                            <button
                              type="button"
                              className="cl-add"
                              disabled={used}
                              title={used ? "Already in basket" : "Add to RFQ"}
                              aria-label={`Add ${l.itemCode} from ${pr.code}`}
                              onClick={() => add(pr, l)}
                            >
                              {used ? "✓" : "+"}
                            </button>
                          </div>
                        );
                      })
                    : null}
                </div>
              );
            })}
          </div>
        </div>

        <div className="cl-pane">
          <div className="cl-paneh">
            <h3>
              <Icon name="rfq" size={15} /> RFQ basket
            </h3>
            <span className="ct">
              {nLines} line{nLines === 1 ? "" : "s"}
            </span>
          </div>
          <div className="cl-body">
            {uomNote ? (
              <div className="cl-uomnote">
                <Icon name="eye" size={13} /> Same item code <b>{uomNote.code}</b> but different UoM (<b>{uomNote.a}</b> vs{" "}
                <b>{uomNote.b}</b>) — can&apos;t sum, kept as separate lines.
                <button type="button" className="lnk" onClick={() => setUomNote(null)}>
                  Dismiss
                </button>
              </div>
            ) : null}
            {basket.length === 0 ? (
              <div className="cl-empty">
                Click <b>+</b> on the left to add open PR lines.
                <br />
                <span className="hint">Lines you don&apos;t add stay open for future RFQs.</span>
              </div>
            ) : null}
            {basket.map((b, idx) => {
              const merged = b.sources.length > 1;
              const pOpen = provOpen[idx];
              return (
                <div className={`bk-line ${merged ? "merged" : ""}`} key={`${b.itemCode}-${idx}`}>
                  <span className="bk-seq">
                    <button type="button" className="seqb" disabled={idx === 0} onClick={() => move(idx, -1)} aria-label="Move up">
                      ▲
                    </button>
                    <button type="button" className="seqb" disabled={idx === basket.length - 1} onClick={() => move(idx, 1)} aria-label="Move down">
                      ▼
                    </button>
                  </span>
                  <span className="lc">
                    <div className="desc">
                      {merged ? <Icon name="box" size={12} /> : null} {b.description}
                    </div>
                    <div className="sub">
                      {b.itemCode} · {b.uom}
                    </div>
                    {merged ? (
                      <>
                        <button type="button" className="prov-tog" onClick={() => setProvOpen((x) => ({ ...x, [idx]: !x[idx] }))}>
                          <span className="chev" style={{ transform: pOpen ? "rotate(90deg)" : "" }}>
                            <Icon name="chev" size={11} />
                          </span>{" "}
                          Merged from {b.sources.length} PRs
                        </button>
                        {pOpen ? (
                          <div className="provlist">
                            {b.sources.map((s) => (
                              <div className="provrow" key={s.lineId}>
                                <span>{s.prCode}</span>
                                <span className="amt">
                                  {s.qty} {b.uom}
                                </span>
                              </div>
                            ))}
                          </div>
                        ) : null}
                      </>
                    ) : (
                      <div className="prov-from">from {b.sources[0]!.prCode}</div>
                    )}
                  </span>
                  <span className="bk-qty">
                    {lineQty(b)} {b.uom}
                  </span>
                  <button type="button" className="bk-rm" title="Send back — line returns to open" aria-label={`Remove ${b.itemCode}`} onClick={() => removeAt(idx)}>
                    −
                  </button>
                </div>
              );
            })}
          </div>
          <div className="cl-foot">
            <div className="cl-summ">
              <span>
                <b>{nLines}</b> lines
              </span>
              <span>
                from <b>{nPrs}</b> PR{nPrs === 1 ? "" : "s"}
              </span>
              <span>
                est. <b>{fmt(total)}</b>
              </span>
            </div>
            <div style={{ flex: 1 }} />
            <button type="button" className="btn btn-ghost btn-sm" disabled={!nLines} onClick={clearBasket}>
              Clear
            </button>
            <button type="button" className="btn btn-pri" disabled={!nLines || build.isPending} onClick={() => build.mutate()}>
              Build RFQ <Icon name="chev" size={14} />
            </button>
          </div>
        </div>
      </div>

      {merge ? (
        <Modal
          title="Merge into one line?"
          icon="box"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={keepSeparate}>
                Keep separate
              </button>
              <button type="button" className="btn btn-pri" onClick={doMerge}>
                <Icon name="box" size={15} /> Merge (sum qty)
              </button>
            </>
          }
        >
          <p style={{ marginTop: 0 }}>
            Item code <b>{merge.incoming.itemCode}</b> is already in the basket at the same UoM (<b>{merge.incoming.uom}</b>). Merge
            them into one RFQ line — quantities summed, provenance kept — or add it as a separate line?
          </p>
        </Modal>
      ) : null}

      {mergeAll ? (
        <Modal
          title={mergeAll.groups.length === 1 ? "Merge these lines?" : `Merge ${mergeAll.groups.length} sets of lines?`}
          icon="box"
          footer={
            <>
              <button type="button" className="btn btn-ghost" onClick={() => setMergeAll(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-out" onClick={() => doBulkAdd(mergeAll.toAdd, "separate")}>
                Keep separate
              </button>
              <button type="button" className="btn btn-pri" onClick={() => doBulkAdd(mergeAll.toAdd, "merge")}>
                <Icon name="box" size={15} /> Merge &amp; add all
              </button>
            </>
          }
        >
          <p style={{ marginTop: 0 }}>
            Adding <b>{mergeAll.toAdd.length}</b> line{mergeAll.toAdd.length === 1 ? "" : "s"}. These item codes appear at the same UoM
            in more than one PR — merging sums their quantities into one RFQ line each (provenance kept). Confirm all, or keep every
            line separate.
          </p>
          <div className="cl-mergelist">
            {mergeAll.groups.map((g) => {
              const sum = g.sources.reduce((n, s) => n + s.qty, 0);
              return (
                <div className="cl-mergegrp" key={`${g.itemCode} ${g.uom}`}>
                  <div className="mg-head">
                    <Icon name="box" size={13} /> <b>{g.itemCode}</b> · {g.uom} → <b>{sum} {g.uom}</b>
                  </div>
                  <div className="mg-src">
                    {g.sources.map((s, i) => (
                      <span key={`${s.prCode}-${i}`}>
                        {i > 0 && " + "}
                        {s.qty} {g.uom} <span className="mut">({s.prCode})</span>
                      </span>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </Modal>
      ) : null}
    </>
  );
}

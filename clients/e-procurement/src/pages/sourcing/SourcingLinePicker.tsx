import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQueries, useQuery } from "@tanstack/react-query";
import {
  listRequisitions,
  getRequisition,
  reserveRequisitionLine,
  unreserveRequisitionLine,
  type PrLineDto,
  type RequisitionDto,
  type RfqLineInput,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { Modal, Notice } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { fmt, dateMY } from "@/lib/format";
import { useAuth } from "@/auth/use-auth";

// ============================================================================
// SourcingLinePicker — shared picker for PR lines into an RFQ.
//
// Two modes:
//  - 'workspace': multi-basket builder (RFQ Workspace). Named basket tabs,
//    Build RFQ / Build all, localStorage persistence, leave-awareness toast.
//  - 'drawer': single implicit basket for the RfqDraftEditPage "Add PRs"
//    overlay. Preloaded lines shown as "In this RFQ" rows. Apply returns the
//    full line set. Unmount unreserves only newly-added (not pre) lines.
//    No localStorage, no basket tabs, no Build verbs.
// ============================================================================

type DraftLine = RfqLineInput & { sourcePrLineIds?: string[] | null };

type Source = { prId: string; prCode: string; lineId: string; qty: number };
type BasketLine = {
  itemCode: string; description: string; uom: string; rate: number; sources: Source[];
  pre?: { dto: DraftLine };
  removed?: boolean;
};
type Basket = { id: number; name: string; lines: BasketLine[] };
type MergePrompt = { basketId: number; existingIdx: number; incoming: BasketLine };
type StoredWorkspace = { baskets: Basket[]; activeId: number; seq: number };

const newQty = (b: BasketLine) => b.sources.reduce((s, x) => s + x.qty, 0);
const lineQty = (b: BasketLine) => (b.pre ? (b.pre.dto.qty ?? 0) : 0) + newQty(b);
const isLineSourceable = (l: PrLineDto) => l.lifecycleStatus === "Open";
const isExtStatus = (status: string) => status === "InDraftRfq" || status === "InRfq";

type FKey = "department" | "category" | "location" | "project";
const FILTERS: { key: FKey; label: string }[] = [
  { key: "department", label: "Dept" },
  { key: "category", label: "Category" },
  { key: "location", label: "Location" },
  { key: "project", label: "Project" },
];

const SOURCEABLE_HEADER_STATUSES = new Set(["Submitted", "PartiallySourced", "PartiallyOrdered"]);
const TIGHT_SOURCING_WINDOW_DAYS = 14;
const daysUntil = (iso: string) => {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((new Date(`${iso.slice(0, 10)}T00:00:00`).getTime() - today.getTime()) / 86400000);
};
const LEAVE_TOAST_KEY = "rfq-workspace-leave-toasted";

export function SourcingLinePicker({
  mode,
  preloaded,
  onBuild,
  onBuildAll,
  onApply,
  onBack,
  onOpenRfq,
  onOpenRfqList,
}: {
  mode: "workspace" | "drawer";
  preloaded?: DraftLine[];
  onBuild?: (title: string, lines: RfqLineInput[], prRefs: string[]) => Promise<string>;
  onBuildAll?: (batches: { title: string; lines: RfqLineInput[]; prRefs: string[] }[]) => Promise<void>;
  onApply?: (lines: RfqLineInput[]) => void;
  onBack?: () => void;
  onOpenRfq?: (id: string) => void;
  onOpenRfqList?: () => void;
}) {
  const { user } = useAuth();
  const storeKey = `rfq-workspace-baskets-${user?.id ?? "anon"}`;

  const { data: prList = [], isPending: listPending } = useQuery({ queryKey: ["requisitions"], queryFn: listRequisitions });
  const sourceablePrs = useMemo(() => prList.filter((p) => SOURCEABLE_HEADER_STATUSES.has(p.headerStatus)), [prList]);
  const detailResults = useQueries({
    queries: sourceablePrs.map((p) => ({ queryKey: ["requisition", p.id], queryFn: () => getRequisition(p.id) })),
  });
  const detailsPending = detailResults.some((r) => r.isPending);
  const loading = listPending || detailsPending;
  const prs = useMemo(() => detailResults.map((r) => r.data).filter((d): d is RequisitionDto => !!d), [detailResults]);

  const [baskets, setBaskets] = useState<Basket[]>([{ id: 1, name: "Basket 1", lines: [] }]);
  const [activeId, setActiveId] = useState(1);
  const [renaming, setRenaming] = useState<number | null>(null);
  const [renameDraft, setRenameDraft] = useState("");
  const focusRename = useCallback((el: HTMLInputElement | null) => el?.focus(), []);
  const [delPrompt, setDelPrompt] = useState<number | null>(null);
  const [moveMenu, setMoveMenu] = useState<number | null>(null);
  const [movePrompt, setMovePrompt] = useState<{ pr: RequisitionDto; l: PrLineDto; from: Basket } | null>(null);
  const [merge, setMerge] = useState<MergePrompt | null>(null);
  const [uomNote, setUomNote] = useState<{ code: string; a: string; b: string } | null>(null);
  const [mergeAll, setMergeAll] = useState<{
    toAdd: { pr: RequisitionDto; l: PrLineDto }[];
    groups: { itemCode: string; uom: string; sources: { prCode: string; qty: number }[] }[];
  } | null>(null);
  const [provOpen, setProvOpen] = useState<Record<number, boolean>>({});
  const [groupBy, setGroupBy] = useState<"pr" | "item">("pr");
  const [filters, setFilters] = useState<Record<FKey, string[]>>({ department: [], category: [], location: [], project: [] });
  const [search, setSearch] = useState("");
  const [openFilter, setOpenFilter] = useState<FKey | null>(null);
  const [expand, setExpand] = useState<Record<string, boolean>>({});
  const [err, setErr] = useState<string | null>(null);
  const [restoreNotice, setRestoreNotice] = useState<string | null>(null);
  const [leaveToast, setLeaveToast] = useState<string | null>(null);

  const basketsRef = useRef(baskets);
  basketsRef.current = baskets;
  const activeIdRef = useRef(activeId);
  activeIdRef.current = activeId;
  const seqRef = useRef(1);
  const restored = useRef(mode !== "workspace");
  const proceeding = useRef(false);

  const save = (bks: Basket[], act: number) => {
    if (mode !== "workspace" || !restored.current) return;
    try {
      localStorage.setItem(storeKey, JSON.stringify({ baskets: bks, activeId: act, seq: seqRef.current } satisfies StoredWorkspace));
    } catch { /* best-effort */ }
  };
  const commitBaskets = (next: Basket[]) => {
    basketsRef.current = next;
    setBaskets(next);
    save(next, activeIdRef.current);
  };
  const setActive = (id: number) => {
    activeIdRef.current = id;
    setActiveId(id);
    setProvOpen({});
    setMoveMenu(null);
    save(basketsRef.current, id);
  };

  const activeBasket = baskets.find((b) => b.id === activeId) ?? baskets[0]!;
  const basket = activeBasket.lines;
  const curLines = () => (basketsRef.current.find((b) => b.id === activeIdRef.current) ?? basketsRef.current[0]!).lines;
  const commit = (next: BasketLine[]) =>
    commitBaskets(basketsRef.current.map((b) => (b.id === activeIdRef.current ? { ...b, lines: next } : b)));

  // Drawer unmount cleanup: unreserve only newly-added lines (not preloaded).
  useEffect(() => () => {
    if (mode !== "drawer" || proceeding.current) return;
    basketsRef.current.flatMap((b) => b.lines).forEach((b) =>
      b.sources.forEach((s) => { void unreserveRequisitionLine(s.prId, s.lineId).catch(() => {}); }),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Drawer: seed basket from preloaded lines once PR data is ready.
  const seeded = useRef(false);
  useEffect(() => {
    if (mode !== "drawer" || seeded.current || loading) return;
    seeded.current = true;
    commit(
      (preloaded ?? []).map((dto) => {
        const hit = prs
          .flatMap((p) => p.lines.map((l) => ({ p, l })))
          .find(({ p, l }) => (dto.sourcePrLineIds ?? []).includes(l.id) || (p.code === dto.prRef && l.itemCode === dto.itemCode));
        return {
          itemCode: dto.itemCode,
          description: dto.description,
          uom: dto.uom,
          rate: hit?.l.estUnitPrice ?? 0,
          sources: [],
          pre: { dto },
        };
      }),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, loading]);

  // Workspace: restore persisted baskets.
  useEffect(() => {
    if (mode !== "workspace" || restored.current || loading) return;
    restored.current = true;
    let stored: StoredWorkspace | null = null;
    try {
      stored = JSON.parse(localStorage.getItem(storeKey) ?? "null") as StoredWorkspace | null;
    } catch { stored = null; }
    if (!stored || !Array.isArray(stored.baskets) || stored.baskets.length === 0) return;
    const fresh = new Map(prs.flatMap((p) => p.lines.map((l) => [l.id, l.lifecycleStatus])));
    let dropped = 0;
    const valid = stored.baskets.map((b) => ({
      id: b.id,
      name: b.name,
      lines: (b.lines ?? [])
        .map((x) => {
          const keep = x.sources.filter((s) => fresh.get(s.lineId) === "InDraftRfq");
          dropped += x.sources.length - keep.length;
          return { ...x, sources: keep };
        })
        .filter((x) => x.sources.length > 0),
    }));
    if (valid.length === 0) return;
    seqRef.current = Math.max(stored.seq ?? 1, ...valid.map((b) => b.id));
    activeIdRef.current = valid.some((b) => b.id === stored.activeId) ? stored.activeId : valid[0]!.id;
    setActiveId(activeIdRef.current);
    commitBaskets(valid);
    if (dropped > 0) setRestoreNotice(`${dropped} line(s) removed from your basket — no longer available.`);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loading]);

  // Workspace: leave-awareness toast.
  const maybeToastLeave = () => {
    if (mode !== "workspace") return;
    const n = basketsRef.current.reduce((s, b) => s + b.lines.filter((x) => !x.removed).length, 0);
    if (n === 0) return;
    try {
      if (sessionStorage.getItem(LEAVE_TOAST_KEY)) return;
      sessionStorage.setItem(LEAVE_TOAST_KEY, "1");
    } catch { return; }
    setLeaveToast(`Your basket is saved — ${n} line(s) reserved`);
    window.setTimeout(() => setLeaveToast(null), 4500);
  };
  useEffect(() => {
    if (mode !== "workspace") return;
    const onPop = () => maybeToastLeave();
    window.addEventListener("popstate", onPop);
    return () => window.removeEventListener("popstate", onPop);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  const goBack = () => {
    maybeToastLeave();
    onBack?.();
  };

  const reserve = useMutation({ mutationFn: (v: { prId: string; lineId: string }) => reserveRequisitionLine(v.prId, v.lineId) });
  const unreserve = useMutation({ mutationFn: (v: { prId: string; lineId: string }) => unreserveRequisitionLine(v.prId, v.lineId) });

  const allLines = baskets.flatMap((b) => b.lines);
  const claimed = new Set<string>([
    ...allLines.flatMap((b) => b.sources.map((s) => s.lineId)),
    ...(merge ? merge.incoming.sources.map((s) => s.lineId) : []),
    ...allLines.flatMap((b) => b.pre?.dto.sourcePrLineIds ?? []),
  ]);
  const claimedKeys = new Set<string>(allLines.filter((b) => b.pre).map((b) => `${b.pre!.dto.prRef}:${b.itemCode}`));
  const inBasket = (pr: RequisitionDto, l: PrLineDto) => claimed.has(l.id) || claimedKeys.has(`${pr.code}:${l.itemCode}`);
  const owningBasket = (pr: RequisitionDto, l: PrLineDto) =>
    baskets.find((b) =>
      b.lines.some(
        (x) =>
          x.sources.some((s) => s.lineId === l.id) ||
          (!!x.pre && ((x.pre.dto.sourcePrLineIds ?? []).includes(l.id) || `${x.pre.dto.prRef}:${x.itemCode}` === `${pr.code}:${l.itemCode}`)),
      ),
    );
  const isExt = (pr: RequisitionDto, l: PrLineDto) => !inBasket(pr, l) && isExtStatus(l.lifecycleStatus);
  const showLine = (pr: RequisitionDto, l: PrLineDto) => isLineSourceable(l) || inBasket(pr, l) || isExt(pr, l);

  const uniq = (key: FKey) => [...new Set(prs.map((p) => (p[key] ?? "") as string).filter(Boolean))];
  const matchSearch = (pr: RequisitionDto) => {
    const q = search.trim().toLowerCase();
    if (!q) return true;
    return [pr.code, pr.requestor, ...pr.lines.flatMap((l) => [l.itemCode, l.description])].some((v) =>
      (v ?? "").toLowerCase().includes(q),
    );
  };
  const leftPrs = prs
    .filter((p) => SOURCEABLE_HEADER_STATUSES.has(p.headerStatus))
    .filter((p) => FILTERS.every((f) => filters[f.key].length === 0 || filters[f.key].includes((p[f.key] ?? "") as string)))
    .filter(matchSearch)
    .map((pr) => ({ pr, lines: pr.lines.filter((l) => showLine(pr, l)) }))
    .filter((g) => g.lines.length > 0);

  const lineFacts = new Map(
    prs.flatMap((p) => p.lines.map((l) => [l.id, { rate: l.estUnitPrice, requiredDate: p.requiredOn ?? null, category: (p.category ?? "").trim() }])),
  );
  const rowRates = (b: BasketLine) => {
    const rs = b.sources.map((s) => lineFacts.get(s.lineId)?.rate ?? b.rate);
    if (b.pre || rs.length === 0) rs.push(b.rate);
    return rs;
  };
  const rowAmount = (b: BasketLine) =>
    (b.pre ? (b.pre.dto.qty ?? 0) * b.rate : 0) +
    b.sources.reduce((n, s) => n + s.qty * (lineFacts.get(s.lineId)?.rate ?? b.rate), 0);
  const rowEarliest = (b: BasketLine): string | null => {
    const ds = [
      ...b.sources.map((s) => lineFacts.get(s.lineId)?.requiredDate),
      ...(b.pre?.dto.sourcePrLineIds ?? []).map((id) => lineFacts.get(id)?.requiredDate),
    ].filter((d): d is string => !!d);
    return ds.length ? ds.reduce((a, c) => (c < a ? c : a)) : null;
  };

  const itemGroups = useMemo(() => {
    const m = new Map<string, { itemCode: string; description: string; rows: { pr: RequisitionDto; l: PrLineDto }[] }>();
    leftPrs.forEach(({ pr, lines }) =>
      lines.forEach((l) => {
        const g = m.get(l.itemCode) ?? { itemCode: l.itemCode, description: l.description, rows: [] };
        g.rows.push({ pr, l });
        m.set(g.itemCode, g);
      }),
    );
    return [...m.values()].sort((a, b) => a.itemCode.localeCompare(b.itemCode));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [leftPrs]);

  type AddMode = "ask" | "merge" | "separate";
  const addLocal = (pr: RequisitionDto, l: PrLineDto, addMode: AddMode = "ask") => {
    const source: Source = { prId: pr.id, prCode: pr.code, lineId: l.id, qty: l.qty };
    const incoming: BasketLine = { itemCode: l.itemCode, description: l.description, uom: l.uom, rate: l.estUnitPrice, sources: [source] };
    const cur = curLines();
    const exIdx = cur.findIndex((b) => !b.removed && b.itemCode === l.itemCode);
    if (exIdx >= 0) {
      if (cur[exIdx]!.uom !== incoming.uom) {
        commit([...cur, incoming]);
        setUomNote({ code: l.itemCode, a: cur[exIdx]!.uom, b: incoming.uom });
        return;
      }
      if (addMode === "merge") {
        commit(cur.map((x, i) => (i === exIdx ? { ...x, sources: [...x.sources, source] } : x)));
        return;
      }
      if (addMode === "ask") {
        setMerge({ basketId: activeIdRef.current, existingIdx: exIdx, incoming });
        return;
      }
    }
    commit([...cur, incoming]);
  };

  const add = (pr: RequisitionDto, l: PrLineDto) => {
    if (inBasket(pr, l)) return;
    setErr(null);
    reserve.mutate(
      { prId: pr.id, lineId: l.id },
      { onSuccess: () => addLocal(pr, l), onError: (e: Error) => setErr(e.message) },
    );
  };
  const addBulk = (pr: RequisitionDto, l: PrLineDto, addMode: AddMode) => {
    if (inBasket(pr, l)) return;
    addLocal(pr, l, addMode);
    void reserveRequisitionLine(pr.id, l.id).catch((e: unknown) => setErr(e instanceof Error ? e.message : "Could not reserve a line."));
  };
  const mergeGroupsFor = (toAdd: { pr: RequisitionDto; l: PrLineDto }[]) => {
    const groups = new Map<string, { itemCode: string; uom: string; sources: { prCode: string; qty: number }[]; hasNew: boolean }>();
    curLines()
      .filter((b) => !b.removed)
      .forEach((b) =>
        groups.set(`${b.itemCode} ${b.uom}`, {
          itemCode: b.itemCode,
          uom: b.uom,
          sources: [
            ...(b.pre ? [{ prCode: b.pre.dto.prRef ?? "this RFQ", qty: b.pre.dto.qty ?? 0 }] : []),
            ...b.sources.map((s) => ({ prCode: s.prCode, qty: s.qty })),
          ],
          hasNew: false,
        }),
      );
    toAdd.forEach(({ pr, l }) => {
      const key = `${l.itemCode} ${l.uom}`;
      const g = groups.get(key) ?? { itemCode: l.itemCode, uom: l.uom, sources: [], hasNew: false };
      g.sources.push({ prCode: pr.code, qty: l.qty });
      g.hasNew = true;
      groups.set(key, g);
    });
    return [...groups.values()]
      .filter((g) => g.hasNew && g.sources.length > 1)
      .map(({ itemCode, uom, sources }) => ({ itemCode, uom, sources }));
  };
  const doBulkAdd = (toAdd: { pr: RequisitionDto; l: PrLineDto }[], addMode: AddMode) => {
    toAdd.forEach(({ pr, l }) => addBulk(pr, l, addMode));
    setMergeAll(null);
  };
  const startBulkAdd = (toAdd: { pr: RequisitionDto; l: PrLineDto }[]) => {
    const pending = toAdd.filter(({ pr, l }) => !inBasket(pr, l));
    if (pending.length === 0) return;
    const groups = mergeGroupsFor(pending);
    if (groups.length > 0) setMergeAll({ toAdd: pending, groups });
    else doBulkAdd(pending, "merge");
  };
  const addAll = (g: { pr: RequisitionDto; lines: PrLineDto[] }) =>
    startBulkAdd(g.lines.filter((l) => l.lifecycleStatus === "Open").map((l) => ({ pr: g.pr, l })));
  const addAllShown = () =>
    startBulkAdd(leftPrs.flatMap((g) => g.lines.filter((l) => l.lifecycleStatus === "Open").map((l) => ({ pr: g.pr, l }))));
  const addableCount = leftPrs.reduce((n, g) => n + g.lines.filter((l) => l.lifecycleStatus === "Open" && !inBasket(g.pr, l)).length, 0);
  const expandAll = () => setExpand({});
  const collapseAll = () => setExpand(Object.fromEntries(leftPrs.map((g) => [g.pr.id, false])));

  const doMerge = () => {
    if (!merge) return;
    commitBaskets(
      basketsRef.current.map((b) =>
        b.id === merge.basketId
          ? { ...b, lines: b.lines.map((x, i) => (i === merge.existingIdx ? { ...x, sources: [...x.sources, ...merge.incoming.sources] } : x)) }
          : b,
      ),
    );
    setMerge(null);
  };
  const keepSeparate = () => {
    if (!merge) return;
    commitBaskets(basketsRef.current.map((b) => (b.id === merge.basketId ? { ...b, lines: [...b.lines, merge.incoming] } : b)));
    setMerge(null);
  };

  // Multi-basket verbs (workspace only)
  const newBasket = () => {
    const id = ++seqRef.current;
    commitBaskets([...basketsRef.current, { id, name: `Basket ${id}`, lines: [] }]);
    setActive(id);
  };
  const commitRename = (id: number, raw: string) => {
    const name = raw.trim();
    setRenaming(null);
    if (!name) return;
    commitBaskets(basketsRef.current.map((b) => (b.id === id ? { ...b, name } : b)));
  };
  const doDeleteBasket = () => {
    const b = basketsRef.current.find((x) => x.id === delPrompt);
    setDelPrompt(null);
    if (!b || basketsRef.current.length <= 1) return;
    b.lines.forEach((x) => x.sources.forEach((s) => void unreserve.mutate({ prId: s.prId, lineId: s.lineId })));
    const next = basketsRef.current.filter((x) => x.id !== b.id);
    if (activeIdRef.current === b.id) {
      activeIdRef.current = next[0]!.id;
      setActiveId(next[0]!.id);
    }
    commitBaskets(next);
    setProvOpen({});
    setMoveMenu(null);
  };
  const placeInBasket = (toId: number, incoming: BasketLine) => {
    const target = basketsRef.current.find((b) => b.id === toId);
    if (!target) return;
    const exIdx = target.lines.findIndex((x) => !x.removed && x.itemCode === incoming.itemCode);
    if (exIdx >= 0) {
      if (target.lines[exIdx]!.uom !== incoming.uom) {
        commitBaskets(basketsRef.current.map((b) => (b.id === toId ? { ...b, lines: [...b.lines, incoming] } : b)));
        setUomNote({ code: incoming.itemCode, a: target.lines[exIdx]!.uom, b: incoming.uom });
        return;
      }
      setMerge({ basketId: toId, existingIdx: exIdx, incoming });
      return;
    }
    commitBaskets(basketsRef.current.map((b) => (b.id === toId ? { ...b, lines: [...b.lines, incoming] } : b)));
  };
  const moveRow = (idx: number, toId: number) => {
    const row = curLines()[idx];
    if (!row || row.pre) return;
    commitBaskets(basketsRef.current.map((b) => (b.id === activeIdRef.current ? { ...b, lines: b.lines.filter((_, i) => i !== idx) } : b)));
    placeInBasket(toId, row);
    setProvOpen({});
    setMoveMenu(null);
  };
  const doMoveFromOther = () => {
    if (!movePrompt) return;
    const { pr, l, from } = movePrompt;
    setMovePrompt(null);
    commitBaskets(
      basketsRef.current.map((b) =>
        b.id === from.id
          ? { ...b, lines: b.lines.map((x) => ({ ...x, sources: x.sources.filter((s) => s.lineId !== l.id) })).filter((x) => x.sources.length > 0 || x.pre) }
          : b,
      ),
    );
    addLocal(pr, l);
  };

  const removeAt = (idx: number) => {
    const b = curLines()[idx];
    b?.sources.forEach((s) => unreserve.mutate({ prId: s.prId, lineId: s.lineId }));
    if (b?.pre) commit(curLines().map((x, i) => (i === idx ? { ...x, sources: [], removed: true } : x)));
    else commit(curLines().filter((_, i) => i !== idx));
    setProvOpen({});
    setMoveMenu(null);
  };
  const restoreAt = (idx: number) =>
    commit(curLines().map((x, i) => (i === idx ? { ...x, removed: false } : x)));

  const clearBasket = () => {
    curLines().forEach((b) => b.sources.forEach((s) => unreserve.mutate({ prId: s.prId, lineId: s.lineId })));
    commit(
      mode === "drawer"
        ? curLines().filter((b) => b.pre).map((b) => ({ ...b, sources: [], removed: false }))
        : [],
    );
    setMerge(null);
    setMergeAll(null);
    setUomNote(null);
    setProvOpen({});
    setMoveMenu(null);
  };
  const move = (idx: number, dir: -1 | 1) => {
    const j = idx + dir;
    if (j < 0 || j >= curLines().length) return;
    const n = [...curLines()];
    [n[idx], n[j]] = [n[j]!, n[idx]!];
    commit(n);
    setProvOpen({});
    setMoveMenu(null);
  };

  const toDtoLine = (b: BasketLine, i: number): RfqLineInput => {
    if (b.pre) {
      if (b.sources.length === 0) return { ...b.pre.dto, lineCode: `L${i + 1}` };
      return {
        ...b.pre.dto,
        lineCode: `L${i + 1}`,
        qty: (b.pre.dto.qty ?? 0) + newQty(b),
        sourcePrLineIds: [...(b.pre.dto.sourcePrLineIds ?? []), ...b.sources.map((s) => s.lineId)],
      };
    }
    return {
      lineCode: `L${i + 1}`,
      itemCode: b.itemCode,
      description: b.description,
      qty: lineQty(b),
      uom: b.uom,
      prRef: b.sources[0]?.prCode,
      sourcePrLineIds: b.sources.map((s) => s.lineId),
    };
  };
  const toBatch = (b: Basket) => ({
    title: b.name,
    lines: b.lines.filter((x) => !x.removed).map((bl, i) => toDtoLine(bl, i)),
    prRefs: [...new Set(b.lines.flatMap((x) => x.sources.map((s) => s.prId)))],
  });

  const build = useMutation({
    mutationFn: async () => {
      const b = basketsRef.current.find((x) => x.id === activeIdRef.current)!;
      const { title, lines, prRefs } = toBatch(b);
      proceeding.current = true;
      const id = await onBuild?.(title, lines, prRefs);
      commitBaskets(basketsRef.current.map((x) => (x.id === b.id ? { ...x, lines: [] } : x)));
      return id;
    },
    onSuccess: (id) => { if (id) onOpenRfq?.(id); },
    onError: (e: Error) => { proceeding.current = false; setErr(e.message); },
  });
  const nonEmpty = baskets.filter((b) => b.lines.some((x) => !x.removed));
  const buildAll = useMutation({
    mutationFn: async () => {
      const batches = basketsRef.current.filter((b) => b.lines.length > 0).map(toBatch);
      if (batches.length === 0) return;
      proceeding.current = true;
      await onBuildAll?.(batches);
      commitBaskets(basketsRef.current.map((b) => ({ ...b, lines: [] })));
    },
    onSuccess: () => onOpenRfqList?.(),
    onError: (e: Error) => { proceeding.current = false; setErr(e.message); },
  });

  // Drawer: Apply
  const dirty = basket.some((b) => b.sources.length > 0 || b.removed);
  const apply = () => {
    basket.filter((b) => b.pre && b.removed).forEach((b) =>
      (b.pre!.dto.sourcePrLineIds ?? []).forEach((sid) => {
        const owner = prs.find((p) => p.lines.some((l) => l.id === sid));
        if (owner?.id) void unreserveRequisitionLine(owner.id, sid).catch(() => {});
      }),
    );
    proceeding.current = true;
    const live = basket.filter((b) => !b.removed);
    onApply?.(live.map((b, i) => toDtoLine(b, i)));
  };

  const lineVerb = (pr: RequisitionDto, l: PrLineDto) => {
    if (isExt(pr, l)) return <SourcingStatusBadge status={l.lifecycleStatus} />;
    const used = inBasket(pr, l);
    const owner = used ? owningBasket(pr, l) : undefined;
    if (mode === "workspace" && owner != null && owner.id !== activeId) {
      return (
        <button
          type="button"
          className="cl-add mv"
          title={`In "${owner.name}" — move to "${activeBasket.name}"`}
          aria-label={`Move ${l.itemCode} from ${pr.code} here`}
          onClick={() => setMovePrompt({ pr, l, from: owner })}
        >
          ⇄
        </button>
      );
    }
    return (
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
    );
  };
  const rowCls = (pr: RequisitionDto, l: PrLineDto) => {
    if (isExt(pr, l)) return "ext";
    const used = inBasket(pr, l);
    const owner = used ? owningBasket(pr, l) : undefined;
    return used && !(mode === "workspace" && owner != null && owner.id !== activeId) ? "used" : "";
  };

  const live = basket.filter((b) => !b.removed);
  const nLines = live.length;
  const nPrs = new Set([
    ...live.flatMap((b) => b.sources.map((s) => s.prCode)),
    ...live.flatMap((b) => (b.pre?.dto.prRef ? [b.pre.dto.prRef] : [])),
  ]).size;
  const total = live.reduce((s, b) => s + rowAmount(b), 0);
  const basketEarliest = live
    .map(rowEarliest)
    .filter((d): d is string => !!d)
    .reduce<string | null>((a, c) => (a == null || c < a ? c : a), null);
  const tight = basketEarliest != null && daysUntil(basketEarliest) <= TIGHT_SOURCING_WINDOW_DAYS;
  const catTotals = useMemo(() => {
    const m = new Map<string, number>();
    live.forEach((b) =>
      b.sources.forEach((s) => {
        const f = lineFacts.get(s.lineId);
        if (!f?.category) return;
        m.set(f.category, (m.get(f.category) ?? 0) + s.qty * (f.rate ?? b.rate));
      }),
    );
    return [...m.entries()];
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [basket]);
  const delBasket = delPrompt != null ? baskets.find((b) => b.id === delPrompt) : undefined;

  return (
    <>
      {/* Workspace page chrome */}
      {mode === "workspace" ? (
        <>
          <div className="crumb">
            <button type="button" className="lnk" onClick={goBack}>
              Requisitions
            </button>{" "}
            <Icon name="chev" size={13} /> <span>RFQ Workspace</span>
          </div>
          <div className="pagehead">
            <div>
              <h1>RFQ Workspace</h1>
              <p>
                Build one or more RFQs from open PR lines across multiple requisitions — keep separate named baskets for
                separate RFQs, switch between them, and <b>Build all</b> when you&apos;re ready. Baskets (and their
                reservations) are saved in this browser, so leaving mid-build and coming back later is safe.
              </p>
            </div>
          </div>
        </>
      ) : null}

      {leaveToast ? (
        <div className="notify-toasts" aria-live="polite">
          <div className="toast">
            <span className="toast-ic">
              <Icon name="check" size={14} />
            </span>
            {leaveToast}
          </div>
        </div>
      ) : null}

      {restoreNotice ? (
        <Notice tone="warn" icon="eye" style={{ marginBottom: 12 }}>
          {restoreNotice}
        </Notice>
      ) : null}
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
            setFilters({ department: [], category: [], location: [], project: [] });
            setSearch("");
          }}
        >
          Reset
        </button>
      </div>

      <div className="cl-wrap">
        {/* LEFT — open PR lines */}
        <div className="cl-pane">
          <div className="cl-paneh">
            <h3>Open PR lines</h3>
            <div className="gb-tog" role="group" aria-label="Group lines by">
              <button type="button" className={groupBy === "pr" ? "on" : ""} aria-pressed={groupBy === "pr"} onClick={() => setGroupBy("pr")}>
                By PR
              </button>
              <button type="button" className={groupBy === "item" ? "on" : ""} aria-pressed={groupBy === "item"} onClick={() => setGroupBy("item")}>
                By item
              </button>
            </div>
            <span className="ct">
              {groupBy === "item" ? `${itemGroups.length} item${itemGroups.length === 1 ? "" : "s"}` : `${leftPrs.length} PR${leftPrs.length === 1 ? "" : "s"}`}
            </span>
            <div className="cl-paneh-actions">
              {groupBy === "pr" ? (
                <>
                  <button type="button" className="freset" disabled={leftPrs.length === 0} onClick={expandAll}>
                    Expand all
                  </button>
                  <button type="button" className="freset" disabled={leftPrs.length === 0} onClick={collapseAll}>
                    Collapse all
                  </button>
                </>
              ) : null}
              <button type="button" className="btn btn-out btn-sm" disabled={addableCount === 0} onClick={addAllShown}>
                <Icon name="plus" size={13} /> Add all{addableCount > 0 ? ` (${addableCount})` : ""}
              </button>
            </div>
          </div>
          <div className="cl-body">
            {loading ? <div className="cl-empty">Loading open PR lines…</div> : null}
            {!loading && leftPrs.length === 0 ? <div className="cl-empty">No PRs with open lines match these filters.</div> : null}
            {groupBy === "pr" &&
              leftPrs.map(({ pr, lines }) => {
                const open = expand[pr.id] !== false;
                const openCount = lines.filter((l) => l.lifecycleStatus === "Open" && !inBasket(pr, l)).length;
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
                      ? lines.map((l) => (
                          <div className={`prg-line ${rowCls(pr, l)}`} key={l.id}>
                            <span className="lc">
                              <div className="desc">{l.description}</div>
                              <div className="sub">
                                {l.itemCode} · {l.qty} {l.uom} · {fmt(l.qty * l.estUnitPrice)}
                              </div>
                            </span>
                            {lineVerb(pr, l)}
                          </div>
                        ))
                      : null}
                  </div>
                );
              })}
            {groupBy === "item" &&
              itemGroups.map((g) => {
                const eligible = g.rows.filter((r) => r.l.lifecycleStatus === "Open" && !inBasket(r.pr, r.l)).map((r) => ({ pr: r.pr, l: r.l }));
                return (
                  <div className="prg" key={g.itemCode}>
                    <div className="prg-h ig">
                      <span className="prg-meta">
                        <span className="prg-1">
                          <span className="prid">{g.itemCode}</span>
                          <span className="needby">
                            {g.rows.length} line{g.rows.length === 1 ? "" : "s"}
                          </span>
                        </span>
                        <span className="prg-2">{g.description}</span>
                      </span>
                      <button type="button" className="btn btn-ghost btn-sm" disabled={eligible.length === 0} aria-label={`Add all ${g.itemCode}`} onClick={() => startBulkAdd(eligible)}>
                        + Add all ({eligible.length})
                      </button>
                    </div>
                    {g.rows.map(({ pr, l }) => (
                      <div className={`prg-line ${rowCls(pr, l)}`} key={l.id}>
                        <span className="lc">
                          <div className="desc">
                            <span className="prid">{pr.code}</span>
                          </div>
                          <div className="sub">
                            {l.qty} {l.uom} · RM {fmt(l.estUnitPrice)} · need by {dateMY(pr.requiredOn)}
                          </div>
                        </span>
                        {lineVerb(pr, l)}
                      </div>
                    ))}
                  </div>
                );
              })}
          </div>
        </div>

        {/* RIGHT — RFQ basket */}
        <div className="cl-pane">
          <div className="cl-paneh">
            <h3>
              <Icon name="rfq" size={15} /> RFQ basket
            </h3>
            <span className="ct">
              {nLines} line{nLines === 1 ? "" : "s"}
            </span>
          </div>
          {mode === "workspace" ? (
            <div className="bk-tabs" role="tablist" aria-label="Baskets">
              {baskets.map((b) =>
                renaming === b.id ? (
                  <span
                    key={b.id}
                    className="bk-tab-edit"
                    onBlur={() => commitRename(b.id, renameDraft)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") commitRename(b.id, renameDraft);
                      if (e.key === "Escape") setRenaming(null);
                    }}
                  >
                    <input ref={focusRename} type="text" value={renameDraft} onChange={(e) => setRenameDraft(e.target.value)} aria-label="Rename basket" />
                  </span>
                ) : (
                  <span key={b.id} className={`bk-tab ${b.id === activeId ? "on" : ""}`} role="tab" aria-selected={b.id === activeId}>
                    <button
                      type="button"
                      className="bk-tab-btn"
                      title="Double-click to rename"
                      onClick={() => setActive(b.id)}
                      onDoubleClick={() => {
                        setRenameDraft(b.name);
                        setRenaming(b.id);
                      }}
                    >
                      {b.name} <span className="ct">{b.lines.filter((x) => !x.removed).length}</span>
                    </button>
                    {baskets.length > 1 ? (
                      <button type="button" className="bk-tab-x" aria-label={`Delete basket ${b.name}`} title="Delete basket" onClick={() => setDelPrompt(b.id)}>
                        ×
                      </button>
                    ) : null}
                  </span>
                ),
              )}
              <button type="button" className="bk-tab-new" onClick={newBasket}>
                + New basket
              </button>
            </div>
          ) : null}
          <div className="cl-body">
            {uomNote ? (
              <div className="cl-uomnote">
                <Icon name="eye" size={13} /> Same item code <b>{uomNote.code}</b> but different UoM (<b>{uomNote.a}</b> vs <b>{uomNote.b}</b>) — can&apos;t sum, kept as separate lines.
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
              const merged = b.sources.length > 1 || (!!b.pre && b.sources.length > 0);
              const pOpen = provOpen[idx];
              const rates = rowRates(b);
              const rMin = Math.min(...rates);
              const rMax = Math.max(...rates);
              const early = rowEarliest(b);
              return (
                <div className={`bk-line ${merged ? "merged" : ""} ${b.pre ? "pre" : ""} ${b.removed ? "removedpre" : ""}`} key={`${b.itemCode}-${idx}`}>
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
                      {b.pre ? <span className="bk-pre-chip"><Icon name="rfq" size={10} /> In this RFQ</span> : null}
                    </div>
                    <div className="sub">
                      {b.itemCode} · {b.uom} · RM {rMin === rMax ? fmt(rMin) : `${fmt(rMin)}–${fmt(rMax)}`}
                      {rMin !== rMax ? <span className="bk-vary">rates vary</span> : null}
                      {early ? <> · need by {dateMY(early)}</> : null}
                    </div>
                    {b.removed ? (
                      <div className="bk-unsrc">Will be removed — unsourced on apply</div>
                    ) : merged ? (
                      <>
                        <button type="button" className="prov-tog" onClick={() => setProvOpen((x) => ({ ...x, [idx]: !x[idx] }))}>
                          <span className="chev" style={{ transform: pOpen ? "rotate(90deg)" : "" }}>
                            <Icon name="chev" size={11} />
                          </span>{" "}
                          {b.pre ? `This RFQ + ${b.sources.length} PR line${b.sources.length === 1 ? "" : "s"}` : `Merged from ${b.sources.length} PRs`}
                        </button>
                        {pOpen ? (
                          <div className="provlist">
                            {b.pre ? (
                              <div className="provrow" key="pre">
                                <span>{b.pre.dto.prRef ?? "this RFQ"} (in RFQ)</span>
                                <span className="amt">
                                  {b.pre.dto.qty} {b.uom}
                                </span>
                              </div>
                            ) : null}
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
                      <div className="prov-from">from {b.pre ? (b.pre.dto.prRef ?? "—") : b.sources[0]!.prCode}</div>
                    )}
                  </span>
                  <span className="bk-num">
                    <span className="bk-qty">
                      {lineQty(b)} {b.uom}
                    </span>
                    {!b.removed ? <span className="bk-amt">RM {fmt(rowAmount(b))}</span> : null}
                  </span>
                  {mode === "workspace" && baskets.length > 1 && !b.removed ? (
                    <span className="bk-move">
                      <button type="button" className="bk-movebtn" title="Move to another basket" aria-label={`Move ${b.itemCode} to another basket`} onClick={() => setMoveMenu(moveMenu === idx ? null : idx)}>
                        ⇄
                      </button>
                      {moveMenu === idx ? (
                        <div className="bk-movepop" role="menu">
                          {baskets
                            .filter((t) => t.id !== activeId)
                            .map((t) => (
                              <button type="button" key={t.id} role="menuitem" onClick={() => moveRow(idx, t.id)}>
                                Move to {t.name}
                              </button>
                            ))}
                        </div>
                      ) : null}
                    </span>
                  ) : null}
                  {b.removed ? (
                    <button type="button" className="bk-rm" title="Keep in RFQ" aria-label={`Restore ${b.itemCode}`} onClick={() => restoreAt(idx)}>
                      ↺
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="bk-rm"
                      title={b.pre ? "Unsource — removed from this RFQ on apply" : "Send back — line returns to open"}
                      aria-label={`Remove ${b.itemCode}`}
                      onClick={() => removeAt(idx)}
                    >
                      −
                    </button>
                  )}
                </div>
              );
            })}
          </div>
          {basketEarliest != null || catTotals.length > 0 ? (
            <div className="cl-footx">
              {basketEarliest != null ? (
                <div className={`bk-early${tight ? " tight" : ""}`}>
                  <Icon name="clock" size={11} /> Earliest required-by: <b>{dateMY(basketEarliest)}</b>
                  {tight ? <span className="bk-tight">tight sourcing window — needed within {TIGHT_SOURCING_WINDOW_DAYS} days</span> : null}
                </div>
              ) : null}
              {catTotals.length > 0 ? (
                <div className="bk-cats">
                  {catTotals.map(([c, amt]) => (
                    <span className="bk-cat" key={c}>
                      {c} <b>RM {fmt(amt)}</b>
                    </span>
                  ))}
                </div>
              ) : null}
              {catTotals.length > 1 ? <div className="bk-mix">Mixed categories: {catTotals.map(([c]) => c).join(" + ")} — consider separate RFQs</div> : null}
            </div>
          ) : null}
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
            <button type="button" className="btn btn-ghost btn-sm" disabled={mode === "drawer" ? !dirty : !nLines} onClick={clearBasket}>
              Clear
            </button>
            {mode === "workspace" ? (
              <>
                {baskets.length > 1 ? (
                  <button type="button" className="btn btn-out" disabled={nonEmpty.length === 0 || buildAll.isPending || build.isPending} onClick={() => buildAll.mutate()}>
                    Build all ({nonEmpty.length})
                  </button>
                ) : null}
                <button type="button" className="btn btn-pri" disabled={!nLines || build.isPending || buildAll.isPending} onClick={() => build.mutate()}>
                  Build RFQ <Icon name="chev" size={14} />
                </button>
              </>
            ) : (
              <button type="button" className="btn btn-pri" disabled={!dirty} onClick={apply}>
                Apply to RFQ <Icon name="chev" size={14} />
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Modals */}
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
            Item code <b>{merge.incoming.itemCode}</b> is already in the basket at the same UoM (<b>{merge.incoming.uom}</b>). Merge them into one RFQ line — quantities summed, provenance kept — or add it as a separate line?
          </p>
        </Modal>
      ) : null}

      {movePrompt ? (
        <Modal
          title="Move line to this basket?"
          icon="box"
          footer={
            <>
              <button type="button" className="btn btn-ghost" onClick={() => setMovePrompt(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-pri" onClick={doMoveFromOther}>
                Move here
              </button>
            </>
          }
        >
          <p style={{ marginTop: 0 }}>
            <b>{movePrompt.l.itemCode}</b> from <b>{movePrompt.pr.code}</b> is already in basket &quot;<b>{movePrompt.from.name}</b>&quot;. A PR line can only be in one basket — move it to &quot;
            <b>{activeBasket.name}</b>&quot;? Its reservation is kept either way.
          </p>
        </Modal>
      ) : null}

      {delBasket ? (
        <Modal
          title="Delete basket?"
          icon="x"
          footer={
            <>
              <button type="button" className="btn btn-ghost" onClick={() => setDelPrompt(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-pri" onClick={doDeleteBasket}>
                Delete basket
              </button>
            </>
          }
        >
          <p style={{ marginTop: 0 }}>
            Deleting &quot;<b>{delBasket.name}</b>&quot; releases its <b>{delBasket.lines.filter((x) => !x.removed).length}</b> line(s) back to Open — they return to the left pane for future RFQs.
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
            Adding <b>{mergeAll.toAdd.length}</b> line{mergeAll.toAdd.length === 1 ? "" : "s"}. These item codes appear at the same UoM in more than one PR — merging sums their quantities into one RFQ line each (provenance kept). Confirm all, or keep every line separate.
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

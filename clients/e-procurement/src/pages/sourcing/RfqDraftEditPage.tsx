import { useCallback, useEffect, useMemo, useState } from "react";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { searchVendors, type VendorListItemDto } from "@/api/suppliers";
import { useSwec, type SwecNode } from "@/api/swec";
import { searchUsers, getUserById, listRoles, type UserDto } from "@/api/identity";
import {
  listFormTemplates,
  getFormTemplate,
  createFormTemplate,
  updateFormTemplate,
  listSegmentAssignments,
  type SegmentAssignmentDto,
} from "@/api/platform";
import { listIncoterms, listCurrencies, listCurrentExchangeRates } from "@/api/configuration";
import { RateRow } from "@/components/currency/RateRow";
import { CustomFieldsSection } from "@/components/customfields/CustomFieldsSection";
import { SegmentsSection } from "@/components/segments/SegmentsSection";
import { useDirtyState, useUnsavedChangesGuard } from "@/hooks/useUnsavedChangesGuard";
import {
  listRequisitions,
  getRequisition,
  inviteVendor,
  releaseRfq,
  updateRfqDraft,
  updateRfqRate,
  type RequisitionDto,
  type RfqDetailDto,
  type RfqLineInput,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Modal, Notice } from "@/components/ui";
import { QuestionEditor } from "@/components/sourcing/QuestionEditor";
import { ApiRequestError } from "@/lib/api-client";
import { fmt, dateMY, roleLabel, isSwecType } from "@/lib/format";
import {
  fromDto,
  fromLibraryQuestion,
  toDto,
  toLibraryQuestion,
  type EditItem,
  type FormSections,
  type GroupKey,
} from "@/lib/formTypes";
import { SourcingLinePicker } from "./SourcingLinePicker";

const TECH_EVAL_ROLE = "TechEvaluator";
const COMM_EVAL_ROLE = "CommEvaluator";

const userDisplayName = (u: UserDto | undefined, fallbackId: string) => {
  if (!u) return fallbackId;
  const name = `${u.firstName ?? ""} ${u.lastName ?? ""}`.trim();
  return name || u.userName || u.email || fallbackId;
};

type DraftLine = RfqDetailDto["lines"][number];

type Draft = {
  title: string;
  envelope: string;
  currency: string;
  opensUtc: string;
  closesUtc: string;
  clarificationDeadlineUtc: string;
  bidValidityDays: string;
  partialBidsAllowed: boolean;
  incotermId: string;
  incotermCode: string;
  incotermSuffix: string;
  lines: DraftLine[];
  items: EditItem[];
  technicalSections: string[];
  commercialSections: string[];
  techEvals: string[];
  commEvals: string[];
};

/** Match RfqConfiguration / Platform Incoterm suffix max length. */
const INCOTERM_SUFFIX_MAX = 200;

const isoToLocal = (iso: string | null | undefined) => (iso ? iso.slice(0, 16) : "");
const localToIso = (v: string) => (v ? new Date(v).toISOString() : null);

const TIGHT_SOURCING_WINDOW_DAYS = 14;
/** Advisory minimum bid-window length (days) on the Review checklist — never blocks release. */
const WINDOW_MIN_DAYS = 3;
const daysUntil = (iso: string) => {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((new Date(`${iso.slice(0, 10)}T00:00:00`).getTime() - today.getTime()) / 86400000);
};

const SOURCEABLE_HEADER_STATUSES = new Set(["Submitted", "PartiallySourced", "PartiallyOrdered"]);

type SrcFact = {
  id: string;
  prId: string;
  rate: number;
  qty: number;
  requiredDate: string | null;
};

const DIMENSION_LABEL: Record<string, string> = {
  Department: "Department",
  Location: "Location",
  CostCentre: "Cost centre",
  Category: "Category",
  Project: "Project",
};

function fromRfq(r: RfqDetailDto): Draft {
  return {
    title: r.title ?? "",
    envelope: r.envelope,
    currency: r.currency,
    opensUtc: isoToLocal(r.opensUtc),
    closesUtc: isoToLocal(r.closesUtc),
    clarificationDeadlineUtc: isoToLocal(r.clarificationDeadlineUtc),
    bidValidityDays: r.bidValidityDays != null ? String(r.bidValidityDays) : "",
    partialBidsAllowed: r.partialBidsAllowed ?? true,
    incotermId: r.incotermId ?? "",
    incotermCode: r.incotermCode ?? "",
    incotermSuffix: (r.incotermSuffix ?? "").slice(0, INCOTERM_SUFFIX_MAX),
    lines: [...r.lines],
    items: r.formItems.map((f) => fromDto(f)),
    technicalSections: [...r.technicalSections],
    commercialSections: [...r.commercialSections],
    techEvals: [...r.technicalEvaluatorIds],
    commEvals: [...r.commercialEvaluatorIds],
  };
}

/**
 * Phase 3 — multi-step RFQ draft wizard with money columns + Add PRs drawer.
 */
export function RfqDraftEditPage({
  rfq,
  onBack,
  onReleased,
}: {
  rfq: RfqDetailDto;
  onBack: () => void;
  onReleased: () => void;
}) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [draft, setDraft] = useState<Draft>(() => fromRfq(rfq));
  const [step, setStep] = useState(0);
  const [notice, setNotice] = useState("");
  const [showRelease, setShowRelease] = useState(false);
  const [showAddPrs, setShowAddPrs] = useState(false);
  const [vendorSearch, setVendorSearch] = useState("");
  const [vendorRegion, setVendorRegion] = useState("");
  const [vendorState, setVendorState] = useState("");
  const [vendorTypeFilter, setVendorTypeFilter] = useState<"All" | "Swec" | "NonSwec">("All");
  const [swecGroups, setSwecGroups] = useState<string[][]>([]);
  const [showSwecDialog, setShowSwecDialog] = useState(false);
  const [templateId, setTemplateId] = useState("");
  const [libForm, setLibForm] = useState<{ id: string; name: string } | null>(null);
  const [saveName, setSaveName] = useState<string | null>(null);

  const { isDirty, markClean: markDraftClean } = useDirtyState(draft);
  const { guardDialog, markClean: dismissGuard } = useUnsavedChangesGuard({ isDirty });
  const markClean = useCallback(() => {
    markDraftClean();
    dismissGuard();
  }, [markDraftClean, dismissGuard]);

  const patch = (p: Partial<Draft>) => setDraft((d) => ({ ...d, ...p }));
  const refresh = () => void qc.invalidateQueries({ queryKey: ["rfq", rfq.id] });
  const onErr = (e: Error) => setNotice(e instanceof ApiRequestError ? e.message : e.message);

  const { data: templates = [] } = useQuery({ queryKey: ["form-templates"], queryFn: () => listFormTemplates() });
  const { data: incoterms = [] } = useQuery({
    queryKey: ["incoterms", true],
    queryFn: () => listIncoterms(true),
  });
  const { data: currencies = [] } = useQuery({
    queryKey: ["currencies", true],
    queryFn: () => listCurrencies(true),
  });
  const {
    data: fxRates = [],
  } = useQuery({
    queryKey: ["exchange-rates", "current"],
    queryFn: listCurrentExchangeRates,
  });

  const baseCurrency = (rfq.baseCurrency ?? fxRates.find((r) => r.isBaseCurrency)?.currencyCode ?? "MYR").toUpperCase();

  // Snapshot on the saved RFQ; live Platform rate only as a preview when currency is dirty/unsaved.
  const currencyMatchesSaved = draft.currency.toUpperCase() === (rfq.currency ?? "").toUpperCase();
  const liveRate = useMemo(() => {
    const code = draft.currency?.toUpperCase();
    if (!code || code === baseCurrency) return null;
    return fxRates.find((r) => r.currencyCode.toUpperCase() === code)?.rateToBase ?? null;
  }, [draft.currency, baseCurrency, fxRates]);
  const exchangeRateToBase = currencyMatchesSaved ? (rfq.exchangeRateToBase ?? null) : liveRate;

  const updateRate = useMutation({
    mutationFn: () => updateRfqRate(rfq.id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["rfq", rfq.id] });
      setNotice("Exchange rate updated");
    },
    onError: onErr,
  });

  const currencyOptions = useMemo(() => {
    const active = currencies.map((c) => c.code.toUpperCase());
    const cur = draft.currency?.toUpperCase();
    if (cur && !active.includes(cur)) {
      return [...currencies, { id: cur, code: cur, name: `${cur} (inactive / unavailable)`, symbol: "", decimals: 2, isActive: false, createdOnUtc: "" }];
    }
    return currencies;
  }, [currencies, draft.currency]);

  // Legacy drafts may only have IncotermCode — resolve Id once the master list is loaded.
  useEffect(() => {
    if (!incoterms.length) return;
    setDraft((d) => {
      if (d.incotermId || !d.incotermCode) return d;
      const hit = incoterms.find((i) => i.code.toUpperCase() === d.incotermCode.toUpperCase());
      return hit ? { ...d, incotermId: hit.id, incotermCode: hit.code } : d;
    });
  }, [incoterms]);

  const { data: vendorPool } = useQuery({
    queryKey: ["vendor-pool"],
    queryFn: () => searchVendors({ pageSize: 200 }),
  });
  const { data: swec } = useSwec();
  const { data: roles = [] } = useQuery({ queryKey: ["roles"], queryFn: listRoles });
  const techRoleId = useMemo(
    () => roles.find((r) => r.name?.toLowerCase() === TECH_EVAL_ROLE.toLowerCase())?.id,
    [roles],
  );
  const commRoleId = useMemo(
    () => roles.find((r) => r.name?.toLowerCase() === COMM_EVAL_ROLE.toLowerCase())?.id,
    [roles],
  );
  const { data: techEvaluators = [] } = useQuery({
    queryKey: ["evaluator-pool", TECH_EVAL_ROLE, techRoleId],
    queryFn: async () => {
      if (!techRoleId) return [] as UserDto[];
      const page = await searchUsers({ roleId: techRoleId, isActive: true, pageSize: 100 });
      return page.items ?? [];
    },
    enabled: !!techRoleId && draft.envelope === "Dual",
  });
  const { data: commEvaluators = [] } = useQuery({
    queryKey: ["evaluator-pool", COMM_EVAL_ROLE, commRoleId],
    queryFn: async () => {
      if (!commRoleId) return [] as UserDto[];
      const page = await searchUsers({ roleId: commRoleId, isActive: true, pageSize: 100 });
      return page.items ?? [];
    },
    enabled: !!commRoleId && draft.envelope === "Dual",
  });

  // --- PR detail fetch for money columns ---
  const { data: prList = [] } = useQuery({ queryKey: ["requisitions"], queryFn: listRequisitions });
  const sourceablePrs = useMemo(() => prList.filter((p) => SOURCEABLE_HEADER_STATUSES.has(p.headerStatus)), [prList]);
  const detailResults = useQueries({
    queries: sourceablePrs.map((p) => ({ queryKey: ["requisition", p.id], queryFn: () => getRequisition(p.id) })),
  });
  const prs = useMemo(() => detailResults.map((r) => r.data).filter((d): d is RequisitionDto => !!d), [detailResults]);

  const lineFacts = useMemo(
    () =>
      new Map(
        prs.flatMap((p) =>
          p.lines.map((l) => [
            l.id,
            {
              id: l.id,
              prId: p.id,
              rate: l.estUnitPrice,
              qty: l.qty,
              requiredDate: p.requiredOn ?? null,
            } as SrcFact,
          ]),
        ),
      ),
    [prs],
  );

  const sourcesOf = (l: DraftLine): SrcFact[] => {
    const byId = (l.sourcePrLineIds ?? []).flatMap((sid) => {
      const f = lineFacts.get(sid);
      return f ? [f] : [];
    });
    if (byId.length > 0) return byId;
    const pr = prs.find((p) => p.code === l.prRef);
    const pl = pr?.lines.find((x) => x.itemCode === l.itemCode);
    return pr && pl
      ? [
          {
            id: pl.id,
            prId: pr.id,
            rate: pl.estUnitPrice,
            qty: l.qty,
            requiredDate: pr.requiredOn ?? null,
          },
        ]
      : [];
  };

  // Line segment columns (POC R2-S3T2, Platform OrgUnit tags): one fetch per distinct source PR.
  // Columns only appear for dimensions that some draft source line actually has valued.
  const segSrcPrIds = useMemo(
    () =>
      [
        ...new Set(
          draft.lines
            .flatMap((l) => (l.sourcePrLineIds ?? []).map((sid) => lineFacts.get(sid)?.prId ?? ""))
            .filter(Boolean),
        ),
      ].sort(),
    [draft.lines, lineFacts],
  );
  const segResults = useQueries({
    queries: segSrcPrIds.map((prId) => ({
      queryKey: ["line-segments", "Requisition", prId],
      queryFn: () => listSegmentAssignments("Requisition", prId),
      staleTime: 60_000,
    })),
  });
  const segByLine = useMemo(() => {
    const map = new Map<string, SegmentAssignmentDto[]>();
    for (const r of segResults) {
      for (const a of r.data ?? []) {
        if (!a.lineId) continue;
        const arr = map.get(a.lineId) ?? [];
        arr.push(a);
        map.set(a.lineId, arr);
      }
    }
    return map;
  }, [segResults]);
  const draftSrcIds = useMemo(
    () => new Set(draft.lines.flatMap((l) => l.sourcePrLineIds ?? [])),
    [draft.lines],
  );
  const segCols = useMemo(() => {
    const dims = new Set<string>();
    for (const sid of draftSrcIds) {
      for (const a of segByLine.get(sid) ?? []) {
        if (a.orgUnitId) dims.add(a.dimension);
      }
    }
    return [...dims].sort().map((dimension) => ({
      dimension,
      label: DIMENSION_LABEL[dimension] ?? dimension,
    }));
  }, [draftSrcIds, segByLine]);
  const segVal = (srcs: SrcFact[], dimension: string) => {
    const vals = [
      ...new Set(
        srcs
          .map((s) => {
            const hit = (segByLine.get(s.id) ?? []).find((a) => a.dimension === dimension);
            return hit?.orgUnitName || hit?.orgUnitCode || "";
          })
          .filter(Boolean),
      ),
    ];
    return vals.length > 1 ? { varies: true as const, text: null } : { varies: false as const, text: vals[0] ?? null };
  };

  const invitedVendorIds = useMemo(() => new Set(rfq.invitations.map((i) => i.vendorId)), [rfq.invitations]);

  const swecMatchesVendor = useCallback(
    (v: VendorListItemDto) => {
      const active = swecGroups.filter((g) => g.length > 0);
      if (active.length === 0) return true;
      const cats = v.categories ?? [];
      return active.some((group) =>
        group.every((code) => (swec?.desc(code) ?? [code]).some((x) => cats.includes(x))),
      );
    },
    [swecGroups, swec],
  );

  const allVendors = vendorPool?.items ?? [];
  const vendorRegions = useMemo(() => [...new Set(allVendors.map((v) => v.region).filter(Boolean))].sort(), [allVendors]);
  const vendorStates = useMemo(() => [...new Set(allVendors.map((v) => v.state).filter(Boolean))].sort(), [allVendors]);

  const filteredVendors = useMemo(() => {
    let list = allVendors;
    const kw = vendorSearch.trim().toLowerCase();
    if (kw) list = list.filter((v) => v.name.toLowerCase().includes(kw) || v.code.toLowerCase().includes(kw));
    if (vendorRegion) list = list.filter((v) => v.region === vendorRegion);
    if (vendorState) list = list.filter((v) => v.state === vendorState);
    if (vendorTypeFilter === "Swec") list = list.filter((v) => isSwecType(v.type) || (v.categories ?? []).length > 0);
    else if (vendorTypeFilter === "NonSwec") list = list.filter((v) => !isSwecType(v.type) && (v.categories ?? []).length === 0);
    list = list.filter(swecMatchesVendor);
    return list;
  }, [allVendors, vendorSearch, vendorRegion, vendorState, vendorTypeFilter, swecMatchesVendor]);

  const swecReadsAs = useCallback(
    (groups: string[][]) => {
      const gs = groups.filter((g) => g.length > 0);
      if (gs.length === 0) return "no category condition — all vendors";
      const label = (c: string) => swec?.label(c) ?? c;
      return gs.map((g) => (g.length > 1 ? `(${g.map(label).join(" AND ")})` : label(g[0]!))).join(" OR ");
    },
    [swec],
  );

  const swecTagCount = swecGroups.reduce((n, g) => n + g.length, 0);
  const swecGroupCount = swecGroups.filter((g) => g.length > 0).length;
  const matchedSwecDesc = useMemo(
    () => new Set(swecGroups.flat().flatMap((c) => swec?.desc(c) ?? [c])),
    [swecGroups, swec],
  );

  const evaluatorIds = useMemo(() => [...new Set([...draft.techEvals, ...draft.commEvals])], [draft.techEvals, draft.commEvals]);
  const evaluatorResults = useQueries({
    queries: evaluatorIds.map((id) => ({ queryKey: ["user", id], queryFn: () => getUserById(id) })),
  });
  const evaluatorName = (id: string) => {
    const i = evaluatorIds.indexOf(id);
    const u = i >= 0 ? evaluatorResults[i]?.data : undefined;
    const fromPool =
      techEvaluators.find((x) => x.id === id) ?? commEvaluators.find((x) => x.id === id);
    return userDisplayName(u ?? fromPool, id);
  };

  const toRequest = (cur: Draft) => ({
    title: cur.title,
    envelope: cur.envelope,
    currency: cur.currency,
    opensUtc: localToIso(cur.opensUtc),
    closesUtc: localToIso(cur.closesUtc),
    clarificationDeadlineUtc: localToIso(cur.clarificationDeadlineUtc),
    bidValidityDays: cur.bidValidityDays ? Number(cur.bidValidityDays) : null,
    partialBidsAllowed: cur.partialBidsAllowed,
    incotermId: cur.incotermId || null,
    incotermCode: cur.incotermCode || null,
    incotermSuffix: cur.incotermSuffix || null,
    lines: cur.lines,
    formItems: cur.items.map((it, i) => toDto(it, i)),
    technicalSections: cur.technicalSections,
    commercialSections: cur.commercialSections,
    technicalEvaluatorIds: cur.techEvals,
    commercialEvaluatorIds: cur.commEvals,
  });

  const save = useMutation({
    mutationFn: (cur: Draft) => updateRfqDraft(rfq.id, toRequest(cur)),
    onSuccess: () => {
      markClean();
      refresh();
      setNotice("Draft saved");
    },
    onError: onErr,
  });
  const invite = useMutation({
    mutationFn: (vendorId: string) => inviteVendor(rfq.id, vendorId),
    onSuccess: () => {
      refresh();
      setVendorSearch("");
    },
    onError: onErr,
  });
  const release = useMutation({
    mutationFn: async (cur: Draft) => {
      await updateRfqDraft(rfq.id, toRequest(cur));
      return releaseRfq(rfq.id);
    },
    onSuccess: () => {
      markClean();
      onReleased();
    },
    onError: (e: Error) => {
      setShowRelease(false);
      onErr(e);
    },
  });
  const loadTemplate = useMutation({
    mutationFn: (id: string) => getFormTemplate(id),
    onSuccess: (tpl) => {
      if (!tpl) return;
      const items = tpl.questions.map((q) => fromLibraryQuestion(q));
      const technicalSections = [
        ...new Set(items.filter((i) => i.group === "Technical" && i.section).map((i) => i.section)),
      ];
      const commercialSections = [
        ...new Set(items.filter((i) => i.group === "Commercial" && i.section).map((i) => i.section)),
      ];
      setDraft((d) => ({ ...d, items, technicalSections, commercialSections }));
      setLibForm({ id: tpl.id, name: tpl.name });
      setTemplateId("");
      setNotice(`Loaded form “${tpl.name}”`);
    },
    onError: onErr,
  });

  const formPayload = (cur: Draft, name: string) => ({
    name,
    questions: cur.items.map((it, i) => toLibraryQuestion(it, i)),
  });

  const saveAsForm = useMutation({
    mutationFn: (name: string) => {
      const keyBase = name
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-|-$/g, "")
        .slice(0, 40);
      return createFormTemplate({
        key: `${keyBase || "form"}-${Date.now().toString(36)}`,
        ...formPayload(draft, name.trim() || "New questionnaire"),
      }).then((id) => ({ id, name: name.trim() || "New questionnaire" }));
    },
    onSuccess: ({ id, name }) => {
      void qc.invalidateQueries({ queryKey: ["form-templates"] });
      setLibForm({ id, name });
      setSaveName(null);
      setNotice(`Saved as new form “${name}”`);
    },
    onError: (e: Error) => {
      setSaveName(null);
      onErr(e);
    },
  });

  const updateLibForm = useMutation({
    mutationFn: (cur: Draft) =>
      updateFormTemplate(libForm!.id, formPayload(cur, libForm!.name)),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["form-templates"] });
      setNotice(`Updated form “${libForm!.name}”`);
    },
    onError: onErr,
  });

  const steps =
    draft.envelope === "Dual"
      ? ["Items", "Settings", "Questions", "Vendors", "Evaluators", "Review"]
      : ["Items", "Settings", "Questions", "Vendors", "Review"];
  const last = steps.length - 1;
  const stepName = steps[step];
  const settingsIdx = steps.indexOf("Settings");
  const opensMsGate = draft.opensUtc ? new Date(draft.opensUtc).getTime() : NaN;
  const closesMsGate = draft.closesUtc ? new Date(draft.closesUtc).getTime() : NaN;
  const windowOrderInvalidGate =
    Number.isFinite(opensMsGate) && Number.isFinite(closesMsGate) && closesMsGate <= opensMsGate;
  const clarifyOutsideWindow =
    !!draft.clarificationDeadlineUtc &&
    !!draft.opensUtc &&
    !!draft.closesUtc &&
    (draft.clarificationDeadlineUtc < draft.opensUtc || draft.clarificationDeadlineUtc > draft.closesUtc);
  const settingsComplete = !!(draft.title && draft.opensUtc && draft.closesUtc);
  const settingsValid = settingsComplete && !windowOrderInvalidGate && !clarifyOutsideWindow;

  const validateStep = () => {
    if (stepName === "Settings") {
      if (!settingsComplete) {
        setNotice("RFQ title, bid open date/time and bid close date/time are required.");
        return false;
      }
      if (windowOrderInvalidGate) {
        setNotice("Bid close must be after bid open.");
        return false;
      }
      if (clarifyOutsideWindow) {
        setNotice("Clarification deadline must sit between bid open and close.");
        return false;
      }
    }
    setNotice("");
    return true;
  };
  const goToStep = (i: number) => {
    if (i > settingsIdx && !settingsValid) {
      setNotice(
        !settingsComplete
          ? "Complete the RFQ title and bid dates in Settings first."
          : windowOrderInvalidGate
            ? "Bid close must be after bid open."
            : "Clarification deadline must sit between bid open and close.",
      );
      setStep(settingsIdx);
      return;
    }
    setNotice("");
    setStep(i);
  };

  const removeLine = (lineCode: string) => setDraft((d) => ({ ...d, lines: d.lines.filter((l) => l.lineCode !== lineCode) }));

  const toggle = (arr: string[], v: string) => (arr.includes(v) ? arr.filter((x) => x !== v) : [...arr, v]);

  const formSections: FormSections = {
    Technical: draft.technicalSections,
    Commercial: draft.commercialSections,
  };

  const onQuestionsChange = (items: EditItem[], sections: FormSections) =>
    setDraft((d) => ({
      ...d,
      items,
      technicalSections: sections.Technical,
      commercialSections: sections.Commercial,
    }));

  const prRefs = [...new Set(draft.lines.map((l) => l.prRef).filter((v): v is string => !!v))];

  // --- Items step: money columns ---
  const itemRows = useMemo(() => {
    return draft.lines.map((l) => {
      const srcs = sourcesOf(l);
      const rates = srcs.map((s) => s.rate);
      const rMin = rates.length ? Math.min(...rates) : null;
      const rMax = rates.length ? Math.max(...rates) : null;
      const amount = srcs.length ? srcs.reduce((n, s) => n + s.qty * s.rate, 0) : null;
      const requiredBy = srcs
        .map((s) => s.requiredDate)
        .filter((x): x is string => !!x)
        .reduce<string | null>((a, c) => (a == null || c < a ? c : a), null);
      const prCodes = [
        ...new Set(
          (l.sourcePrLineIds ?? [])
            .map((sid) => prs.find((p) => p.lines.some((x) => x.id === sid))?.code)
            .filter((c): c is string => !!c),
        ),
      ];
      if (prCodes.length === 0 && l.prRef) prCodes.push(l.prRef);
      return { l, srcs, rMin, rMax, amount, requiredBy, prCodes };
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft.lines, lineFacts, prs]);

  const estTotal = itemRows.reduce((n, r) => n + (r.amount ?? 0), 0);
  const earliest = itemRows
    .map((r) => r.requiredBy)
    .filter((x): x is string => !!x)
    .reduce<string | null>((a, c) => (a == null || c < a ? c : a), null);
  const tight = earliest != null && daysUntil(earliest) <= TIGHT_SOURCING_WINDOW_DAYS;

  // --- Review readiness (POC R2-S3T6) ---
  const liveInvites = useMemo(
    () => rfq.invitations.filter((i) => i.status !== "Rescinded"),
    [rfq.invitations],
  );
  const opensMs = draft.opensUtc ? new Date(draft.opensUtc).getTime() : NaN;
  const closesMs = draft.closesUtc ? new Date(draft.closesUtc).getTime() : NaN;
  const windowOrderInvalid =
    Number.isFinite(opensMs) && Number.isFinite(closesMs) && closesMs <= opensMs;
  const winDays =
    Number.isFinite(opensMs) && Number.isFinite(closesMs)
      ? Math.round((closesMs - opensMs) / 86400000)
      : null;
  const earliestIsoDay = earliest?.slice(0, 10) ?? null;
  const closeIsoDay = draft.closesUtc ? draft.closesUtc.slice(0, 10) : null;
  const breachesRequiredBy = !!(closeIsoDay && earliestIsoDay && closeIsoDay > earliestIsoDay);
  const hasRequiredQ = (g: GroupKey) =>
    draft.items.some((it) => it.kind === "question" && it.group === g && it.required);

  type CkRow = { key: string; hard: boolean; ok: boolean; label: string; why: string };
  const checklist: CkRow[] = [
    {
      key: "window",
      hard: false,
      ok: !windowOrderInvalid && winDays != null && winDays >= WINDOW_MIN_DAYS,
      label:
        winDays != null && !windowOrderInvalid
          ? `Bid window (${winDays} day${winDays === 1 ? "" : "s"})`
          : "Bid window",
      why: windowOrderInvalid
        ? "bid-window dates are mis-ordered"
        : winDays == null
          ? "bid open and close dates are not set"
          : `bid window is only ${winDays} day${winDays === 1 ? "" : "s"} — vendors may not have time to respond`,
    },
    ...(earliestIsoDay != null
      ? [
          {
            key: "feasibility",
            hard: false,
            ok: !breachesRequiredBy,
            label: "Closes before lines are needed",
            why: `closes after the earliest required-by (${dateMY(earliestIsoDay)})`,
          } satisfies CkRow,
        ]
      : []),
    {
      key: "close",
      hard: true,
      ok: !!draft.closesUtc && !windowOrderInvalid,
      label: "Close date set",
      why: windowOrderInvalid ? "fix the bid-window dates" : "set a close date before releasing",
    },
    ...(draft.clarificationDeadlineUtc
      ? [
          {
            key: "clarify",
            hard: true,
            ok: !clarifyOutsideWindow,
            label: "Clarification deadline in bid window",
            why: "clarification deadline must sit between bid open and close",
          } satisfies CkRow,
        ]
      : []),
    ...(draft.envelope === "Dual"
      ? ([
          {
            key: "tech-eval",
            hard: true,
            ok: draft.techEvals.length > 0,
            label: "Technical evaluator assigned",
            why: "at least one technical evaluator is required — the release will be rejected",
          },
          {
            key: "comm-eval",
            hard: false,
            ok: draft.commEvals.length > 0,
            label: "Commercial evaluator assigned",
            why: "no commercial evaluator yet — needed before the commercial opening",
          },
          {
            key: "req-q-tech",
            hard: false,
            ok: hasRequiredQ("Technical"),
            label: "Required technical question",
            why: "technical envelope has no required questions",
          },
          {
            key: "req-q-comm",
            hard: false,
            ok: hasRequiredQ("Commercial"),
            label: "Required commercial question",
            why: "commercial envelope has no required questions",
          },
        ] satisfies CkRow[])
      : ([
          {
            key: "req-q",
            hard: false,
            ok: hasRequiredQ("Technical") || hasRequiredQ("Commercial"),
            label: "Required question",
            why: "the questionnaire has no required questions",
          },
        ] satisfies CkRow[])),
    {
      key: "vendors",
      hard: true,
      ok: liveInvites.length > 0,
      label: `Vendor invited (${liveInvites.length})`,
      why: "invite at least one vendor",
    },
    {
      key: "lines",
      hard: true,
      ok: draft.lines.length > 0,
      label: `Line items (${draft.lines.length})`,
      why: "add at least one line item",
    },
  ];
  const hardFails = checklist.filter((r) => r.hard && !r.ok);
  const ckWarns = checklist.filter((r) => !r.hard && !r.ok);

  const qBuckets = (g: GroupKey) => {
    const qs = draft.items.filter((it) => it.kind === "question" && it.group === g);
    const secs = g === "Technical" ? draft.technicalSections : draft.commercialSections;
    const buckets: { name: string | null; list: EditItem[] }[] = [];
    const unsectioned = qs.filter((q) => !q.section);
    if (unsectioned.length) buckets.push({ name: null, list: unsectioned });
    for (const s of secs) {
      const list = qs.filter((q) => q.section === s);
      if (list.length) buckets.push({ name: s, list });
    }
    for (const q of qs) {
      if (q.section && !secs.includes(q.section) && !buckets.some((b) => b.name === q.section)) {
        buckets.push({ name: q.section, list: qs.filter((x) => x.section === q.section) });
      }
    }
    return buckets;
  };

  const editLink = (stepLabel: string) => (
    <button
      type="button"
      className="rv-edit"
      aria-label={`Edit ${stepLabel}`}
      onClick={() => goToStep(steps.indexOf(stepLabel))}
    >
      <Icon name="edit" size={12} /> Edit
    </button>
  );

  const fmtLocal = (v: string) => {
    if (!v) return "—";
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? v : d.toLocaleString();
  };

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          RFQs
        </button>{" "}
        <Icon name="chev" size={13} /> <span>{rfq.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Create RFQ</h1>
          <p>
            {rfq.code} · from {prRefs.join(", ") || "—"}
          </p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          {step > 0 ? (
            <button type="button" className="btn btn-out" onClick={() => setStep((s) => Math.max(s - 1, 0))}>
              <Icon name="back" size={15} /> Back
            </button>
          ) : null}
          <button type="button" className="btn btn-out" disabled={save.isPending} onClick={() => save.mutate(draft)}>
            <Icon name="doc" size={15} /> Save as draft
          </button>
          {step < last ? (
            <button
              type="button"
              className="btn btn-pri"
              onClick={() => {
                if (validateStep()) setStep((s) => Math.min(s + 1, last));
              }}
            >
              Continue <Icon name="chev" size={15} />
            </button>
          ) : (
            <button
              type="button"
              className="btn btn-pri"
              disabled={hardFails.length > 0}
              title={hardFails.length > 0 ? `Blocked: ${hardFails.map((r) => r.why).join(" · ")}` : undefined}
              onClick={() => setShowRelease(true)}
            >
              <Icon name="send" size={15} /> Release to {liveInvites.length} vendor{liveInvites.length !== 1 ? "s" : ""}
            </button>
          )}
        </div>
      </div>

      <div className="steps">
        {steps.map((s, i) => (
          <span key={s} style={{ display: "contents" }}>
            <button type="button" className={`step${i === step ? " on" : i < step ? " done" : ""}`} onClick={() => goToStep(i)}>
              <span className="n">{i < step ? "✓" : i + 1}</span>
              {s}
            </button>
            {i < steps.length - 1 ? <span className="sep" /> : null}
          </span>
        ))}
      </div>

      {notice ? <Notice>{notice}</Notice> : null}

      {stepName === "Items" ? (
        <>
          <div className="card">
            <div className="chead">
              <h3>RFQ Line Items</h3>
              <div className="spacer" />
              <span className="hint" style={{ marginRight: 12 }}>
                {draft.lines.length} {draft.lines.length === 1 ? "line" : "lines"} from {prRefs.length} PR{prRefs.length !== 1 ? "s" : ""}
              </span>
              {estTotal > 0 ? (
                <span className="rfq-esttotal">
                  Est. total {draft.currency} {fmt(estTotal)}
                </span>
              ) : null}
              <button type="button" className="btn btn-out btn-sm" onClick={() => setShowAddPrs(true)}>
                <Icon name="plus" size={15} /> Add PRs
              </button>
            </div>
            <RateRow
              currency={draft.currency}
              baseCurrency={baseCurrency}
              exchangeRateToBase={exchangeRateToBase}
              total={estTotal}
              editable={currencyMatchesSaved}
              busy={updateRate.isPending}
              onUpdate={() => updateRate.mutate()}
            />
            <table className="rfqlt">
              <thead>
                <tr>
                  <th>PR #</th>
                  <th>Code</th>
                  <th>Item</th>
                  <th>Description</th>
                  <th className="amt">Qty</th>
                  <th>UoM</th>
                  <th className="amt">Est. rate</th>
                  <th className="amt">Est. amount</th>
                  <th>Required by</th>
                  {segCols.map((c) => (
                    <th key={c.dimension}>{c.label}</th>
                  ))}
                  <th />
                </tr>
              </thead>
              <tbody>
                {itemRows.map((r) => {
                  const vary = r.rMin != null && r.rMax != null && r.rMin !== r.rMax;
                  return (
                    <tr key={r.l.lineCode}>
                      <td>
                        <span className="prtag">{r.l.prRef ?? "—"}</span>
                      </td>
                      <td>{r.l.lineCode}</td>
                      <td>{r.l.itemCode}</td>
                      <td>{r.l.description}</td>
                      <td className="amt">{r.l.qty}</td>
                      <td>{r.l.uom}</td>
                      <td className="amt">
                        {r.rMin == null || r.rMax == null ? (
                          "—"
                        ) : (
                          <>
                            {vary ? `${fmt(r.rMin)}–${fmt(r.rMax)}` : fmt(r.rMin)}
                            {vary ? <span className="bk-vary">rates vary</span> : null}
                          </>
                        )}
                      </td>
                      <td className="amt">{r.amount == null ? "—" : fmt(r.amount)}</td>
                      <td>{r.requiredBy ? dateMY(r.requiredBy) : "—"}</td>
                      {segCols.map((c) => {
                        const v = segVal(r.srcs, c.dimension);
                        return (
                          <td key={c.dimension}>
                            {v.varies ? <span title="varies across sources">—</span> : (v.text ?? "—")}
                          </td>
                        );
                      })}
                      <td className="amt">
                        <button type="button" className="btn btn-ghost btn-sm" title="Remove line" onClick={() => removeLine(r.l.lineCode)}>
                          <Icon name="x" size={15} />
                        </button>
                      </td>
                    </tr>
                  );
                })}
                {draft.lines.length === 0 ? (
                  <tr>
                    <td colSpan={10 + segCols.length} style={{ padding: 22, textAlign: "center", color: "var(--muted)" }}>
                      No lines on this RFQ. Click <b>Add PRs</b> to source lines from requisitions.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
            {earliest != null && !tight ? (
              <div className="cl-footx">
                <div className="bk-early">
                  <Icon name="clock" size={11} /> Earliest required-by: <b>{dateMY(earliest)}</b>
                </div>
              </div>
            ) : null}
          </div>
          {tight ? (
            <Notice tone="warn" icon="clock" style={{ marginTop: 14 }}>
              Earliest required-by: <b>{dateMY(earliest!)}</b> — tight sourcing window, needed within {TIGHT_SOURCING_WINDOW_DAYS} days.
            </Notice>
          ) : null}
        </>
      ) : null}

      {stepName === "Settings" ? (
        <>
        <div className="card">
          <div className="cbody">
            <div className="field">
              <label>RFQ title *</label>
              <input
                type="text"
                required
                value={draft.title}
                placeholder="e.g. Pump &amp; VFD Package — Facilities Upgrade"
                onChange={(e) => patch({ title: e.target.value })}
                style={!draft.title ? { borderColor: "var(--amber)" } : undefined}
              />
            </div>
            <div className="grid g2">
              <div className="field">
                <label>Bid opens *</label>
                <input
                  type="datetime-local"
                  required
                  value={draft.opensUtc}
                  onChange={(e) => patch({ opensUtc: e.target.value })}
                  style={!draft.opensUtc || windowOrderInvalidGate ? { borderColor: !draft.opensUtc ? "var(--amber)" : "var(--red)" } : undefined}
                />
              </div>
              <div className="field">
                <label>Bid closes *</label>
                <input
                  type="datetime-local"
                  required
                  value={draft.closesUtc}
                  onChange={(e) => patch({ closesUtc: e.target.value })}
                  style={!draft.closesUtc || windowOrderInvalidGate ? { borderColor: !draft.closesUtc ? "var(--amber)" : "var(--red)" } : undefined}
                />
                {windowOrderInvalidGate ? (
                  <p className="ferr" style={{ margin: "4px 0 0" }}>Bid close must be after bid open.</p>
                ) : null}
              </div>
            </div>
            <div className="grid g2">
              <div className="field" style={{ maxWidth: 320 }}>
                <label>Currency</label>
                <select value={draft.currency} onChange={(e) => patch({ currency: e.target.value })}>
                  {currencyOptions.map((c) => (
                    <option key={c.id || c.code} value={c.code}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
                <p className="hint" style={{ margin: "4px 0 0" }}>
                  From Configuration → Currencies (active). Base ledger currency is {baseCurrency}.
                </p>
                <RateRow
                  currency={draft.currency}
                  baseCurrency={baseCurrency}
                  exchangeRateToBase={exchangeRateToBase}
                  total={estTotal}
                  editable={currencyMatchesSaved}
                  busy={updateRate.isPending}
                  onUpdate={() => updateRate.mutate()}
                />
              </div>
            </div>
            <div className="field">
              <label>Envelope type</label>
              <div className="grid g2" style={{ gap: 12 }}>
                {(
                  [
                    ["Single", "Single envelope", "Commercial & technical opened together when bids close."],
                    ["Dual", "Dual envelope (sealed)", "Technical opened & scored first; commercial stays sealed until technical pass."],
                  ] as const
                ).map(([val, title, desc]) => (
                  <div
                    key={val}
                    className="addr"
                    style={{
                      cursor: "pointer",
                      borderColor: draft.envelope === val ? "var(--teal)" : "var(--line)",
                    }}
                    onClick={() => patch({ envelope: val })}
                  >
                    <label className="ck">
                      <input
                        type="radio"
                        name="rfq-envelope"
                        checked={draft.envelope === val}
                        onChange={() => patch({ envelope: val })}
                      />{" "}
                      <strong>{title}</strong>
                    </label>
                    <p className="hint" style={{ margin: "8px 0 0" }}>
                      {desc}
                    </p>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid g2">
              <div className="field">
                <label>Clarification deadline</label>
                <input
                  type="datetime-local"
                  value={draft.clarificationDeadlineUtc}
                  onChange={(e) => patch({ clarificationDeadlineUtc: e.target.value })}
                  style={clarifyOutsideWindow ? { borderColor: "var(--red)" } : undefined}
                />
                {clarifyOutsideWindow ? (
                  <p className="ferr" style={{ margin: "4px 0 0" }}>Must be between bid open and close dates.</p>
                ) : null}
              </div>
              <div className="field" style={{ maxWidth: 200 }}>
                <label>Bid validity (days)</label>
                <input
                  type="number"
                  min={1}
                  placeholder="e.g. 90"
                  value={draft.bidValidityDays}
                  onChange={(e) => patch({ bidValidityDays: e.target.value })}
                />
              </div>
            </div>
            <div className="grid g2">
              <div className="field">
                <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <input
                    type="checkbox"
                    checked={draft.partialBidsAllowed}
                    onChange={(e) => patch({ partialBidsAllowed: e.target.checked })}
                    style={{ width: "auto" }}
                  />
                  Allow partial bids
                </label>
                <p className="hint" style={{ margin: "4px 0 0" }}>
                  When enabled, vendors may bid on a subset of line items.
                </p>
              </div>
            </div>
            {/* POC R-QF16-T3: dedicated Shipping band — Incoterm + named place (RFQ has no ship-to). */}
            <div className="txn-band" aria-label="Shipping">
              <div className="txn-bandbar">Shipping</div>
              <div className="txn-bandbody">
                <p className="hint" style={{ margin: "0 0 10px" }}>
                  Shipping / delivery terms for this RFQ (carried forward to the PO at award). There is no ship-to address on an RFQ.
                </p>
                <div className="grid g2" style={{ maxWidth: 560, gap: 12 }}>
                  <div className="field" style={{ margin: 0 }}>
                    <label>Incoterm</label>
                    <select
                      value={draft.incotermId}
                      onChange={(e) => {
                        const id = e.target.value;
                        const hit = incoterms.find((i) => i.id === id);
                        patch({
                          incotermId: id,
                          incotermCode: hit?.code ?? "",
                          ...(id ? {} : { incotermSuffix: "" }),
                        });
                      }}
                    >
                      <option value="">— None —</option>
                      {incoterms.map((i) => (
                        <option key={i.id} value={i.id}>
                          {i.code} — {i.name}
                        </option>
                      ))}
                      {draft.incotermId && !incoterms.some((i) => i.id === draft.incotermId) ? (
                        <option value={draft.incotermId}>
                          {draft.incotermCode || draft.incotermId} (inactive / unavailable)
                        </option>
                      ) : null}
                    </select>
                    <p className="hint" style={{ margin: "4px 0 0" }}>
                      From Configuration → Incoterms (active terms).
                    </p>
                  </div>
                  <div className="field" style={{ margin: 0 }}>
                    <label>Incoterm suffix / named place</label>
                    <input
                      type="text"
                      placeholder="e.g. Port Klang"
                      value={draft.incotermSuffix}
                      maxLength={INCOTERM_SUFFIX_MAX}
                      disabled={!draft.incotermId}
                      onChange={(e) => patch({ incotermSuffix: e.target.value.slice(0, INCOTERM_SUFFIX_MAX) })}
                    />
                    <p className="hint" style={{ margin: "4px 0 0" }}>
                      Named place / port — max {INCOTERM_SUFFIX_MAX} characters
                      {draft.incotermSuffix
                        ? ` (${draft.incotermSuffix.length}/${INCOTERM_SUFFIX_MAX})`
                        : ""}.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <CustomFieldsSection recordType="Rfq" recordId={rfq.id} />
        <SegmentsSection recordType="Rfq" recordId={rfq.id} />
        </>
      ) : null}

      {stepName === "Questions" ? (
        <>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="cbody" style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
              <label className="hint" style={{ fontWeight: 700 }}>
                Form library
              </label>
              <select value={templateId} onChange={(e) => setTemplateId(e.target.value)} style={{ maxWidth: 320 }}>
                <option value="">Choose a form template…</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name} · {t.questionCount} question(s)
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={!templateId || loadTemplate.isPending}
                onClick={() => loadTemplate.mutate(templateId)}
              >
                <Icon name="doc" size={13} /> Load form
              </button>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={saveAsForm.isPending}
                onClick={() => setSaveName(draft.title ? `${draft.title} questionnaire` : "New questionnaire")}
              >
                <Icon name="plus" size={13} /> Save as new form
              </button>
              {libForm ? (
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={updateLibForm.isPending}
                  onClick={() => updateLibForm.mutate(draft)}
                >
                  <Icon name="check" size={13} /> Update “{libForm.name}”
                </button>
              ) : null}
              <span className="hint">Load replaces the questionnaire below. Save / Update stores it in the form library.</span>
            </div>
          </div>
          <QuestionEditor items={draft.items} sections={formSections} onChange={onQuestionsChange} />
        </>
      ) : null}

      {stepName === "Vendors" ? (
        <>
          <div className="card" style={{ marginBottom: 12 }}>
            <div className="cbody">
              <div className="grid" style={{ gridTemplateColumns: "1.6fr 1fr 1fr 1fr", gap: 10, alignItems: "end", marginBottom: 11 }}>
                <div className="field" style={{ margin: 0 }}>
                  <label>Search vendor</label>
                  <input value={vendorSearch} onChange={(e) => setVendorSearch(e.target.value)} placeholder="Vendor name" />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label>Region</label>
                  <select value={vendorRegion} onChange={(e) => setVendorRegion(e.target.value)}>
                    <option value="">All regions</option>
                    {vendorRegions.map((r) => (
                      <option key={r} value={r}>{r}</option>
                    ))}
                  </select>
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label>State</label>
                  <select value={vendorState} onChange={(e) => setVendorState(e.target.value)}>
                    <option value="">All states</option>
                    {vendorStates.map((s) => (
                      <option key={s} value={s}>{s}</option>
                    ))}
                  </select>
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label>Type</label>
                  <select value={vendorTypeFilter} onChange={(e) => setVendorTypeFilter(e.target.value as "All" | "Swec" | "NonSwec")}>
                    <option value="All">All</option>
                    <option value="Swec">SWEC</option>
                    <option value="NonSwec">Non-SWEC</option>
                  </select>
                </div>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                <button type="button" className="btn btn-out btn-sm" onClick={() => setShowSwecDialog(true)}>
                  <Icon name="split" size={14} /> SWEC categories{swecTagCount ? ` · ${swecTagCount} tag(s)` : ""}
                </button>
                <span className="hint swcond-summary" style={{ fontSize: 12 }}>
                  reads as: <b>{swecReadsAs(swecGroups)}</b>
                </span>
                <div style={{ flex: 1 }} />
                {swecGroupCount > 0 ? (
                  <button type="button" className="freset" onClick={() => setSwecGroups([])}>
                    Clear
                  </button>
                ) : null}
              </div>
            </div>
          </div>

          <div className="card">
            <div className="chead">
              <h3>Vendors</h3>
              <div className="spacer" />
              <span className="hint" style={{ marginRight: 8 }}>
                {filteredVendors.length} shown · {rfq.invitations.length} selected
                {swecGroupCount ? ` · ${swecGroupCount} category group(s)` : ""}
              </span>
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                disabled={invite.isPending}
                onClick={() => {
                  const toInvite = filteredVendors.filter((v) => !invitedVendorIds.has(v.id));
                  if (toInvite.length === 0) return;
                  void (async () => {
                    for (const v of toInvite) {
                      try {
                        await inviteVendor(rfq.id, v.id);
                      } catch {
                        /* skip already invited */
                      }
                    }
                    refresh();
                  })();
                }}
              >
                Select all shown
              </button>
            </div>
            <div className="cbody" style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {filteredVendors.map((v) => {
                const on = invitedVendorIds.has(v.id);
                const swecVendor = isSwecType(v.type) || (v.categories ?? []).length > 0;
                return (
                  <div
                    className={`vpick ${on ? "on" : ""}`}
                    key={v.id}
                    onClick={() => {
                      if (invite.isPending) return;
                      if (on) {
                        void import("@/api/sourcing").then((m) =>
                          m.rescindInvitation(rfq.id, v.id, "DESELECTED").then(refresh).catch(onErr),
                        );
                      } else {
                        invite.mutate(v.id);
                      }
                    }}
                  >
                    <span className="vck">{on ? <Icon name="check" size={12} /> : null}</span>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontWeight: 700 }}>
                        {v.name}{" "}
                        {swecVendor ? (
                          <span className="badge b-teal" style={{ marginLeft: 4 }}>
                            SWEC
                          </span>
                        ) : null}
                      </div>
                      <div className="hint">
                        {v.code} · {v.state || "—"} · {v.region || "—"}
                        {v.rating > 0 ? ` · ★ ${v.rating.toFixed(1)}` : ""}
                      </div>
                      {(v.categories ?? []).length > 0 ? (
                        <div style={{ marginTop: 5 }}>
                          {v.categories.map((c) => (
                            <span key={c} className={`swchip${matchedSwecDesc.has(c) ? " hit" : ""}`}>
                              {swec?.label(c) ?? c}
                            </span>
                          ))}
                        </div>
                      ) : null}
                    </div>
                  </div>
                );
              })}
              {filteredVendors.length === 0 ? (
                <div className="empty" style={{ padding: 26 }}>
                  No vendors match these filters.
                </div>
              ) : null}
            </div>
          </div>

          {rfq.invitations.length > 0 ? (
            <div className="card" style={{ marginTop: 12 }}>
              <div className="cbody">
                <div className="hint" style={{ marginBottom: 6, fontWeight: 700 }}>
                  Invited ({rfq.invitations.length})
                </div>
                <div>
                  {rfq.invitations.map((i) => (
                    <span key={i.id} className="chip">
                      {i.vendorName || i.vendorCode || i.vendorId}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          ) : null}

          {showSwecDialog ? (
            <SwecConditionDialog
              initial={swecGroups}
              onCancel={() => setShowSwecDialog(false)}
              onApply={(groups) => {
                setSwecGroups(groups);
                setShowSwecDialog(false);
              }}
            />
          ) : null}
        </>
      ) : null}

      {stepName === "Evaluators" ? (
        <>
          {/* Early warn — Continue stays unlocked; release enforces server-side. */}
          {draft.techEvals.length === 0 ? (
            <Notice tone="warn" icon="users">
              At least one technical evaluator is required before release — only an assigned evaluator can open the
              sealed technical envelope.
            </Notice>
          ) : null}
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="chead">
              <h3>Technical envelope — evaluators</h3>
              <span className="sub">· {draft.techEvals.length} selected</span>
            </div>
            <div className="cbody">
              <p className="hint" style={{ marginTop: 0 }}>
                Only assigned evaluators can open and score the sealed technical envelope. Pool is users with the{" "}
                {roleLabel(TECH_EVAL_ROLE)} role.
              </p>
              {!techRoleId ? (
                <p className="hint" style={{ margin: 0 }}>
                  Role “{TECH_EVAL_ROLE}” is not defined yet.{" "}
                  <button
                    type="button"
                    className="lnk"
                    style={{ padding: 0, textDecoration: "underline" }}
                    onClick={() => void navigate("/admin/roles")}
                  >
                    Create it in Roles
                  </button>
                </p>
              ) : techEvaluators.length === 0 ? (
                <p className="hint" style={{ margin: 0 }}>
                  No users hold the {roleLabel(TECH_EVAL_ROLE)} role.{" "}
                  <button
                    type="button"
                    className="lnk"
                    style={{ padding: 0, textDecoration: "underline" }}
                    onClick={() => void navigate("/admin")}
                  >
                    Assign roles in User Management
                  </button>
                </p>
              ) : (
                techEvaluators.map((u) => {
                  const id = u.id ?? "";
                  if (!id) return null;
                  return (
                    <label className="ck" key={id} style={{ padding: "6px 0" }}>
                      <input
                        type="checkbox"
                        checked={draft.techEvals.includes(id)}
                        onChange={() => patch({ techEvals: toggle(draft.techEvals, id) })}
                      />
                      <span style={{ fontWeight: 600 }}>{userDisplayName(u, id)}</span>
                      <span className="hint">· {roleLabel(TECH_EVAL_ROLE)}</span>
                    </label>
                  );
                })
              )}
              {/* Orphans: previously assigned users no longer in the role pool */}
              {draft.techEvals
                .filter((id) => !techEvaluators.some((u) => u.id === id))
                .map((id) => (
                  <label className="ck" key={`orphan-tech-${id}`} style={{ padding: "6px 0" }}>
                    <input
                      type="checkbox"
                      checked
                      onChange={() => patch({ techEvals: toggle(draft.techEvals, id) })}
                    />
                    <span style={{ fontWeight: 600 }}>{evaluatorName(id)}</span>
                    <span className="hint">· not in current role pool</span>
                  </label>
                ))}
            </div>
          </div>
          <div className="card">
            <div className="chead">
              <h3>Commercial envelope — evaluators</h3>
              <span className="sub">· {draft.commEvals.length} selected</span>
            </div>
            <div className="cbody">
              <p className="hint" style={{ marginTop: 0 }}>
                Commercial evaluators are not required for release — they gate the commercial opening after technical
                finalisation. Pool is users with the {roleLabel(COMM_EVAL_ROLE)} role.
              </p>
              {!commRoleId ? (
                <p className="hint" style={{ margin: 0 }}>
                  Role “{COMM_EVAL_ROLE}” is not defined yet.{" "}
                  <button
                    type="button"
                    className="lnk"
                    style={{ padding: 0, textDecoration: "underline" }}
                    onClick={() => void navigate("/admin/roles")}
                  >
                    Create it in Roles
                  </button>
                </p>
              ) : commEvaluators.length === 0 ? (
                <p className="hint" style={{ margin: 0 }}>
                  No users hold the {roleLabel(COMM_EVAL_ROLE)} role.{" "}
                  <button
                    type="button"
                    className="lnk"
                    style={{ padding: 0, textDecoration: "underline" }}
                    onClick={() => void navigate("/admin")}
                  >
                    Assign roles in User Management
                  </button>
                </p>
              ) : (
                commEvaluators.map((u) => {
                  const id = u.id ?? "";
                  if (!id) return null;
                  return (
                    <label className="ck" key={id} style={{ padding: "6px 0" }}>
                      <input
                        type="checkbox"
                        checked={draft.commEvals.includes(id)}
                        onChange={() => patch({ commEvals: toggle(draft.commEvals, id) })}
                      />
                      <span style={{ fontWeight: 600 }}>{userDisplayName(u, id)}</span>
                      <span className="hint">· {roleLabel(COMM_EVAL_ROLE)}</span>
                    </label>
                  );
                })
              )}
              {draft.commEvals
                .filter((id) => !commEvaluators.some((u) => u.id === id))
                .map((id) => (
                  <label className="ck" key={`orphan-comm-${id}`} style={{ padding: "6px 0" }}>
                    <input
                      type="checkbox"
                      checked
                      onChange={() => patch({ commEvals: toggle(draft.commEvals, id) })}
                    />
                    <span style={{ fontWeight: 600 }}>{evaluatorName(id)}</span>
                    <span className="hint">· not in current role pool</span>
                  </label>
                ))}
            </div>
          </div>
        </>
      ) : null}

      {stepName === "Review" ? (
        <>
          <div className="card rv-card" data-rv="items">
            <div className="chead">
              <h3>Line items</h3>
              <div className="spacer" />
              {estTotal > 0 ? (
                <span className="rfq-esttotal">
                  Est. total {draft.currency} {fmt(estTotal)}
                </span>
              ) : null}
              {editLink("Items")}
            </div>
            <RateRow
              currency={draft.currency}
              baseCurrency={baseCurrency}
              exchangeRateToBase={exchangeRateToBase}
              total={estTotal}
              editable={false}
              busy={false}
              onUpdate={() => undefined}
            />
            <table className="rfqlt rv-items">
              <thead>
                <tr>
                  <th>#</th>
                  <th>PR</th>
                  <th>Item</th>
                  <th className="amt">Qty</th>
                  <th>UoM</th>
                  <th className="amt">Est. rate</th>
                  <th className="amt">Est. amount</th>
                </tr>
              </thead>
              <tbody>
                {itemRows.map((r, i) => {
                  const vary = r.rMin != null && r.rMax != null && r.rMin !== r.rMax;
                  return (
                    <tr key={r.l.lineCode}>
                      <td>{i + 1}</td>
                      <td>
                        {(r.prCodes.length ? r.prCodes : ["—"]).map((c) => (
                          <span key={c} className="prtag">
                            {c}
                          </span>
                        ))}
                      </td>
                      <td>
                        {r.l.itemCode} <span className="hint">{r.l.description}</span>
                      </td>
                      <td className="amt">{r.l.qty}</td>
                      <td>{r.l.uom}</td>
                      <td className="amt">
                        {r.rMin == null || r.rMax == null
                          ? "—"
                          : `${vary ? `${fmt(r.rMin)}–${fmt(r.rMax)}` : fmt(r.rMin)}`}
                      </td>
                      <td className="amt">{r.amount == null ? "—" : fmt(r.amount)}</td>
                    </tr>
                  );
                })}
                {draft.lines.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: 18, textAlign: "center", color: "var(--muted)" }}>
                      No lines.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          <div className="card rv-card" data-rv="settings">
            <div className="chead">
              <h3>Settings</h3>
              <div className="spacer" />
              {editLink("Settings")}
            </div>
            <div className="cbody">
              <div className="grid g2">
                <div>
                  <div className="hint">Title</div>
                  <div className="rv-v">{draft.title || "—"}</div>
                </div>
                <div>
                  <div className="hint">Bid window</div>
                  <div className="rv-v">
                    {fmtLocal(draft.opensUtc)} → {fmtLocal(draft.closesUtc)}
                    {windowOrderInvalid ? <span className="ferr"> Mis-ordered</span> : null}
                  </div>
                </div>
                <div>
                  <div className="hint">Currency</div>
                  <div className="rv-v">{draft.currency}</div>
                </div>
                <div>
                  <div className="hint">Envelope</div>
                  <div className="rv-v">{draft.envelope === "Dual" ? "Dual envelope (sealed)" : "Single envelope"}</div>
                </div>
                <div>
                  <div className="hint">Clarification deadline</div>
                  <div className="rv-v">{fmtLocal(draft.clarificationDeadlineUtc)}</div>
                </div>
                <div>
                  <div className="hint">Bid validity</div>
                  <div className="rv-v">{draft.bidValidityDays ? `${draft.bidValidityDays} days` : "—"}</div>
                </div>
                <div>
                  <div className="hint">Partial bids</div>
                  <div className="rv-v">{draft.partialBidsAllowed ? "Allowed" : "Not allowed"}</div>
                </div>
                <div>
                  <div className="hint">Shipping (Incoterm)</div>
                  <div className="rv-v">
                    {draft.incotermId || draft.incotermCode
                      ? `${(() => {
                          const hit = incoterms.find((i) => i.id === draft.incotermId);
                          const head = hit ? `${hit.code} — ${hit.name}` : draft.incotermCode || "—";
                          return draft.incotermSuffix ? `${head} ${draft.incotermSuffix}` : head;
                        })()}`
                      : "—"}
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="card rv-card" data-rv="questions">
            <div className="chead">
              <h3>Questionnaire</h3>
              <div className="spacer" />
              <span className="hint" style={{ marginRight: 12 }}>
                {draft.items.filter((i) => i.kind === "question").length} question
                {draft.items.filter((i) => i.kind === "question").length === 1 ? "" : "s"}
              </span>
              {editLink("Questions")}
            </div>
            <div className="cbody">
              {(["Technical", "Commercial"] as const).map((g) => {
                const buckets = qBuckets(g);
                return (
                  <div key={g} className="rv-qgroup">
                    <div className="rv-qhead">
                      {g}
                      {draft.envelope === "Dual" ? " envelope" : ""}
                    </div>
                    {buckets.length === 0 ? <div className="hint">No questions.</div> : null}
                    {buckets.map((b) => (
                      <div key={b.name ?? "__ungrouped"} className="rv-qline">
                        {b.name ?? "Ungrouped"} — {b.list.length} question{b.list.length === 1 ? "" : "s"},{" "}
                        {b.list.filter((q) => q.required).length} required
                      </div>
                    ))}
                  </div>
                );
              })}
            </div>
          </div>

          <div className="card rv-card" data-rv="vendors">
            <div className="chead">
              <h3>Invited vendors</h3>
              <div className="spacer" />
              <span className="hint" style={{ marginRight: 12 }}>
                {liveInvites.length} invited
              </span>
              {editLink("Vendors")}
            </div>
            <div className="cbody">
              {liveInvites.length === 0 ? <div className="hint">No vendors invited yet.</div> : null}
              {liveInvites.map((inv) => {
                const v = allVendors.find((x) => x.id === inv.vendorId);
                const swecVendor = v ? isSwecType(v.type) || (v.categories ?? []).length > 0 : false;
                return (
                  <div key={inv.id} className="rv-row">
                    <span style={{ fontWeight: 600 }}>{inv.vendorName || inv.vendorCode || inv.vendorId}</span>
                    {swecVendor ? <span className="badge b-teal">SWEC</span> : null}
                  </div>
                );
              })}
              <div className="rv-reads hint">
                Category condition reads as: <b>{swecReadsAs(swecGroups)}</b>
              </div>
            </div>
          </div>

          {draft.envelope === "Dual" ? (
            <div className="card rv-card" data-rv="evaluators">
              <div className="chead">
                <h3>Evaluators</h3>
                <div className="spacer" />
                {editLink("Evaluators")}
              </div>
              <div className="cbody">
                <div className="grid g2">
                  <div>
                    <div className="hint">Technical envelope</div>
                    {draft.techEvals.length === 0 ? (
                      <div className="rv-v" style={{ color: "var(--red)" }}>
                        None assigned
                      </div>
                    ) : (
                      draft.techEvals.map((id) => (
                        <div key={id} className="rv-v">
                          {evaluatorName(id)}
                        </div>
                      ))
                    )}
                  </div>
                  <div>
                    <div className="hint">Commercial envelope</div>
                    {draft.commEvals.length === 0 ? (
                      <div className="rv-v" style={{ color: "var(--muted)" }}>
                        None assigned — needed before the commercial opening
                      </div>
                    ) : (
                      draft.commEvals.map((id) => (
                        <div key={id} className="rv-v">
                          {evaluatorName(id)}
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </div>
            </div>
          ) : null}

          <div className="card rv-card" data-rv="checklist">
            <div className="chead">
              <h3>Release readiness</h3>
            </div>
            <div className="cbody">
              {checklist.map((r) => (
                <div key={r.key} className={`rv-ck ${r.ok ? "ok" : r.hard ? "hard" : "warn"}`} data-ck={r.key}>
                  <span className="rv-ckic">
                    <Icon name={r.ok ? "check" : r.hard ? "x" : "flag"} size={13} />
                  </span>
                  <span className="rv-cklabel">{r.label}</span>
                  {!r.ok ? <span className="rv-ckwhy">{r.why}</span> : null}
                  {!r.ok && r.hard ? <span className="rv-ckblock">blocks release</span> : null}
                </div>
              ))}
              <p className="hint" style={{ margin: "12px 0 0" }}>
                Releasing captures the closing date as the server-side bid deadline and opens the RFQ to invited vendors.
                {hardFails.length > 0 ? (
                  <>
                    {" "}
                    <b>Release is blocked until the ✗ rows are resolved.</b>
                  </>
                ) : null}
              </p>
            </div>
          </div>
        </>
      ) : null}

      {showRelease ? (
        <ConfirmModal
          icon="send"
          title="Release RFQ to Vendors"
          body={
            <>
              <p className="hint" style={{ marginTop: 0 }}>
                Once released, the invited vendors can see this RFQ and start bidding. Line items and questions are
                locked after release — you can still send clarifications.
              </p>
              <div className="locked-note rv-cer">
                <div>
                  <b>{draft.title || "Untitled RFQ"}</b>
                  <br />
                  Releasing to <b>
                    {liveInvites.length} vendor{liveInvites.length === 1 ? "" : "s"}
                  </b>
                  :{" "}
                  {liveInvites
                    .slice(0, 6)
                    .map((i) => i.vendorName || i.vendorCode || i.vendorId)
                    .join(", ")}
                  {liveInvites.length > 6 ? ` +${liveInvites.length - 6} more` : ""}
                  <br />
                  Closes <b>{fmtLocal(draft.closesUtc)}</b> ·{" "}
                  {draft.envelope === "Dual" ? "Dual envelope (sealed)" : "Single envelope"}
                  {estTotal > 0 ? (
                    <>
                      {" "}
                      · Est. total {draft.currency} {fmt(estTotal)}
                    </>
                  ) : null}
                </div>
              </div>
              {ckWarns.length > 0 ? (
                <div className="rv-cerwarn">
                  <Icon name="flag" size={13} /> Releasing with warnings: {ckWarns.map((r) => r.why).join("; ")}.
                </div>
              ) : null}
            </>
          }
          cancelLabel="Not yet"
          confirmLabel="Release — irreversible"
          busy={release.isPending}
          onCancel={() => setShowRelease(false)}
          onConfirm={() => {
            setShowRelease(false);
            release.mutate(draft);
          }}
        />
      ) : null}

      {saveName !== null ? (
        <Modal
          icon="doc"
          title="Save as new form"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setSaveName(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={!saveName.trim() || saveAsForm.isPending}
                onClick={() => saveAsForm.mutate(saveName)}
              >
                <Icon name="check" size={15} /> Save form
              </button>
            </>
          }
        >
          <p className="hint" style={{ marginTop: 0 }}>
            Save this questionnaire to the form library so you can reuse it on future RFQs.
          </p>
          <div className="field">
            <label>Form name</label>
            <input
              type="text"
              autoFocus
              value={saveName}
              onChange={(e) => setSaveName(e.target.value)}
              placeholder="e.g. Standard Technical Questionnaire"
            />
          </div>
        </Modal>
      ) : null}

      {/* Add PRs drawer overlay */}
      {showAddPrs ? (
        <>
          <div className="mscrim" onClick={() => setShowAddPrs(false)} />
          <div className="addpr-overlay">
            <div className="addpr-head">
              <Icon name="rfq" size={18} />
              <h3>Add PRs to This RFQ</h3>
              <div style={{ flex: 1 }} />
              <button type="button" className="x" onClick={() => setShowAddPrs(false)}>
                ×
              </button>
            </div>
            <div className="addpr-body">
              <SourcingLinePicker
                mode="drawer"
                preloaded={draft.lines as (RfqLineInput & { sourcePrLineIds?: string[] | null })[]}
                onApply={(lines) => {
                  patch({ lines: lines as DraftLine[] });
                  setShowAddPrs(false);
                }}
              />
            </div>
          </div>
        </>
      ) : null}
      {guardDialog}
    </>
  );
}

/**
 * SWEC condition builder — OR-of-AND groups (DNF).
 * Each group is a list of SWEC category codes that must ALL match a vendor's categories.
 * Groups are OR'd together: vendor matches if it satisfies any group.
 */
function SwecConditionDialog({
  initial,
  onCancel,
  onApply,
}: {
  initial: string[][];
  onCancel: () => void;
  onApply: (groups: string[][]) => void;
}) {
  const { data: swec } = useSwec();
  const [groups, setGroups] = useState<string[][]>(() => initial.length > 0 ? initial.map((g) => [...g]) : [[]]);
  const [kw, setKw] = useState("");

  const addGroup = () => setGroups((g) => [...g, []]);
  const removeGroup = (gi: number) => setGroups((g) => g.filter((_, i) => i !== gi));
  const toggleCode = (gi: number, code: string) =>
    setGroups((g) =>
      g.map((grp, i) =>
        i !== gi ? grp : grp.includes(code) ? grp.filter((c) => c !== code) : [...grp, code],
      ),
    );

  const matches = (n: SwecNode): boolean => {
    if (!kw) return true;
    const k = kw.toLowerCase();
    return (
      (n.name ?? "").toLowerCase().includes(k) ||
      (n.code ?? "").toLowerCase().includes(k) ||
      n.children.some(matches)
    );
  };

  const renderNodes = (nodes: SwecNode[], depth: number, gi: number, sel: Set<string>): React.ReactNode =>
    nodes.filter(matches).map((n) => (
      <div key={n.code}>
        <label className="swnode" style={{ paddingLeft: depth * 18 }}>
          <input
            type="checkbox"
            checked={sel.has(n.code)}
            onChange={() => toggleCode(gi, n.code)}
            style={{ width: "auto" }}
          />
          <span style={{ fontWeight: n.isLeaf ? 400 : 700 }}>{n.name}</span>
          {n.isLeaf ? <span className="hint" style={{ marginLeft: "auto" }}>{n.code}</span> : null}
        </label>
        {n.children.length > 0 ? renderNodes(n.children, depth + 1, gi, sel) : null}
      </div>
    ));

  const readsAs = () => {
    const nonEmpty = groups.filter((g) => g.length > 0);
    if (nonEmpty.length === 0) return "No condition — all vendors shown.";
    const labelGroup = (g: string[]) => g.map((c) => swec?.label(c) ?? c).join(" AND ");
    return nonEmpty.map((g) => `(${labelGroup(g)})`).join(" OR ");
  };

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal" style={{ maxWidth: 700 }}>
        <div className="mhead">
          <Icon name="vendor" size={20} />
          <h3>SWEC category condition</h3>
        </div>
        <div className="mbody">
          <p className="hint" style={{ marginTop: 0 }}>
            Build an OR-of-AND filter. Each group requires ALL its categories. A vendor matches if it satisfies ANY group.
          </p>
          <div className="field" style={{ marginBottom: 12 }}>
            <input type="text" placeholder="Search categories…" value={kw} onChange={(e) => setKw(e.target.value)} />
          </div>
          {groups.map((grp, gi) => {
            const sel = new Set(grp);
            return (
              <div key={gi} style={{ marginBottom: 16, border: "1px solid var(--border)", borderRadius: 8, padding: 12 }}>
                <div style={{ display: "flex", alignItems: "center", marginBottom: 8 }}>
                  <b style={{ fontSize: 13 }}>Group {gi + 1}</b>
                  <div style={{ flex: 1 }} />
                  {groups.length > 1 ? (
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeGroup(gi)}>
                      <Icon name="x" size={13} /> Remove
                    </button>
                  ) : null}
                </div>
                <div className="swtree" style={{ maxHeight: 200, overflow: "auto" }}>
                  {swec ? renderNodes(swec.tree, 0, gi, sel) : <span className="hint">Loading…</span>}
                </div>
                {grp.length > 0 ? (
                  <div style={{ marginTop: 8, display: "flex", flexWrap: "wrap", gap: 4 }}>
                    {grp.map((c) => (
                      <span key={c} className="swchip" style={{ cursor: "pointer" }} onClick={() => toggleCode(gi, c)}>
                        {swec?.label(c) ?? c} ✕
                      </span>
                    ))}
                  </div>
                ) : null}
                {gi < groups.length - 1 ? (
                  <div style={{ textAlign: "center", margin: "8px 0 0", fontSize: 12, fontWeight: 700, color: "var(--muted)" }}>OR</div>
                ) : null}
              </div>
            );
          })}
          <button type="button" className="btn btn-out btn-sm" onClick={addGroup}>
            <Icon name="plus" size={13} /> Add OR group
          </button>
          <div style={{ marginTop: 12, padding: "8px 12px", background: "var(--bg-hover)", borderRadius: 6, fontSize: 13 }}>
            <b>Reads as:</b> {readsAs()}
          </div>
        </div>
        <div className="mfoot">
          <button type="button" className="btn btn-out" onClick={onCancel}>Cancel</button>
          <button type="button" className="btn btn-pri" onClick={() => onApply(groups.filter((g) => g.length > 0))}>
            <Icon name="check" size={15} /> Apply filter
          </button>
        </div>
      </div>
    </div>
  );
}

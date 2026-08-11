import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import {
  cancelRequisition,
  cancelRequisitionLine,
  createRequisition,
  getRequisition,
  releaseRequisitionLine,
  reopenRequisitionLine,
  submitRequisition,
  unreserveRequisitionLine,
  updateRequisition,
  type PrLineInput,
} from "@/api/sourcing";
import { listItems, listLocations, listTaxCodes, type ItemDto, type TaxCodeDto } from "@/api/configuration";
import { listOrgUnits, type OrgUnitDto } from "@/api/platform";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { CustomFieldsSection } from "@/components/customfields/CustomFieldsSection";
import { PoFromPrModal } from "@/pages/procurement/PoFromPrModal";
import { ApiRequestError } from "@/lib/api-client";
import { fmt } from "@/lib/format";
import { useAuth } from "@/auth/use-auth";
import { FshPermissions } from "@/lib/fsh-permissions";
import { Gated } from "@/components/Gated";

function unitsOfType(units: OrgUnitDto[], type: string): OrgUnitDto[] {
  return units.filter((u) => u.type === type);
}

function matchOrgUnitId(units: OrgUnitDto[], name: string, code?: string | null): string {
  if (code) {
    const byCode = units.find((u) => u.code === code);
    if (byCode) return byCode.id;
  }
  if (name) {
    const byName = units.find((u) => u.name === name || u.code === name);
    if (byName) return byName.id;
  }
  return "";
}

type TabKey = "items" | "shipping" | "attachments";

type DraftLine = {
  key: number;
  id?: string;
  itemCode: string;
  description: string;
  qty: number;
  uom: string;
  estUnitPrice: number;
  taxCodeId: string;
  lifecycleStatus: string;
  editable: boolean;
};

let keySeq = 1;

function todayLocalIso(): string {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function isRequiredOnInPast(value: string): boolean {
  return Boolean(value) && value < todayLocalIso();
}

function lineSst(amount: number, ratePct: number): number {
  return Math.round((amount * ratePct) / 100 + Number.EPSILON * 100) / 100;
}

function taxLabel(code: string, ratePct: number | null | undefined): string {
  if (!code) return "—";
  if (ratePct == null) return code;
  return `${code} ${Number(ratePct)}%`;
}

function blankLine(): DraftLine {
  return {
    key: keySeq++,
    itemCode: "",
    description: "",
    qty: 1,
    uom: "Unit",
    estUnitPrice: 0,
    taxCodeId: "",
    lifecycleStatus: "Open",
    editable: true,
  };
}

function lineEst(l: DraftLine): number {
  return (Number(l.qty) || 0) * (Number(l.estUnitPrice) || 0);
}

function lineSstFor(l: DraftLine, taxById: Map<string, TaxCodeDto>): number {
  if (!l.taxCodeId) return 0;
  const tc = taxById.get(l.taxCodeId);
  if (!tc) return 0;
  return lineSst(lineEst(l), tc.ratePct);
}

export function RequisitionFormPage({
  id,
  onSaved,
  onBack,
}: {
  id?: string;
  onSaved: (id: string) => void;
  onBack: () => void;
}) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { user: authUser } = useAuth();
  const isNew = !id;

  const { data: existing, isPending } = useQuery({
    queryKey: ["requisition", id],
    queryFn: () => getRequisition(id!),
    enabled: !isNew,
  });

  const { data: taxCodes = [] } = useQuery({
    queryKey: ["tax-codes", true],
    queryFn: () => listTaxCodes(true),
    staleTime: 60_000,
  });
  const { data: items = [] } = useQuery({
    queryKey: ["items", true],
    queryFn: () => listItems(true),
    staleTime: 60_000,
  });
  const { data: locations = [] } = useQuery({
    queryKey: ["locations", true],
    queryFn: () => listLocations(true),
    staleTime: 60_000,
  });
  const { data: orgUnits = [] } = useQuery({
    queryKey: ["org-units", true],
    queryFn: () => listOrgUnits(undefined, true),
    staleTime: 60_000,
  });

  const taxById = useMemo(() => new Map(taxCodes.map((t) => [t.id, t])), [taxCodes]);
  const itemByCode = useMemo(() => new Map(items.map((i) => [i.itemCode, i])), [items]);
  const deptUnits = useMemo(() => unitsOfType(orgUnits, "Department"), [orgUnits]);
  const classLocationUnits = useMemo(() => unitsOfType(orgUnits, "Location"), [orgUnits]);
  const categoryUnits = useMemo(() => unitsOfType(orgUnits, "Category"), [orgUnits]);
  const projectUnits = useMemo(() => unitsOfType(orgUnits, "Project"), [orgUnits]);

  const [requestor, setRequestor] = useState("");
  const [department, setDepartment] = useState("");
  const [departmentCode, setDepartmentCode] = useState<string | null>(null);
  const [location, setLocation] = useState("");
  const [locationCode, setLocationCode] = useState<string | null>(null);
  const [category, setCategory] = useState("");
  const [categoryCode, setCategoryCode] = useState<string | null>(null);
  const [job, setJob] = useState("");
  const [memo, setMemo] = useState("");
  const [costCentre, setCostCentre] = useState("");
  const [project, setProject] = useState("");
  const [requiredOn, setRequiredOn] = useState("");
  const [createPoOpen, setCreatePoOpen] = useState(false);
  const [currency, setCurrency] = useState("MYR");
  const [lines, setLines] = useState<DraftLine[]>(() => [blankLine()]);
  const [shipMode, setShipMode] = useState<"location" | "adhoc">("location");
  const [shipToLocationId, setShipToLocationId] = useState("");
  const [shipToAddressId, setShipToAddressId] = useState("");
  const [shipToAdhoc, setShipToAdhoc] = useState("");
  const [tab, setTab] = useState<TabKey>("items");
  const [err, setErr] = useState<string | null>(null);
  const [hydratedId, setHydratedId] = useState<string | null>(null);
  const [lineAction, setLineAction] = useState<{ lineId: string; action: "cancel" | "release" } | null>(null);
  const [reason, setReason] = useState("");
  const [cancelling, setCancelling] = useState(false);

  useEffect(() => {
    if (!existing || hydratedId === existing.id) return;
    setRequestor(existing.requestor);
    setDepartment(existing.department);
    setDepartmentCode(existing.departmentCode ?? null);
    setLocation(existing.location);
    setLocationCode(existing.locationCode ?? null);
    setCategory(existing.category);
    setCategoryCode(existing.categoryCode ?? null);
    setJob(existing.job);
    setMemo(existing.memo);
    setCostCentre(existing.costCentre);
    setProject(existing.project ?? "");
    setRequiredOn(existing.requiredOn ?? "");
    setCurrency(existing.currency || "MYR");
    setLines(
      existing.lines.length
        ? existing.lines.map((l) => ({
            key: keySeq++,
            id: l.id,
            itemCode: l.itemCode,
            description: l.description,
            qty: l.qty,
            uom: l.uom,
            estUnitPrice: l.estUnitPrice,
            taxCodeId: l.taxCodeId ?? "",
            lifecycleStatus: l.lifecycleStatus,
            editable: l.lifecycleStatus === "Open",
          }))
        : [blankLine()],
    );
    setShipToLocationId(existing.shipToLocationId ?? "");
    setShipToAddressId(existing.shipToAddressId ?? "");
    setShipToAdhoc(existing.shipToAdhoc ?? "");
    setShipMode(existing.shipToAdhoc ? "adhoc" : "location");
    setHydratedId(existing.id);
  }, [existing, hydratedId]);

  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);
  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["requisition", id] });
    setHydratedId(null);
  };

  const editable = isNew || (existing != null && !existing.submitted && existing.headerStatus !== "Cancelled");
  const headerStatus = existing?.headerStatus ?? "Draft";
  const today = todayLocalIso();
  const requiredOnMin =
    !isNew && existing?.requiredOn && existing.requiredOn < today ? existing.requiredOn : today;

  const totals = useMemo(() => {
    let subtotal = 0;
    let sst = 0;
    for (const l of lines) {
      const est = lineEst(l);
      subtotal += est;
      sst += lineSstFor(l, taxById);
    }
    return { subtotal, sst, gross: subtotal + sst };
  }, [lines, taxById]);

  const selectedDeptId = useMemo(
    () => matchOrgUnitId(deptUnits, department, departmentCode),
    [deptUnits, department, departmentCode],
  );
  const selectedClassLocationId = useMemo(
    () => matchOrgUnitId(classLocationUnits, location, locationCode),
    [classLocationUnits, location, locationCode],
  );
  const selectedCategoryId = useMemo(
    () => matchOrgUnitId(categoryUnits, category, categoryCode),
    [categoryUnits, category, categoryCode],
  );
  const selectedProjectId = useMemo(
    () => matchOrgUnitId(projectUnits, project ?? "", null),
    [projectUnits, project],
  );

  const shipLocation = locations.find((l) => l.id === shipToLocationId);

  const toLineInputs = (): PrLineInput[] =>
    lines
      .filter((l) => l.itemCode.trim())
      .map((l) => ({
        id: l.id ?? null,
        itemCode: l.itemCode.trim(),
        description: l.description,
        qty: Number(l.qty) || 0,
        uom: l.uom || "Unit",
        estUnitPrice: Number(l.estUnitPrice) || 0,
        taxCodeId: l.taxCodeId || null,
      }));

  const shipToPayload = () => {
    if (shipMode === "adhoc") {
      return {
        shipToLocationId: null as string | null,
        shipToAddressId: null as string | null,
        shipToAdhoc: shipToAdhoc.trim() || null,
      };
    }
    if (shipToLocationId && shipToAddressId) {
      return {
        shipToLocationId,
        shipToAddressId,
        shipToAdhoc: null as string | null,
      };
    }
    return {
      shipToLocationId: null as string | null,
      shipToAddressId: null as string | null,
      shipToAdhoc: null as string | null,
    };
  };

  const ensureRequiredOnOk = (): boolean => {
    if (isRequiredOnInPast(requiredOn)) {
      const original = existing?.requiredOn ?? "";
      if (!isNew && requiredOn === original) {
        setErr(null);
        return true;
      }
      setErr("Required on cannot be a date in the past.");
      return false;
    }
    setErr(null);
    return true;
  };

  const ensureLinesOk = (): boolean => {
    const inputs = toLineInputs();
    if (inputs.length === 0) {
      setErr("Add at least one line with an item code.");
      return false;
    }
    if (inputs.some((l) => l.qty <= 0)) {
      setErr("Each line needs a quantity greater than zero.");
      return false;
    }
    return true;
  };

  const create = useMutation({
    mutationFn: (submit: boolean) =>
      createRequisition({
        requestor,
        department,
        departmentCode,
        location,
        locationCode,
        category,
        categoryCode,
        job,
        memo,
        costCentre,
        project: project || null,
        requiredOn: requiredOn || null,
        currency,
        lines: toLineInputs(),
        submit,
        ...shipToPayload(),
      }),
    onSuccess: (newId) => onSaved(newId),
    onError: onErr,
  });

  const update = useMutation({
    mutationFn: () =>
      updateRequisition(id!, {
        department,
        departmentCode,
        location,
        locationCode,
        category,
        categoryCode,
        job,
        memo,
        costCentre,
        project: project || null,
        requiredOn: requiredOn || null,
        lines: toLineInputs(),
        ...shipToPayload(),
      }),
    onSuccess: refresh,
    onError: onErr,
  });

  const submit = useMutation({
    mutationFn: async () => {
      if (editable) {
        await updateRequisition(id!, {
          department,
          departmentCode,
          location,
          locationCode,
          category,
          categoryCode,
          job,
          memo,
          costCentre,
          project: project || null,
          requiredOn: requiredOn || null,
          lines: toLineInputs(),
          ...shipToPayload(),
        });
      }
      return submitRequisition(id!);
    },
    onSuccess: refresh,
    onError: onErr,
  });

  const cancelPr = useMutation({
    mutationFn: () => cancelRequisition(id!),
    onSuccess: () => {
      setCancelling(false);
      refresh();
    },
    onError: onErr,
  });

  const cancelLine = useMutation({
    mutationFn: () => cancelRequisitionLine(id!, lineAction!.lineId, reason || null),
    onSuccess: () => {
      setLineAction(null);
      setReason("");
      refresh();
    },
    onError: onErr,
  });

  const releaseLine = useMutation({
    mutationFn: () => releaseRequisitionLine(id!, lineAction!.lineId, reason || null),
    onSuccess: () => {
      setLineAction(null);
      setReason("");
      refresh();
    },
    onError: onErr,
  });

  const reopenLine = useMutation({
    mutationFn: (lineId: string) => reopenRequisitionLine(id!, lineId),
    onSuccess: refresh,
    onError: onErr,
  });

  const unreserveLine = useMutation({
    mutationFn: (lineId: string) => unreserveRequisitionLine(id!, lineId),
    onSuccess: refresh,
    onError: onErr,
  });

  const addLine = () => setLines((xs) => [...xs, blankLine()]);
  const removeLine = (key: number) =>
    setLines((xs) => (xs.length <= 1 ? xs : xs.filter((l) => l.key !== key)));

  const updateLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((xs) => xs.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  const pickItem = (key: number, item: ItemDto | null) => {
    if (!item) {
      updateLine(key, { itemCode: "" });
      return;
    }
    updateLine(key, {
      itemCode: item.itemCode,
      description: item.description || "",
      uom: item.uom || "Unit",
    });
  };

  const busy = create.isPending || update.isPending || submit.isPending;
  const canCreatePo =
    !isNew &&
    ["Submitted", "PartiallySourced", "PartiallyOrdered"].includes(headerStatus) &&
    (authUser?.permissions ?? []).includes(FshPermissions.purchaseOrders.createFromRequisition);
  const hasLiveLine = existing?.lines.some((l) => l.lifecycleStatus === "InRfq" || l.lifecycleStatus === "Awarded");

  if (!isNew && isPending) return <Spinner label="Loading requisition…" />;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Requisitions
        </button>{" "}
        <Icon name="chev" size={12} /> {isNew ? "New PR" : existing?.code}
      </div>

      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {isNew ? "Create Requisition" : existing?.code}{" "}
            {existing ? <SourcingStatusBadge status={existing.headerStatus} /> : null}
          </h1>
          <p>{isNew ? "Raise demand for goods or services." : `Requested by ${existing?.requestor}`}</p>
        </div>
        <div className="spacer" />
        <div className="pr-form-actions">
          {canCreatePo ? (
            <Gated permission={FshPermissions.purchaseOrders.createFromRequisition}>
              <button type="button" className="btn btn-out btn-sm" onClick={() => setCreatePoOpen(true)}>
                <Icon name="box" size={14} /> Create Purchase Order
              </button>
            </Gated>
          ) : null}
          {isNew ? (
            <>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={busy || !requestor.trim()}
                onClick={() => {
                  if (!ensureRequiredOnOk() || !ensureLinesOk()) return;
                  create.mutate(false);
                }}
              >
                Save draft
              </button>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                disabled={busy || !requestor.trim()}
                onClick={() => {
                  if (!ensureRequiredOnOk() || !ensureLinesOk()) return;
                  create.mutate(true);
                }}
              >
                <Icon name="check" size={14} /> Submit Requisition
              </button>
            </>
          ) : editable ? (
            <>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={busy}
                onClick={() => {
                  if (!ensureRequiredOnOk() || !ensureLinesOk()) return;
                  update.mutate();
                }}
              >
                Save changes
              </button>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                disabled={busy}
                onClick={() => {
                  if (!ensureRequiredOnOk() || !ensureLinesOk()) return;
                  submit.mutate();
                }}
              >
                <Icon name="check" size={14} /> Submit Requisition
              </button>
            </>
          ) : null}
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="pr-form-top">
        <div className="card pr-form-header">
          <div className="chead">
            <h3>Header</h3>
          </div>
          <div className="cbody">
            <div className="grid g2">
              {isNew || editable ? (
                <div className="field">
                  <label>Requestor</label>
                  <input
                    value={requestor}
                    placeholder="Name"
                    onChange={(e) => setRequestor(e.target.value)}
                    disabled={!isNew}
                  />
                </div>
              ) : (
                <div className="field">
                  <label>Requestor</label>
                  <input value={requestor} disabled />
                </div>
              )}
              <div className="field">
                <label>Required by</label>
                <input
                  type="date"
                  value={requiredOn}
                  min={requiredOnMin}
                  onChange={(e) => setRequiredOn(e.target.value)}
                  disabled={!editable}
                />
              </div>
              <div className="field">
                <label>Job / cost ref</label>
                <input
                  value={job}
                  placeholder="JOB-…"
                  onChange={(e) => setJob(e.target.value)}
                  disabled={!editable}
                />
              </div>
              <div className="field">
                <label>Cost centre</label>
                <input value={costCentre} onChange={(e) => setCostCentre(e.target.value)} disabled={!editable} />
              </div>
            </div>
            <div className="field" style={{ marginBottom: 0 }}>
              <label>Memo / justification</label>
              <input
                value={memo}
                placeholder="Short description of the requirement"
                onChange={(e) => setMemo(e.target.value)}
                disabled={!editable}
              />
            </div>
          </div>
        </div>

        <div className="card pr-form-summary">
          <div className="chead">
            <h3>Summary</h3>
          </div>
          <div className="cbody">
            <div className="pr-summ-total">
              <div className="hint">Estimated total</div>
              <div className="pr-summ-num">
                {currency} {fmt(totals.gross)}
              </div>
            </div>
            <div className="pr-summ-rows">
              <div>
                <span>Subtotal</span>
                <strong>
                  {currency} {fmt(totals.subtotal)}
                </strong>
              </div>
              <div>
                <span>SST amount</span>
                <strong>
                  {currency} {fmt(totals.sst)}
                </strong>
              </div>
              <div>
                <span>Gross amount</span>
                <strong>
                  {currency} {fmt(totals.gross)}
                </strong>
              </div>
              <div>
                <span>Currency</span>
                <strong>
                  {isNew && editable ? (
                    <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
                      <option value="MYR">MYR</option>
                      <option value="USD">USD</option>
                      <option value="SGD">SGD</option>
                    </select>
                  ) : (
                    currency
                  )}
                </strong>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Classification</h3>
        </div>
        <div className="cbody">
          <div className="grid g2 pr-class-grid">
            <div className="field">
              <label>Department</label>
              <select
                value={selectedDeptId}
                disabled={!editable}
                onChange={(e) => {
                  const u = deptUnits.find((x) => x.id === e.target.value);
                  setDepartment(u?.name ?? "");
                  setDepartmentCode(u?.code ?? null);
                }}
              >
                <option value="">Select department…</option>
                {deptUnits.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code} — {u.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Location</label>
              <select
                value={selectedClassLocationId}
                disabled={!editable}
                onChange={(e) => {
                  const u = classLocationUnits.find((x) => x.id === e.target.value);
                  setLocation(u?.name ?? "");
                  setLocationCode(u?.code ?? null);
                }}
              >
                <option value="">Select location…</option>
                {classLocationUnits.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code} — {u.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Category</label>
              <select
                value={selectedCategoryId}
                disabled={!editable}
                onChange={(e) => {
                  const u = categoryUnits.find((x) => x.id === e.target.value);
                  setCategory(u?.name ?? "");
                  setCategoryCode(u?.code ?? null);
                }}
              >
                <option value="">Select category…</option>
                {categoryUnits.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code} — {u.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Project</label>
              <select
                value={selectedProjectId}
                disabled={!editable}
                onChange={(e) => {
                  const u = projectUnits.find((x) => x.id === e.target.value);
                  setProject(u?.name ?? "");
                }}
              >
                <option value="">Select project…</option>
                {projectUnits.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code} — {u.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <p className="hint" style={{ margin: "10px 0 0" }}>
            Values come from Setup → Lookups → Organisation units (types Department, Location, Category, Project).
          </p>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="vtabs" style={{ padding: "0 16px", marginBottom: 0 }}>
          {(
            [
              ["items", "Items"],
              ["shipping", "Shipping"],
              ["attachments", "Attachments"],
            ] as const
          ).map(([key, label]) => (
            <button
              key={key}
              type="button"
              className={`vtab${tab === key ? " on" : ""}`}
              onClick={() => setTab(key)}
            >
              {label}
            </button>
          ))}
        </div>

        {tab === "items" ? (
          <div className="cbody">
            <div className="chead" style={{ padding: "0 0 10px", border: 0 }}>
              <h3 style={{ fontSize: 14 }}>Item lines</h3>
              {editable ? (
                <>
                  <div className="spacer" style={{ flex: 1 }} />
                  <button type="button" className="lnk" onClick={addLine}>
                    <Icon name="plus" size={13} /> Add line
                  </button>
                </>
              ) : null}
            </div>
            <div className="pr-lines-scroll">
              <table>
                <thead>
                  <tr>
                    <th style={{ minWidth: 48 }}>Line</th>
                    <th style={{ minWidth: 150 }}>Item code</th>
                    <th style={{ minWidth: 220 }}>Description</th>
                    <th className="amt" style={{ minWidth: 90 }}>
                      Qty
                    </th>
                    <th style={{ minWidth: 80 }}>UoM</th>
                    <th className="amt" style={{ minWidth: 100 }}>
                      Est. rate
                    </th>
                    <th className="amt" style={{ minWidth: 110 }}>
                      Est. amount
                    </th>
                    <th style={{ minWidth: 130 }}>Tax code</th>
                    <th className="amt" style={{ minWidth: 90 }}>
                      SST
                    </th>
                    <th className="amt" style={{ minWidth: 100 }}>
                      Gross
                    </th>
                    {!isNew ? <th>Status</th> : null}
                    <th style={{ minWidth: 40 }} />
                  </tr>
                </thead>
                <tbody>
                  {lines.map((l, i) => {
                    const est = lineEst(l);
                    const sst = lineSstFor(l, taxById);
                    const ro = !editable || !l.editable;
                    return (
                      <tr key={l.key}>
                        <td className="hint">{i + 1}</td>
                        <td>
                          {ro ? (
                            l.itemCode
                          ) : (
                            <select
                              value={l.itemCode}
                              onChange={(e) => {
                                const code = e.target.value;
                                const item = itemByCode.get(code) ?? null;
                                if (item) pickItem(l.key, item);
                                else updateLine(l.key, { itemCode: code });
                              }}
                            >
                              <option value="">Pick an item</option>
                              {items.map((it) => (
                                <option key={it.id} value={it.itemCode}>
                                  {it.itemCode} — {it.description}
                                </option>
                              ))}
                            </select>
                          )}
                        </td>
                        <td>
                          {ro ? (
                            l.description
                          ) : (
                            <input
                              value={l.description}
                              onChange={(e) => updateLine(l.key, { description: e.target.value })}
                            />
                          )}
                        </td>
                        <td className="amt">
                          {ro ? (
                            l.qty
                          ) : (
                            <input
                              type="number"
                              min={0}
                              step="any"
                              value={l.qty}
                              style={{ width: 80 }}
                              onChange={(e) => updateLine(l.key, { qty: Number(e.target.value) })}
                            />
                          )}
                        </td>
                        <td>
                          {ro ? (
                            l.uom
                          ) : (
                            <input
                              value={l.uom}
                              style={{ width: 70 }}
                              onChange={(e) => updateLine(l.key, { uom: e.target.value })}
                            />
                          )}
                        </td>
                        <td className="amt">
                          {ro ? (
                            fmt(l.estUnitPrice)
                          ) : (
                            <input
                              type="number"
                              min={0}
                              step="any"
                              value={l.estUnitPrice}
                              style={{ width: 100 }}
                              onChange={(e) => updateLine(l.key, { estUnitPrice: Number(e.target.value) })}
                            />
                          )}
                        </td>
                        <td className="amt">{fmt(est)}</td>
                        <td>
                          {ro ? (
                            taxLabel(
                              taxById.get(l.taxCodeId)?.code ?? "",
                              taxById.get(l.taxCodeId)?.ratePct ?? null,
                            ) || "—"
                          ) : (
                            <select
                              value={l.taxCodeId}
                              onChange={(e) => updateLine(l.key, { taxCodeId: e.target.value })}
                            >
                              <option value="">— No tax</option>
                              {taxCodes.map((t) => (
                                <option key={t.id} value={t.id}>
                                  {taxLabel(t.code, t.ratePct)}
                                </option>
                              ))}
                            </select>
                          )}
                        </td>
                        <td className="amt">{sst > 0 ? fmt(sst) : "—"}</td>
                        <td className="amt">{fmt(est + sst)}</td>
                        {!isNew ? (
                          <td>
                            <span className="hint">{l.lifecycleStatus}</span>
                            {l.id && existing && !existing.submitted ? (
                              <div style={{ display: "flex", gap: 4, marginTop: 4 }}>
                                {l.lifecycleStatus === "Open" ? (
                                  <button
                                    type="button"
                                    className="lnk"
                                    onClick={() => setLineAction({ lineId: l.id!, action: "cancel" })}
                                  >
                                    Cancel
                                  </button>
                                ) : null}
                                {l.lifecycleStatus === "InDraftRfq" ? (
                                  <button type="button" className="lnk" onClick={() => unreserveLine.mutate(l.id!)}>
                                    Unreserve
                                  </button>
                                ) : null}
                                {l.lifecycleStatus === "Cancelled" ? (
                                  <button type="button" className="lnk" onClick={() => reopenLine.mutate(l.id!)}>
                                    Reopen
                                  </button>
                                ) : null}
                                {l.lifecycleStatus === "Open" ? (
                                  <button
                                    type="button"
                                    className="lnk"
                                    onClick={() => setLineAction({ lineId: l.id!, action: "release" })}
                                  >
                                    Release
                                  </button>
                                ) : null}
                              </div>
                            ) : null}
                          </td>
                        ) : null}
                        <td className="amt">
                          {editable && l.editable ? (
                            <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(l.key)}>
                              <Icon name="x" size={13} />
                            </button>
                          ) : null}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {tab === "shipping" ? (
          <div className="cbody">
            <p className="hint" style={{ marginTop: 0 }}>
              Optional ship-to suggestion for a direct purchase order. Not required to submit.
            </p>
            {existing?.shipTo && !editable ? (
              <Notice tone="info">{existing.shipTo}</Notice>
            ) : null}
            <div style={{ display: "flex", gap: 16, marginBottom: 10 }}>
              <label className="hint" style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <input
                  type="radio"
                  checked={shipMode === "location"}
                  disabled={!editable}
                  onChange={() => setShipMode("location")}
                />{" "}
                Location address
              </label>
              <label className="hint" style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <input
                  type="radio"
                  checked={shipMode === "adhoc"}
                  disabled={!editable}
                  onChange={() => setShipMode("adhoc")}
                />{" "}
                Ad-hoc address
              </label>
            </div>
            {shipMode === "location" ? (
              <div className="grid g2">
                <div className="field">
                  <label>Location</label>
                  <select
                    value={shipToLocationId}
                    disabled={!editable}
                    onChange={(e) => {
                      setShipToLocationId(e.target.value);
                      setShipToAddressId("");
                    }}
                  >
                    <option value="">Select…</option>
                    {locations.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Address</label>
                  <select
                    value={shipToAddressId}
                    disabled={!editable || !shipLocation}
                    onChange={(e) => setShipToAddressId(e.target.value)}
                  >
                    <option value="">Select…</option>
                    {(shipLocation?.addresses ?? []).map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.label} — {a.line1}, {a.city}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            ) : (
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Ad-hoc address</label>
                <textarea
                  value={shipToAdhoc}
                  disabled={!editable}
                  rows={3}
                  maxLength={400}
                  onChange={(e) => setShipToAdhoc(e.target.value)}
                />
              </div>
            )}
          </div>
        ) : null}

        {tab === "attachments" ? (
          <div className="cbody">
            {isNew ? (
              <EmptyStateHint>Save the requisition first to attach files.</EmptyStateHint>
            ) : (
              <EmptyStateHint>Attachments API is not wired yet for requisitions.</EmptyStateHint>
            )}
          </div>
        ) : null}
      </div>

      <CustomFieldsSection recordType="Requisition" recordId={id ?? null} readOnly={!editable} />

      {!isNew ? (
        <div className="actionbar" style={{ marginTop: 14 }}>
          {existing?.headerStatus !== "Cancelled" ? (
            <button
              type="button"
              className="btn btn-out"
              style={{ color: "var(--red)" }}
              disabled={Boolean(hasLiveLine)}
              title={hasLiveLine ? "Cannot cancel while a line is in an RFQ or awarded." : undefined}
              onClick={() => setCancelling(true)}
            >
              Cancel PR
            </button>
          ) : (
            <span className="hint">This requisition is cancelled.</span>
          )}
          <div className="spacer" style={{ flex: 1 }} />
        </div>
      ) : null}

      {lineAction ? (
        <ConfirmModal
          title={lineAction.action === "cancel" ? "Cancel line" : "Release for re-sourcing"}
          icon="flag"
          body={
            <div className="field" style={{ marginBottom: 0 }}>
              <label>Reason (optional)</label>
              <textarea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
            </div>
          }
          confirmLabel={lineAction.action === "cancel" ? "Cancel line" : "Release"}
          danger={lineAction.action === "cancel"}
          busy={cancelLine.isPending || releaseLine.isPending}
          onCancel={() => {
            setLineAction(null);
            setReason("");
          }}
          onConfirm={() => (lineAction.action === "cancel" ? cancelLine.mutate() : releaseLine.mutate())}
        />
      ) : null}

      {cancelling ? (
        <ConfirmModal
          title="Cancel requisition"
          icon="x"
          body="This cancels the requisition and every open line. This cannot be undone."
          confirmLabel="Cancel requisition"
          danger
          busy={cancelPr.isPending}
          onCancel={() => setCancelling(false)}
          onConfirm={() => cancelPr.mutate()}
        />
      ) : null}

      {createPoOpen && id ? (
        <PoFromPrModal
          initialPrId={id}
          onClose={() => setCreatePoOpen(false)}
          onCreated={(poId) => {
            setCreatePoOpen(false);
            void navigate(`/pos/${poId}`);
          }}
        />
      ) : null}
    </>
  );
}

function EmptyStateHint({ children }: { children: ReactNode }) {
  return (
    <div className="hint" style={{ padding: "24px 8px", textAlign: "center" }}>
      {children}
    </div>
  );
}

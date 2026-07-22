import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { ApiRequestError } from "@/lib/api-client";

type DraftLine = PrLineInput & { key: number };
let keySeq = 1;

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
  const isNew = !id;

  const { data: existing, isPending } = useQuery({
    queryKey: ["requisition", id],
    queryFn: () => getRequisition(id!),
    enabled: !isNew,
  });

  const [requestor, setRequestor] = useState("");
  const [department, setDepartment] = useState("");
  const [location, setLocation] = useState("");
  const [category, setCategory] = useState("");
  const [job, setJob] = useState("");
  const [memo, setMemo] = useState("");
  const [costCentre, setCostCentre] = useState("");
  const [project, setProject] = useState("");
  const [requiredOn, setRequiredOn] = useState("");
  const [currency, setCurrency] = useState("MYR");
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [lineAction, setLineAction] = useState<{ lineId: string; action: "cancel" | "release" } | null>(null);
  const [reason, setReason] = useState("");
  const [cancelling, setCancelling] = useState(false);

  const hydrated = existing && lines.length === 0 && department === "";
  if (hydrated && existing) {
    setDepartment(existing.department);
    setLocation(existing.location);
    setCategory(existing.category);
    setJob(existing.job);
    setMemo(existing.memo);
    setCostCentre(existing.costCentre);
    setProject(existing.project ?? "");
    setRequiredOn(existing.requiredOn ?? "");
  }

  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);
  const refresh = () => void qc.invalidateQueries({ queryKey: ["requisition", id] });

  const addLine = () =>
    setLines((xs) => [...xs, { key: keySeq++, itemCode: "", description: "", qty: 1, uom: "EA", estUnitPrice: 0 }]);
  const removeLine = (key: number) => setLines((xs) => xs.filter((l) => l.key !== key));
  const updateLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((xs) => xs.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  const create = useMutation({
    mutationFn: (submit: boolean) =>
      createRequisition({
        requestor,
        department,
        location,
        category,
        job,
        memo,
        costCentre,
        project: project || null,
        requiredOn: requiredOn || null,
        currency,
        lines: lines.map(({ key: _key, ...l }) => l),
        submit,
      }),
    onSuccess: (newId) => onSaved(newId),
    onError: onErr,
  });

  const update = useMutation({
    mutationFn: () =>
      updateRequisition(id!, {
        department,
        location,
        category,
        job,
        memo,
        costCentre,
        project: project || null,
        requiredOn: requiredOn || null,
      }),
    onSuccess: refresh,
    onError: onErr,
  });

  const submit = useMutation({
    mutationFn: () => submitRequisition(id!),
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

  if (!isNew && isPending) return <Spinner label="Loading requisition…" />;

  const hasLiveLine = existing?.lines.some((l) => l.lifecycleStatus === "InRfq" || l.lifecycleStatus === "Awarded");
  const editable = isNew || (existing && !existing.submitted);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Requisitions
        </button>{" "}
        <Icon name="chev" size={12} /> {isNew ? "New" : existing?.code}
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {isNew ? "New requisition" : existing?.code}{" "}
            {existing ? <SourcingStatusBadge status={existing.headerStatus} /> : null}
          </h1>
          <p>{isNew ? "Raise demand for goods or services." : `Requested by ${existing?.requestor}`}</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Details</h3>
        </div>
        <div className="cbody">
          <div className="grid g2">
            {isNew ? (
              <div className="field">
                <label>Requestor</label>
                <input value={requestor} onChange={(e) => setRequestor(e.target.value)} />
              </div>
            ) : null}
            <div className="field">
              <label>Department</label>
              <input value={department} onChange={(e) => setDepartment(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Location</label>
              <input value={location} onChange={(e) => setLocation(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Category</label>
              <input value={category} onChange={(e) => setCategory(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Job</label>
              <input value={job} onChange={(e) => setJob(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Cost centre</label>
              <input value={costCentre} onChange={(e) => setCostCentre(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Project</label>
              <input value={project} onChange={(e) => setProject(e.target.value)} disabled={!editable} />
            </div>
            <div className="field">
              <label>Required on</label>
              <input type="date" value={requiredOn} onChange={(e) => setRequiredOn(e.target.value)} disabled={!editable} />
            </div>
            {isNew ? (
              <div className="field">
                <label>Currency</label>
                <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
                  <option value="MYR">MYR</option>
                  <option value="USD">USD</option>
                  <option value="SGD">SGD</option>
                </select>
              </div>
            ) : null}
          </div>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>Memo</label>
            <textarea rows={2} value={memo} onChange={(e) => setMemo(e.target.value)} disabled={!editable} />
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Lines</h3>
          {isNew ? (
            <>
              <div className="spacer" style={{ flex: 1 }} />
              <button type="button" className="btn btn-out btn-sm" onClick={addLine}>
                <Icon name="plus" size={14} /> Add line
              </button>
            </>
          ) : null}
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item code</th>
                <th>Description</th>
                <th className="amt">Qty</th>
                <th>UoM</th>
                <th className="amt">Est. unit price</th>
                {!isNew ? (
                  <>
                    <th>Status</th>
                    <th />
                  </>
                ) : (
                  <th />
                )}
              </tr>
            </thead>
            <tbody>
              {isNew
                ? lines.map((l) => (
                    <tr key={l.key}>
                      <td>
                        <input value={l.itemCode} onChange={(e) => updateLine(l.key, { itemCode: e.target.value })} />
                      </td>
                      <td>
                        <input
                          value={l.description}
                          onChange={(e) => updateLine(l.key, { description: e.target.value })}
                        />
                      </td>
                      <td className="amt">
                        <input
                          type="number"
                          value={l.qty}
                          style={{ width: 80 }}
                          onChange={(e) => updateLine(l.key, { qty: Number(e.target.value) })}
                        />
                      </td>
                      <td>
                        <input
                          value={l.uom}
                          style={{ width: 70 }}
                          onChange={(e) => updateLine(l.key, { uom: e.target.value })}
                        />
                      </td>
                      <td className="amt">
                        <input
                          type="number"
                          value={l.estUnitPrice}
                          style={{ width: 100 }}
                          onChange={(e) => updateLine(l.key, { estUnitPrice: Number(e.target.value) })}
                        />
                      </td>
                      <td className="amt">
                        <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(l.key)}>
                          <Icon name="x" size={13} />
                        </button>
                      </td>
                    </tr>
                  ))
                : existing?.lines.map((l) => (
                    <tr key={l.id}>
                      <td>{l.itemCode}</td>
                      <td>{l.description}</td>
                      <td className="amt">{l.qty}</td>
                      <td>{l.uom}</td>
                      <td className="amt">{l.estUnitPrice.toFixed(2)}</td>
                      <td>
                        <SourcingStatusBadge status={l.lifecycleStatus} />
                      </td>
                      <td className="amt">
                        {l.lifecycleStatus === "Open" ? (
                          <div className="rowactions">
                            <button
                              type="button"
                              className="btn btn-ghost btn-sm"
                              onClick={() => setLineAction({ lineId: l.id, action: "cancel" })}
                            >
                              Cancel
                            </button>
                            <button
                              type="button"
                              className="btn btn-ghost btn-sm"
                              onClick={() => setLineAction({ lineId: l.id, action: "release" })}
                            >
                              Release
                            </button>
                          </div>
                        ) : l.lifecycleStatus === "Cancelled" ? (
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            onClick={() => reopenLine.mutate(l.id)}
                          >
                            Reopen
                          </button>
                        ) : l.lifecycleStatus === "InDraftRfq" ? (
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            title="Release this line from the RFQ draft basket back to Open"
                            onClick={() => unreserveLine.mutate(l.id)}
                          >
                            Unreserve
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
              {(isNew ? lines.length === 0 : existing?.lines.length === 0) ? (
                <tr>
                  <td colSpan={7} className="hint" style={{ textAlign: "center", padding: 20 }}>
                    No lines yet.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </div>

      <div className="actionbar" style={{ marginTop: 14 }}>
        {isNew ? (
          <>
            <span className="hint">Save as draft or submit immediately.</span>
            <div className="spacer" style={{ flex: 1 }} />
            <button
              type="button"
              className="btn btn-out"
              disabled={create.isPending || !requestor || lines.length === 0}
              onClick={() => create.mutate(false)}
            >
              Save draft
            </button>
            <button
              type="button"
              className="btn btn-pri"
              disabled={create.isPending || !requestor || lines.length === 0}
              onClick={() => create.mutate(true)}
            >
              Submit PR
            </button>
          </>
        ) : existing && !existing.submitted ? (
          <>
            <span className="hint">Save changes or submit for sourcing.</span>
            <div className="spacer" style={{ flex: 1 }} />
            <button
              type="button"
              className="btn btn-out"
              style={{ color: "var(--red)" }}
              onClick={() => setCancelling(true)}
            >
              Cancel PR
            </button>
            <button type="button" className="btn btn-out" disabled={update.isPending} onClick={() => update.mutate()}>
              Save changes
            </button>
            <button type="button" className="btn btn-pri" disabled={submit.isPending} onClick={() => submit.mutate()}>
              Submit PR
            </button>
          </>
        ) : existing ? (
          <>
            <span className="hint">
              {existing.headerStatus === "Cancelled" ? "This requisition is cancelled." : "Save header changes."}
            </span>
            <div className="spacer" style={{ flex: 1 }} />
            {existing.headerStatus !== "Cancelled" ? (
              <button
                type="button"
                className="btn btn-out"
                style={{ color: "var(--red)" }}
                disabled={hasLiveLine}
                title={hasLiveLine ? "Cannot cancel while a line is in an RFQ or awarded." : undefined}
                onClick={() => setCancelling(true)}
              >
                Cancel PR
              </button>
            ) : null}
            {existing.headerStatus !== "Cancelled" ? (
              <button type="button" className="btn btn-pri" disabled={update.isPending} onClick={() => update.mutate()}>
                Save changes
              </button>
            ) : null}
          </>
        ) : null}
      </div>

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
    </>
  );
}

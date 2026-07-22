import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { approveAward, getAward, getAwardEligibility, getRfq, submitAward, type AwardAllocationDto } from "@/api/sourcing";
import { createPurchaseOrdersFromAward } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type Cell = { checked: boolean; qty: number; unitPrice: number };

export function AwardWorkspacePage({ rfqId, onBack }: { rfqId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [approving, setApproving] = useState(false);
  const [creatingPos, setCreatingPos] = useState(false);
  const [cells, setCells] = useState<Record<string, Cell>>({});

  const { data: rfq, isPending: rfqPending } = useQuery({ queryKey: ["rfq", rfqId], queryFn: () => getRfq(rfqId) });
  const { data: eligibility, isPending: eligPending } = useQuery({
    queryKey: ["award-eligibility", rfqId],
    queryFn: () => getAwardEligibility(rfqId),
  });
  const { data: award } = useQuery({ queryKey: ["award", rfqId], queryFn: () => getAward(rfqId) });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["award", rfqId] });
    void qc.invalidateQueries({ queryKey: ["award-eligibility", rfqId] });
  };
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const eligibleVendors = useMemo(() => (eligibility ?? []).filter((v) => v.eligible), [eligibility]);
  const sealed = (eligibility ?? []).some((v) => v.masked);

  const cellKey = (lineCode: string, vendorId: string) => `${lineCode}|${vendorId}`;

  const submit = useMutation({
    mutationFn: () => {
      const allocations: AwardAllocationDto[] = Object.entries(cells)
        .filter(([, c]) => c.checked && c.qty > 0)
        .map(([key, c]) => {
          const [lineCode, vendorId] = key.split("|") as [string, string];
          return { rfqLineCode: lineCode, vendorId, qty: c.qty, unitPrice: c.unitPrice };
        });
      return submitAward(rfqId, allocations);
    },
    onSuccess: refresh,
    onError: onErr,
  });

  const approve = useMutation({
    mutationFn: () => approveAward(rfqId),
    onSuccess: () => {
      setApproving(false);
      refresh();
    },
    onError: (e: Error) => {
      setApproving(false);
      onErr(e);
    },
  });

  const createPos = useMutation({
    mutationFn: () => createPurchaseOrdersFromAward(rfqId),
    onSuccess: () => {
      setCreatingPos(false);
      void navigate("/pos");
    },
    onError: (e: Error) => {
      setCreatingPos(false);
      onErr(e);
    },
  });

  if (rfqPending || eligPending || !rfq) return <Spinner label="Loading award workspace…" />;

  if (sealed) {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={onBack}>
            Awards
          </button>{" "}
          <Icon name="chev" size={12} /> {rfq.code}
        </div>
        <Notice tone="warn" icon="lock">
          Commercial envelope is sealed. Open it under Bid Openings before awarding.
        </Notice>
      </>
    );
  }

  const isPendingApproval = award?.status === "PendingApproval";
  const isApproved = award?.status === "Approved";
  const canApprove = isPendingApproval && award!.createdByUserId !== user?.id;
  const total = Object.values(cells).reduce((s, c) => (c.checked ? s + c.qty * c.unitPrice : s), 0);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Awards
        </button>{" "}
        <Icon name="chev" size={12} /> {rfq.code}
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {rfq.code} {award ? <SourcingStatusBadge status={award.status} /> : null}
          </h1>
          <p>{rfq.title}</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}
      {isPendingApproval ? (
        <Notice tone="warn" icon="flag">
          Pending approval — a different Approver-permissioned user must approve this award.
        </Notice>
      ) : null}
      {isApproved ? (
        <Notice tone="success" icon="check">
          Approved on {award?.approvedUtc ? new Date(award.approvedUtc).toLocaleString("en-MY") : "—"}.
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Allocation</h3>
          <span className="sub">· lowest eligible price highlighted per line</span>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Line</th>
                {eligibleVendors.map((v) => (
                  <th key={v.vendorId} className="amt">
                    {v.masked ? v.alias : v.vendorName}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rfq.lines.map((l) => {
                const linePrices = eligibleVendors.map((v) => cells[cellKey(l.lineCode, v.vendorId)]?.unitPrice ?? Infinity);
                const min = Math.min(...linePrices.filter((p) => p > 0));
                return (
                  <tr key={l.lineCode}>
                    <td>
                      {l.lineCode} · {l.itemCode}
                      <div className="hint">
                        {l.qty} {l.uom} required
                      </div>
                    </td>
                    {eligibleVendors.map((v) => {
                      const key = cellKey(l.lineCode, v.vendorId);
                      const cell = cells[key] ?? { checked: false, qty: 0, unitPrice: 0 };
                      const isLowest = cell.unitPrice > 0 && cell.unitPrice === min;
                      return (
                        <td key={v.vendorId} className="amt" style={isLowest ? { background: "var(--soft)" } : undefined}>
                          <label style={{ display: "flex", alignItems: "center", gap: 4, justifyContent: "flex-end" }}>
                            <input
                              type="checkbox"
                              disabled={isPendingApproval || isApproved}
                              checked={cell.checked}
                              onChange={(e) =>
                                setCells((xs) => ({ ...xs, [key]: { ...cell, checked: e.target.checked, qty: e.target.checked ? l.qty : 0 } }))
                              }
                            />
                          </label>
                          {cell.checked ? (
                            <div style={{ display: "flex", gap: 4, justifyContent: "flex-end", marginTop: 4 }}>
                              <input
                                type="number"
                                style={{ width: 60 }}
                                value={cell.qty}
                                max={l.qty}
                                disabled={isPendingApproval || isApproved}
                                onChange={(e) => setCells((xs) => ({ ...xs, [key]: { ...cell, qty: Math.min(Number(e.target.value), l.qty) } }))}
                              />
                              <input
                                type="number"
                                style={{ width: 70 }}
                                value={cell.unitPrice}
                                disabled={isPendingApproval || isApproved}
                                onChange={(e) => setCells((xs) => ({ ...xs, [key]: { ...cell, unitPrice: Number(e.target.value) } }))}
                              />
                            </div>
                          ) : null}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
          <p className="hint" style={{ marginTop: 8 }}>
            Allocated quantity/price is validated server-side against each vendor's actual submitted bid.
          </p>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Summary</h3>
        </div>
        <div className="cbody">
          <p style={{ fontSize: 16, fontWeight: 700, margin: 0 }}>Total: {rfq.currency} {fmt(total)}</p>
        </div>
      </div>

      <div className="actionbar" style={{ marginTop: 14 }}>
        <div className="spacer" style={{ flex: 1 }} />
        {!isApproved ? (
          <button type="button" className="btn btn-out" disabled={submit.isPending} onClick={() => submit.mutate()}>
            {award ? "Resubmit for approval" : "Submit for approval"}
          </button>
        ) : null}
        {isPendingApproval ? (
          <Gated permission={FshPermissions.award.approve}>
            <button
              type="button"
              className="btn btn-pri"
              disabled={!canApprove}
              title={!canApprove ? "A different Approver-permissioned user must approve (segregation of duties)." : undefined}
              onClick={() => setApproving(true)}
            >
              Approve &amp; generate PO
            </button>
          </Gated>
        ) : null}
        {isApproved ? (
          <Gated permission={FshPermissions.purchaseOrders.createFromAward}>
            <button type="button" className="btn btn-pri" disabled={createPos.isPending} onClick={() => setCreatingPos(true)}>
              Create purchase orders
            </button>
          </Gated>
        ) : null}
      </div>

      {approving ? (
        <ConfirmModal
          title="Approve award"
          icon="check"
          body="This approves the award. Purchase orders can then be generated in Procurement."
          confirmLabel="Approve"
          busy={approve.isPending}
          onCancel={() => setApproving(false)}
          onConfirm={() => approve.mutate()}
        />
      ) : null}
      {creatingPos ? (
        <ConfirmModal
          title="Create purchase orders"
          icon="box"
          body="Generate purchase orders from this approved award and open the PO list."
          confirmLabel="Create POs"
          busy={createPos.isPending}
          onCancel={() => setCreatingPos(false)}
          onConfirm={() => createPos.mutate()}
        />
      ) : null}
    </>
  );
}

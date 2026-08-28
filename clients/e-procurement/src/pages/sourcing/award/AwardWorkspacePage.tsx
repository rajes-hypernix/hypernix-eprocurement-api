import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import {
  approveAward,
  getAward,
  getAwardEligibility,
  submitAward,
  type AwardAllocationDto,
  type AwardCompareLineDto,
  type AwardQaItemDto,
  type AwardResponseDto,
} from "@/api/sourcing";
import { createPurchaseOrdersFromAward, listPurchaseOrders } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { ConfirmModal, Modal, Notice, Spinner } from "@/components/ui";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

const cellKey = (lineCode: string, vendorId: string) => `${lineCode}::${vendorId}`;

export function AwardWorkspacePage({ rfqId, onBack }: { rfqId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [approving, setApproving] = useState(false);
  const [generatedPos, setGeneratedPos] = useState<string[] | null>(null);
  /** Allocated qty per line::vendor — 0 / missing = not selected. */
  const [alloc, setAlloc] = useState<Record<string, number>>({});
  const [seeded, setSeeded] = useState(false);

  const { data: elig, isPending: eligPending } = useQuery({
    queryKey: ["award-eligibility", rfqId],
    queryFn: () => getAwardEligibility(rfqId),
  });
  const { data: award } = useQuery({ queryKey: ["award", rfqId], queryFn: () => getAward(rfqId) });
  const { data: pos } = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["award", rfqId] });
    void qc.invalidateQueries({ queryKey: ["award-eligibility", rfqId] });
    void qc.invalidateQueries({ queryKey: ["awards"] });
    void qc.invalidateQueries({ queryKey: ["rfqs"] });
    void qc.invalidateQueries({ queryKey: ["purchase-orders"] });
  };
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  // Seed recommended allocation once commercial is revealed.
  useEffect(() => {
    if (!elig?.commercialRevealed || seeded) return;
    const next: Record<string, number> = {};
    for (const line of elig.lines) {
      if (!line.recommendedVendorId) continue;
      const opt = line.options.find((o) => o.vendorId === line.recommendedVendorId);
      if (!opt) continue;
      next[cellKey(line.lineCode, line.recommendedVendorId)] = Math.min(line.requiredQty, opt.offeredQty);
    }
    setAlloc(next);
    setSeeded(true);
  }, [elig, seeded]);

  const cols = elig?.ranking ?? [];
  const currency = elig?.currency ?? "MYR";

  const optFor = (lineCode: string, vendorId: string) =>
    elig?.lines.find((l) => l.lineCode === lineCode)?.options.find((o) => o.vendorId === vendorId);

  const lineAllocated = (lineCode: string) =>
    cols.reduce((s, c) => s + (alloc[cellKey(lineCode, c.vendorId)] ?? 0), 0);

  const setQty = (key: string, qty: number) => setAlloc((a) => ({ ...a, [key]: Math.max(0, qty) }));

  const toggleCell = (line: AwardCompareLineDto, vendorId: string, on: boolean) => {
    const key = cellKey(line.lineCode, vendorId);
    if (!on) {
      setAlloc((a) => {
        const next = { ...a };
        delete next[key];
        return next;
      });
      return;
    }
    const offered = optFor(line.lineCode, vendorId)?.offeredQty ?? line.requiredQty;
    const remaining = line.requiredQty - lineAllocated(line.lineCode);
    setAlloc((a) => ({
      ...a,
      [key]: Math.max(1, Math.min(offered, remaining > 0 ? remaining : line.requiredQty)),
    }));
  };

  const summary = useMemo(() => {
    const byV = new Map<string, { name: string; total: number }>();
    for (const [key, qty] of Object.entries(alloc)) {
      if (!qty) continue;
      const [lineCode, vendorId] = key.split("::") as [string, string];
      const o = optFor(lineCode, vendorId);
      if (!o) continue;
      const cur = byV.get(vendorId) ?? { name: o.vendorName, total: 0 };
      cur.total += o.unitPrice * qty;
      byV.set(vendorId, cur);
    }
    const rows = [...byV.values()];
    return { rows, grand: rows.reduce((s, r) => s + r.total, 0) };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [alloc, elig]);

  const submit = useMutation({
    mutationFn: () => {
      const allocations: AwardAllocationDto[] = [];
      for (const [key, qty] of Object.entries(alloc)) {
        if (!qty) continue;
        const [lineCode, vendorId] = key.split("::") as [string, string];
        const o = optFor(lineCode, vendorId);
        if (!o) continue;
        allocations.push({ rfqLineCode: lineCode, vendorId, qty, unitPrice: o.unitPrice });
      }
      return submitAward(rfqId, allocations);
    },
    onSuccess: refresh,
    onError: onErr,
  });

  const approve = useMutation({
    mutationFn: () => approveAward(rfqId),
    onSuccess: async () => {
      setApproving(false);
      refresh();
      try {
        const codes = await createPurchaseOrdersFromAward(rfqId);
        setGeneratedPos(codes);
        void qc.invalidateQueries({ queryKey: ["purchase-orders"] });
      } catch (e) {
        onErr(e instanceof Error ? e : new Error("Could not generate purchase orders."));
      }
    },
    onError: (e: Error) => {
      setApproving(false);
      onErr(e);
    },
  });

  const createPos = useMutation({
    mutationFn: () => createPurchaseOrdersFromAward(rfqId),
    onSuccess: (codes) => {
      setGeneratedPos(codes);
      refresh();
    },
    onError: onErr,
  });

  if (eligPending || !elig) return <Spinner label="Loading award workspace…" />;

  if (!elig.commercialRevealed) {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={onBack}>
            Awards
          </button>{" "}
          <Icon name="chev" size={12} /> {elig.code}
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
  const locked = isPendingApproval || isApproved;
  const awardPos = (pos ?? []).filter((p) => p.awardId && award && p.awardId === award.id);
  const vendorName = (vendorId: string) =>
    elig.ranking.find((r) => r.vendorId === vendorId)?.vendorName
    ?? elig.lines.flatMap((l) => l.options).find((o) => o.vendorId === vendorId)?.vendorName
    ?? vendorId;

  const header = (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Awards &amp; POs
        </button>{" "}
        <Icon name="chev" size={12} /> {elig.code}
      </div>
      <div className="pagehead">
        <div>
          <h1>Award — {elig.code}</h1>
          <p>
            {elig.title} · {elig.envelope} envelope
          </p>
        </div>
      </div>
    </>
  );

  if (isApproved && award) {
    return (
      <>
        {header}
        <Notice tone="success" icon="check">
          {award.code} approved
          {award.approverUserId ? ` by ${award.approverUserId}` : ""}. {awardPos.length} PO(s) generated.
        </Notice>
        <div className="card">
          <div className="chead">
            <h3>Award outcome</h3>
            <span className="badge b-green">Approved</span>
          </div>
          <table>
            <thead>
              <tr>
                <th>Line</th>
                <th>Vendor</th>
                <th className="amt">Qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Value</th>
              </tr>
            </thead>
            <tbody>
              {award.allocations.map((al, i) => (
                <tr key={`${al.rfqLineCode}-${al.vendorId}-${i}`}>
                  <td>{al.rfqLineCode}</td>
                  <td>{vendorName(al.vendorId)}</td>
                  <td className="amt">{fmt(al.qty)}</td>
                  <td className="amt">RM {fmt(al.unitPrice)}</td>
                  <td className="amt">RM {fmt(al.qty * al.unitPrice)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="cbody" style={{ display: "flex", justifyContent: "space-between", fontWeight: 700, borderTop: "2px solid #333" }}>
            <span>
              Total award value
              {awardPos.length > 0 ? ` · PO ${awardPos.map((p) => p.code).join(", ")}` : ""}
            </span>
            <span>RM {fmt(award.totalValue)}</span>
          </div>
        </div>
        {awardPos.length === 0 ? (
          <div className="actionbar" style={{ marginTop: 14 }}>
            <div className="spacer" style={{ flex: 1 }} />
            <Gated permission={FshPermissions.purchaseOrders.createFromAward}>
              <button type="button" className="btn btn-pri" disabled={createPos.isPending} onClick={() => createPos.mutate()}>
                Create purchase orders
              </button>
            </Gated>
          </div>
        ) : (
          <div className="actionbar" style={{ marginTop: 14 }}>
            <div className="spacer" style={{ flex: 1 }} />
            <button type="button" className="btn btn-out" onClick={() => void navigate("/pos")}>
              Open purchase orders
            </button>
          </div>
        )}
        {generatedPos ? (
          <Modal
            title="Award confirmed"
            icon="check"
            footer={
              <button type="button" className="btn btn-pri" onClick={() => { setGeneratedPos(null); onBack(); }}>
                View awards
              </button>
            }
          >
            <div className="health health-ok" style={{ marginBottom: 14 }}>
              <span className="dot" /> {(awardPos.length || generatedPos.length)} purchase order(s) generated as Draft — verify and issue each PO.
            </div>
            {(awardPos.length > 0 ? awardPos.map((p) => p.code) : generatedPos).map((code) => (
              <div className="file" key={code} style={{ justifyContent: "space-between" }}>
                <div style={{ fontWeight: 700 }}>{code}</div>
                <span className="badge b-grey">Draft</span>
              </div>
            ))}
          </Modal>
        ) : null}
      </>
    );
  }

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Awards &amp; POs
        </button>{" "}
        <Icon name="chev" size={12} /> {elig.code}
      </div>
      <div className="pagehead">
        <div>
          <h1>Award — {elig.code}</h1>
          <p>
            {elig.title} · {elig.envelope} envelope
          </p>
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

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>Award comparison</h3>
          <div className="spacer" />
          <span className="badge b-teal">Eligible vendors only · split allowed</span>
        </div>
        <div className="cbody" style={{ paddingBottom: 0 }}>
          <p className="hint" style={{ marginTop: 0 }}>
            Recommended (lowest) per line is highlighted. Tick vendor(s) to award each line — split quantity across vendors by ticking more than one.
          </p>
        </div>
        <div className="comp-wrap">
          <table className="comp">
            <thead>
              <tr>
                <th style={{ minWidth: 220 }}>Line item</th>
                {cols.map((c) => (
                  <th key={c.vendorId} className="vcol" style={{ textAlign: "center" }}>
                    {c.vendorName}
                    {c.recommended ? (
                      <div className="hint" style={{ fontWeight: 500 }}>
                        recommended
                      </div>
                    ) : null}
                  </th>
                ))}
                <th className="amt">Allocated</th>
              </tr>
            </thead>
            <tbody>
              {elig.lines.map((l) => {
                const alloced = lineAllocated(l.lineCode);
                const over = alloced > l.requiredQty;
                return (
                  <tr key={l.lineCode}>
                    <td>
                      <div style={{ fontWeight: 600 }}>{l.description}</div>
                      <div className="hint">
                        {l.lineCode} · {l.itemCode} · need {l.requiredQty} {l.uom}
                      </div>
                    </td>
                    {cols.map((c) => {
                      const opt = optFor(l.lineCode, c.vendorId);
                      const key = cellKey(l.lineCode, c.vendorId);
                      const qty = alloc[key] ?? 0;
                      const isRec = l.recommendedVendorId === c.vendorId;
                      if (!opt) {
                        return (
                          <td key={c.vendorId} className="vcol" style={{ textAlign: "center" }}>
                            <span className="hint">No bid</span>
                          </td>
                        );
                      }
                      return (
                        <td
                          key={c.vendorId}
                          className="vcol"
                          style={{ textAlign: "center", background: isRec ? "var(--green-bg)" : undefined }}
                        >
                          <label className="ck" style={{ justifyContent: "center", gap: 6 }}>
                            <input
                              type="checkbox"
                              style={{ width: "auto" }}
                              checked={qty > 0}
                              disabled={locked}
                              onChange={(e) => toggleCell(l, c.vendorId, e.target.checked)}
                              aria-label={`Award ${l.lineCode} to ${c.vendorName}`}
                            />
                            <span style={{ fontWeight: 700 }}>
                              {currency} {fmt(opt.unitPrice)}
                            </span>
                          </label>
                          {qty > 0 ? (
                            <input
                              type="number"
                              min={0}
                              max={Math.min(opt.offeredQty, l.requiredQty)}
                              value={qty}
                              disabled={locked}
                              style={{ width: 76, textAlign: "right", marginTop: 4 }}
                              onChange={(e) => setQty(key, Number(e.target.value))}
                              aria-label={`Qty ${l.lineCode} ${c.vendorName}`}
                            />
                          ) : null}
                        </td>
                      );
                    })}
                    <td
                      className="amt"
                      style={{
                        fontWeight: 700,
                        color: over ? "var(--red)" : alloced === l.requiredQty ? "#2f9e6e" : "var(--muted)",
                      }}
                    >
                      {alloced}/{l.requiredQty}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>Award summary</h3>
        </div>
        <div className="cbody">
          {summary.rows.map((r) => (
            <div className="file" key={r.name} style={{ justifyContent: "space-between" }}>
              <div style={{ fontWeight: 600 }}>{r.name}</div>
              <div style={{ fontWeight: 700, color: "var(--teal)" }}>
                {currency} {fmt(r.total)}
              </div>
            </div>
          ))}
          {summary.rows.length === 0 ? <p className="hint" style={{ margin: 0 }}>No lines allocated yet.</p> : null}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              paddingTop: 12,
              marginTop: 6,
              borderTop: "2px solid #333",
              fontWeight: 700,
              fontSize: 15,
            }}
          >
            <span>Total award value</span>
            <span>
              {currency} {fmt(summary.grand)}
            </span>
          </div>
          <p className="hint" style={{ marginBottom: 0 }}>
            {summary.rows.length} purchase order{summary.rows.length !== 1 ? "s" : ""} will be created (one per awarded vendor) once approved.
          </p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>Combined evaluation</h3>
          <div className="spacer" />
          <span className="hint">{elig.envelope === "Dual" ? "technical 70% · commercial 30%" : "lowest price"}</span>
        </div>
        <table>
          <thead>
            <tr>
              <th style={{ width: 50 }}>Rank</th>
              <th>Vendor</th>
              {elig.envelope === "Dual" ? <th className="amt">Technical</th> : null}
              <th className="amt">Price score</th>
              <th className="amt">Combined</th>
            </tr>
          </thead>
          <tbody>
            {elig.ranking.map((r, i) => (
              <tr key={r.vendorId}>
                <td style={{ textAlign: "center", fontWeight: 700 }}>{i + 1}</td>
                <td style={{ fontWeight: 600 }}>
                  {r.vendorName}
                  {r.recommended ? (
                    <span className="badge b-green" style={{ marginLeft: 8 }}>
                      Recommended
                    </span>
                  ) : null}
                </td>
                {elig.envelope === "Dual" ? <td className="amt">{r.technicalScore ?? "—"}</td> : null}
                <td className="amt">{r.priceScore}</td>
                <td className="amt" style={{ fontWeight: 800, color: "var(--teal)" }}>
                  {r.combined}
                </td>
              </tr>
            ))}
            {elig.ranking.length === 0 ? (
              <tr>
                <td colSpan={elig.envelope === "Dual" ? 5 : 4}>
                  <p className="hint" style={{ margin: 0 }}>
                    No eligible vendors yet.
                  </p>
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      <ResponsesCard title="Technical responses" questions={elig.technicalQuestions} responses={elig.responses} />
      <ResponsesCard title="Commercial responses" questions={elig.commercialQuestions} responses={elig.responses} />

      <div className="actionbar" style={{ marginTop: 14 }}>
        <div className="spacer" style={{ flex: 1 }} />
        <button type="button" className="btn btn-pri" disabled={submit.isPending || locked} onClick={() => submit.mutate()}>
          <Icon name="award" size={15} /> {award ? "Resubmit for approval" : "Submit for approval"}
        </button>
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
      </div>

      {approving ? (
        <ConfirmModal
          title="Approve award"
          icon="check"
          body="This approves the award and generates one draft purchase order per awarded vendor."
          confirmLabel="Approve"
          busy={approve.isPending}
          onCancel={() => setApproving(false)}
          onConfirm={() => approve.mutate()}
        />
      ) : null}
    </>
  );
}

function ResponsesCard({
  title,
  questions,
  responses,
}: {
  title: string;
  questions: AwardQaItemDto[];
  responses: AwardResponseDto[];
}) {
  if (questions.length === 0 || responses.length === 0) return null;
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <div className="chead">
        <h3>{title}</h3>
        <div className="spacer" />
        <span className="hint">progressed vendors</span>
      </div>
      <div style={{ overflowX: "auto" }}>
        <table>
          <thead>
            <tr>
              <th style={{ minWidth: 230 }}>Question</th>
              {responses.map((r) => (
                <th key={r.vendorId} style={{ whiteSpace: "normal" }}>
                  {r.vendorName}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {questions.map((q) => (
              <tr key={q.order}>
                <td style={{ fontWeight: 600, whiteSpace: "normal", maxWidth: 260 }}>{q.label || "Untitled"}</td>
                {responses.map((r) => {
                  const a = r.answers.find((x) => x.questionOrder === q.order)?.value;
                  return (
                    <td key={r.vendorId} style={{ whiteSpace: "normal" }}>
                      {a || <span className="hint">—</span>}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

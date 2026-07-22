import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CustomListKeys, listCustomListItems } from "@/api/platform";
import {
  declineInvitation,
  getMyBid,
  getRfqForBidding,
  saveBidDraft,
  submitBid,
  withdrawBid,
  type BidAnswerDto,
  type BidLineDto,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { EnvelopeTag } from "@/components/sourcing/badges";
import { ApiRequestError } from "@/lib/api-client";

export function BidFormPage({ rfqId, onBack }: { rfqId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { data: rfq, isPending: rfqPending } = useQuery({
    queryKey: ["rfq-for-bidding", rfqId],
    queryFn: () => getRfqForBidding(rfqId),
  });
  const { data: bid, isPending: bidPending } = useQuery({
    queryKey: ["my-bid", rfqId],
    queryFn: () => getMyBid(rfqId),
  });

  const [lead, setLead] = useState("");
  const [warranty, setWarranty] = useState("");
  const [lines, setLines] = useState<Record<string, BidLineDto>>({});
  const [answers, setAnswers] = useState<Record<number, string>>({});
  const [err, setErr] = useState<string | null>(null);
  const [declining, setDeclining] = useState(false);
  const [reasonCode, setReasonCode] = useState("");
  const [note, setNote] = useState("");
  const [submitted, setSubmitted] = useState(false);

  const hydratedRef = useState({ done: false })[0];
  if (rfq && !hydratedRef.done) {
    hydratedRef.done = true;
    setLead(bid?.lead ?? "");
    setWarranty(bid?.warranty ?? "");
    const byItem = new Map((bid?.lines ?? []).map((l) => [l.itemCode, l]));
    const initLines: Record<string, BidLineDto> = {};
    for (const l of rfq.lines) {
      const existing = byItem.get(l.itemCode);
      initLines[l.itemCode] = existing ?? {
        itemCode: l.itemCode,
        bidding: false,
        price: 0,
        qty: l.qty,
        partial: false,
        altItem: null,
      };
    }
    setLines(initLines);
    const initAnswers: Record<number, string> = {};
    for (const a of bid?.answers ?? []) initAnswers[a.questionOrder] = a.value;
    setAnswers(initAnswers);
  }

  const { data: declineReasons } = useQuery({
    queryKey: ["custom-list", CustomListKeys.bidDecline],
    queryFn: () => listCustomListItems(CustomListKeys.bidDecline),
    enabled: declining,
  });

  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);
  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["my-bid", rfqId] });
    void qc.invalidateQueries({ queryKey: ["my-invitations"] });
  };

  const buildAnswers = (): BidAnswerDto[] =>
    Object.entries(answers)
      .filter(([, v]) => v.trim() !== "")
      .map(([order, value]) => ({ questionOrder: Number(order), value }));

  const save = useMutation({
    mutationFn: () =>
      saveBidDraft(rfqId, {
        lead: lead || null,
        warranty: warranty || null,
        lines: Object.values(lines),
        answers: buildAnswers(),
        files: [],
      }),
    onSuccess: refresh,
    onError: onErr,
  });

  const submit = useMutation({
    mutationFn: async () => {
      await saveBidDraft(rfqId, {
        lead: lead || null,
        warranty: warranty || null,
        lines: Object.values(lines),
        answers: buildAnswers(),
        files: [],
      });
      return submitBid(rfqId);
    },
    onSuccess: () => {
      setSubmitted(true);
      refresh();
    },
    onError: onErr,
  });

  const withdraw = useMutation({ mutationFn: () => withdrawBid(rfqId), onSuccess: refresh, onError: onErr });

  const decline = useMutation({
    mutationFn: () => declineInvitation(rfqId, reasonCode, note || null),
    onSuccess: () => {
      setDeclining(false);
      refresh();
    },
    onError: onErr,
  });

  if (rfqPending || bidPending || !rfq) return <Spinner label="Loading RFQ…" />;

  const isOpen = rfq.status === "Open";
  const isSubmitted = Boolean(bid?.submitted);
  const canBid = isOpen;
  const total = Object.values(lines).reduce((s, l) => s + (l.bidding ? l.price * l.qty : 0), 0);
  const hasPricedLine = Object.values(lines).some((l) => l.bidding && l.price > 0);
  const technicalItems = rfq.formItems.filter((f) => f.group === "Technical");
  const commercialItems = rfq.formItems.filter((f) => f.group === "Commercial");

  if (submitted) {
    return (
      <div className="card" style={{ maxWidth: 480, margin: "60px auto", textAlign: "center" }}>
        <div className="cbody">
          <Icon name="check" size={40} />
          <h2 style={{ marginTop: 12 }}>Bid submitted</h2>
          <p className="hint">Your bid for {rfq.code} has been submitted.</p>
          <button type="button" className="btn btn-pri" style={{ marginTop: 12 }} onClick={onBack}>
            Back to my RFQs
          </button>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          My RFQs
        </button>{" "}
        <Icon name="chev" size={12} /> {rfq.code}
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {rfq.code} <EnvelopeTag envelope={rfq.envelope} />
          </h1>
          <p>{rfq.title}</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}
      {!isOpen ? (
        <Notice tone="warn" icon="lock">
          This RFQ is {rfq.status.toLowerCase()} — bidding is closed.
        </Notice>
      ) : null}
      {isSubmitted && isOpen ? (
        <Notice tone="success" icon="check">
          Your bid was submitted. You can withdraw and re-edit while bidding is still open.
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Lines</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th />
                <th>Item</th>
                <th className="amt">Required qty</th>
                <th className="amt">Offered qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Line total</th>
                <th>Alt. item</th>
              </tr>
            </thead>
            <tbody>
              {rfq.lines.map((l) => {
                const bl = lines[l.itemCode];
                if (!bl) return null;
                const partial = bl.bidding && bl.qty < l.qty;
                return (
                  <tr key={l.lineCode}>
                    <td>
                      <input
                        type="checkbox"
                        checked={bl.bidding}
                        disabled={!canBid}
                        onChange={(e) => setLines((xs) => ({ ...xs, [l.itemCode]: { ...bl, bidding: e.target.checked } }))}
                      />
                    </td>
                    <td>
                      {l.itemCode} — {l.description}
                      {partial ? <span className="badge b-amber" style={{ marginLeft: 6 }}>Partial</span> : null}
                    </td>
                    <td className="amt">
                      {l.qty} {l.uom}
                    </td>
                    <td className="amt">
                      <input
                        type="number"
                        style={{ width: 90 }}
                        value={bl.qty}
                        max={l.qty}
                        disabled={!canBid || !bl.bidding}
                        onChange={(e) =>
                          setLines((xs) => ({
                            ...xs,
                            [l.itemCode]: { ...bl, qty: Math.min(Number(e.target.value), l.qty) },
                          }))
                        }
                      />
                    </td>
                    <td className="amt">
                      <input
                        type="number"
                        style={{ width: 100 }}
                        value={bl.price}
                        disabled={!canBid || !bl.bidding}
                        onChange={(e) => setLines((xs) => ({ ...xs, [l.itemCode]: { ...bl, price: Number(e.target.value) } }))}
                      />
                    </td>
                    <td className="amt">{(bl.bidding ? bl.price * bl.qty : 0).toFixed(2)}</td>
                    <td>
                      <input
                        value={bl.altItem ?? ""}
                        disabled={!canBid || !bl.bidding}
                        placeholder="substitute item"
                        onChange={(e) => setLines((xs) => ({ ...xs, [l.itemCode]: { ...bl, altItem: e.target.value || null } }))}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={5} className="amt" style={{ fontWeight: 700 }}>
                  Total
                </td>
                <td className="amt" style={{ fontWeight: 700 }}>
                  {rfq.currency} {total.toFixed(2)}
                </td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      <div className="grid g2" style={{ marginTop: 14 }}>
        <div className="field">
          <label>Delivery lead time</label>
          <input value={lead} disabled={!canBid} onChange={(e) => setLead(e.target.value)} placeholder="e.g. 4 weeks" />
        </div>
        <div className="field">
          <label>Warranty</label>
          <input value={warranty} disabled={!canBid} onChange={(e) => setWarranty(e.target.value)} placeholder="e.g. 12 months" />
        </div>
      </div>

      {(["Technical", "Commercial"] as const).map((group) => {
        const list = group === "Technical" ? technicalItems : commercialItems;
        if (list.length === 0) return null;
        return (
          <div className="card" key={group} style={{ marginTop: 14 }}>
            <div className="chead">
              <h3>{group} response</h3>
            </div>
            <div className="cbody">
              {list.map((f) => (
                <div className="field" key={f.order} style={{ borderLeft: f.required && !answers[f.order] ? "2px solid var(--red)" : undefined, paddingLeft: f.required ? 8 : 0 }}>
                  <label>
                    {f.label} {f.required ? <span className="hint">(required)</span> : null}
                  </label>
                  {f.type === "longtext" ? (
                    <textarea
                      rows={2}
                      disabled={!canBid}
                      value={answers[f.order] ?? ""}
                      onChange={(e) => setAnswers((xs) => ({ ...xs, [f.order]: e.target.value }))}
                    />
                  ) : (
                    <input
                      type={f.type === "number" ? "number" : f.type === "date" ? "date" : "text"}
                      disabled={!canBid}
                      value={answers[f.order] ?? ""}
                      onChange={(e) => setAnswers((xs) => ({ ...xs, [f.order]: e.target.value }))}
                    />
                  )}
                  {f.help ? <p className="hint" style={{ margin: "4px 0 0" }}>{f.help}</p> : null}
                </div>
              ))}
            </div>
          </div>
        );
      })}

      <div className="actionbar" style={{ marginTop: 14 }}>
        {canBid ? (
          <>
            <span className="hint">Enter a price on at least one line before submitting.</span>
            <div className="spacer" style={{ flex: 1 }} />
            {!isSubmitted ? (
              <button type="button" className="btn btn-out" onClick={() => setDeclining(true)}>
                Decline invitation
              </button>
            ) : null}
            <button type="button" className="btn btn-out" disabled={save.isPending} onClick={() => save.mutate()}>
              Save draft
            </button>
            {isSubmitted ? (
              <button type="button" className="btn btn-out" style={{ color: "var(--red)" }} disabled={withdraw.isPending} onClick={() => withdraw.mutate()}>
                Withdraw bid
              </button>
            ) : null}
            <button
              type="button"
              className="btn btn-pri"
              disabled={!hasPricedLine || submit.isPending}
              onClick={() => submit.mutate()}
            >
              {isSubmitted ? "Resubmit bid" : "Submit bid"}
            </button>
          </>
        ) : (
          <span className="hint">Bidding is closed for this RFQ.</span>
        )}
      </div>

      {declining ? (
        <ConfirmModal
          title="Decline invitation"
          icon="x"
          body={
            <>
              <div className="field">
                <label>Reason</label>
                <select value={reasonCode} onChange={(e) => setReasonCode(e.target.value)}>
                  <option value="">Select a reason…</option>
                  {declineReasons?.map((r) => (
                    <option key={r.code} value={r.code}>
                      {r.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Note (optional)</label>
                <textarea rows={2} value={note} onChange={(e) => setNote(e.target.value)} />
              </div>
            </>
          }
          confirmLabel="Decline"
          danger
          busy={decline.isPending}
          onCancel={() => setDeclining(false)}
          onConfirm={() => reasonCode && decline.mutate()}
        />
      ) : null}
    </>
  );
}

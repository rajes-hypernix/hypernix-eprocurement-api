import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/auth/use-auth";
import {
  TECHNICAL_CRITERIA_META,
  TECHNICAL_PASS_THRESHOLD,
  finalizeTechnical,
  getBidOpening,
  getTechnicalEval,
  setTechnicalScore,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";

export function TechnicalScoringPage({
  rfqId,
  onBack,
  onOpenCommercial,
}: {
  rfqId: string;
  onBack: () => void;
  onOpenCommercial: () => void;
}) {
  const qc = useQueryClient();
  const { user } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [finalizing, setFinalizing] = useState(false);
  const [evaluator, setEvaluator] = useState("");

  const { data: status } = useQuery({ queryKey: ["bid-opening", rfqId], queryFn: () => getBidOpening(rfqId) });
  const { data: rows, isPending } = useQuery({
    queryKey: ["technical-eval", rfqId],
    queryFn: () => getTechnicalEval(rfqId),
  });

  const refresh = () => void qc.invalidateQueries({ queryKey: ["technical-eval", rfqId] });

  const score = useMutation({
    mutationFn: (vars: { vendorId: string; criterion: string; value: number }) =>
      setTechnicalScore(rfqId, vars.vendorId, vars.criterion, vars.value),
    onSuccess: refresh,
    onError: (e: Error) => setErr(e.message),
  });

  const finalize = useMutation({
    mutationFn: () => finalizeTechnical(rfqId),
    onSuccess: () => {
      setFinalizing(false);
      refresh();
      void qc.invalidateQueries({ queryKey: ["bid-opening", rfqId] });
    },
    onError: (e: Error) => {
      setFinalizing(false);
      setErr(e.message);
    },
  });

  if (isPending || !rows || !status) return <Spinner label="Loading scores…" />;

  const finalized = Boolean(status.techFinalized);
  const myId = user?.id ?? "";
  const evaluators = status.evaluators ?? [];
  const pic = evaluator || (evaluators.some((x) => x.id === myId) ? myId : evaluators[0]?.id) || myId;
  const canEdit = !finalized && pic === myId;
  const masked = rows.some((r) => r.masked);
  const failedCount = rows.filter((v) => v.pass === false && v.committeeScore != null).length;

  const displayName = (v: (typeof rows)[number]) => (v.masked ? v.alias : v.vendorName || v.alias);

  const cellScore = (vendorId: string, criterion: string) => {
    const v = rows.find((r) => r.vendorId === vendorId);
    return v?.scores.find((s) => s.evaluatorId === pic && s.criterion === criterion)?.score;
  };

  const weighted = (vendorId: string) => {
    let sum = 0;
    for (const c of TECHNICAL_CRITERIA_META) {
      const s = cellScore(vendorId, c.key);
      if (s == null) return null;
      sum += Number(s) * c.weight;
    }
    return Math.round((sum / 100) * 10) / 10;
  };

  const evaluatorDone = (id: string) =>
    rows.length > 0 &&
    rows.every((v) =>
      TECHNICAL_CRITERIA_META.every((c) => v.scores.some((s) => s.evaluatorId === id && s.criterion === c.key)),
    );

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Open bids
        </button>{" "}
        <Icon name="chev" size={13} /> <span>{status.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Technical Evaluation — {status.code}</h1>
          <p>
            {status.title} · sealed technical envelope
          </p>
        </div>
        <div className="spacer" />
        {!finalized ? (
          <button type="button" className="btn btn-pri" disabled={finalize.isPending} onClick={() => setFinalizing(true)}>
            <Icon name="check" size={15} /> Finalize technical
          </button>
        ) : (
          <span className="badge b-green">Technical finalized</span>
        )}
      </div>

      {masked ? (
        <div className="ribbon" style={{ marginBottom: 14, background: "#ecebf6", borderColor: "#d9d6ef", color: "#4a45a0" }}>
          <Icon name="eye" size={14} /> Evaluator view — vendor identities are masked (Bidder A, B, C…) until award.
        </div>
      ) : null}

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Scoring matrix</h3>
          <div className="spacer" />
          {!finalized && evaluators.length > 0 ? (
            <>
              <span className="hint" style={{ marginRight: 8 }}>
                Scoring as
              </span>
              <select
                aria-label="Scoring as"
                value={pic}
                onChange={(e) => setEvaluator(e.target.value)}
                style={{ width: "auto", minWidth: 180 }}
              >
                {evaluators.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                    {evaluatorDone(x.id) ? " ✓" : ""}
                  </option>
                ))}
              </select>
            </>
          ) : null}
        </div>
        <table>
          <thead>
            <tr>
              <th style={{ minWidth: 220 }}>Evaluation criterion</th>
              {rows.map((v) => (
                <th key={v.vendorId} className="amt">
                  {displayName(v)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {TECHNICAL_CRITERIA_META.map((c) => (
              <tr key={c.key}>
                <td>
                  <div style={{ fontWeight: 600 }}>{c.label}</div>
                  <div className="hint">Weight {c.weight}%</div>
                </td>
                {rows.map((v) => {
                  const val = cellScore(v.vendorId, c.key);
                  return (
                    <td key={v.vendorId} className="amt">
                      {!canEdit ? (
                        <strong>{val ?? "—"}</strong>
                      ) : (
                        <input
                          type="number"
                          min={0}
                          max={100}
                          style={{ width: 70, textAlign: "right" }}
                          defaultValue={val ?? ""}
                          aria-label={`${c.key} ${displayName(v)}`}
                          onBlur={(e) => {
                            if (e.target.value !== "") score.mutate({ vendorId: v.vendorId, criterion: c.key, value: Number(e.target.value) });
                          }}
                        />
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
            {!finalized ? (
              <tr style={{ background: "var(--soft)" }}>
                <td style={{ fontWeight: 700 }}>Your weighted score</td>
                {rows.map((v) => {
                  const w = weighted(v.vendorId);
                  return (
                    <td key={v.vendorId} className="amt" style={{ fontWeight: 700 }}>
                      {w == null ? <span className="hint">incomplete</span> : w.toFixed(1)}
                    </td>
                  );
                })}
              </tr>
            ) : null}
            <tr style={{ background: "var(--soft)" }}>
              <td style={{ fontWeight: 700 }}>
                Committee score <span className="hint">avg of {evaluators.length || 1} evaluator(s)</span>
              </td>
              {rows.map((v) => (
                <td key={v.vendorId} className="amt" style={{ fontWeight: 800 }}>
                  <span
                    style={{
                      color: v.committeeScore == null ? "var(--muted)" : v.pass === false ? "var(--red)" : "#2f9e6e",
                    }}
                  >
                    {v.committeeScore ?? "—"}
                  </span>
                  {finalized && v.committeeScore != null ? (
                    <div>
                      <span className={`badge ${v.pass ? "b-green" : "b-red"}`}>{v.pass ? "Pass" : "Fail"}</span>
                    </div>
                  ) : null}
                </td>
              ))}
            </tr>
          </tbody>
        </table>
      </div>

      {finalized ? (
        <div className="card" style={{ marginTop: 16 }}>
          <div className="cbody" style={{ display: "flex", alignItems: "center", gap: 14 }}>
            <div style={{ flex: 1 }}>
              <strong>Technical evaluation finalized.</strong>{" "}
              <span className="hint">
                {failedCount} vendor(s) did not pass — excluded from commercial. A commercial evaluator opens the
                commercial envelope next.
              </span>
            </div>
            <button type="button" className="btn btn-pri" onClick={onOpenCommercial}>
              <Icon name="unlock" size={15} /> Commercial opening
            </button>
          </div>
        </div>
      ) : (
        <p className="hint" style={{ marginTop: 10 }}>
          Commercial (price) envelopes release only for vendors scoring ≥ {TECHNICAL_PASS_THRESHOLD}. Pricing stays sealed
          until technical is finalized.
        </p>
      )}

      {finalizing ? (
        <ConfirmModal
          title="Finalize technical evaluation"
          icon="check"
          body="Vendors below the pass threshold are excluded from commercial (price) opening. This cannot be undone."
          confirmLabel="Finalize"
          busy={finalize.isPending}
          onCancel={() => setFinalizing(false)}
          onConfirm={() => finalize.mutate()}
        />
      ) : null}
    </>
  );
}

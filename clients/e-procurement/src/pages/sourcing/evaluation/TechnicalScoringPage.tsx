import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/auth/use-auth";
import {
  TECHNICAL_CRITERIA,
  finalizeTechnical,
  getBidOpening,
  getTechnicalEval,
  setTechnicalScore,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";

export function TechnicalScoringPage({ rfqId, onBack }: { rfqId: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { user } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [finalizing, setFinalizing] = useState(false);

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

  if (isPending || !rows) return <Spinner label="Loading scores…" />;

  const finalized = Boolean(status?.techFinalized);
  const myId = user?.id;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Bid opening
        </button>{" "}
        <Icon name="chev" size={12} /> Technical scoring
      </div>
      <div className="pagehead">
        <div>
          <h1>Technical scoring</h1>
          <p>Evaluator view — vendor identities are masked for pure evaluators until award.</p>
        </div>
        <div className="spacer" />
        {!finalized ? (
          <button type="button" className="btn btn-pri" disabled={finalize.isPending} onClick={() => setFinalizing(true)}>
            Finalize technical
          </button>
        ) : (
          <span className="badge b-green">Finalized</span>
        )}
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Bidder</th>
                {TECHNICAL_CRITERIA.map((c) => (
                  <th key={c} className="amt">
                    {c}
                  </th>
                ))}
                <th className="amt">My weighted</th>
                <th className="amt">Committee</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => {
                const myScores = r.scores.filter((s) => s.evaluatorId === myId);
                const myWeighted =
                  myScores.length === TECHNICAL_CRITERIA.length
                    ? myScores.reduce((sum, s) => {
                        const weight = { Compliance: 35, Experience: 20, Delivery: 20, QA: 25 }[s.criterion] ?? 0;
                        return sum + (s.score * weight) / 100;
                      }, 0)
                    : null;
                return (
                  <tr key={r.vendorId}>
                    <td style={{ fontWeight: 700 }}>{r.masked ? r.alias : `${r.vendorName} (${r.alias})`}</td>
                    {TECHNICAL_CRITERIA.map((c) => {
                      const mine = myScores.find((s) => s.criterion === c);
                      return (
                        <td key={c} className="amt">
                          <input
                            type="number"
                            min={0}
                            max={100}
                            style={{ width: 60 }}
                            disabled={finalized}
                            defaultValue={mine?.score ?? ""}
                            onBlur={(e) => {
                              const v = Number(e.target.value);
                              if (!Number.isNaN(v)) score.mutate({ vendorId: r.vendorId, criterion: c, value: v });
                            }}
                          />
                        </td>
                      );
                    })}
                    <td className="amt">{myWeighted !== null ? myWeighted.toFixed(1) : <span className="hint">incomplete</span>}</td>
                    <td className="amt" style={{ color: r.committeeScore != null ? (r.pass ? "var(--green)" : "var(--red)") : undefined }}>
                      {r.committeeScore != null ? r.committeeScore.toFixed(1) : "—"}
                    </td>
                    <td>
                      {r.committeeScore != null ? (
                        <span className={`badge ${r.pass ? "b-green" : "b-red"}`}>{r.pass ? "Pass" : "Fail"}</span>
                      ) : (
                        "—"
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

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

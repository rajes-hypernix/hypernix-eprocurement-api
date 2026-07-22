import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  createRfqDraft,
  getRequisition,
  listRequisitions,
  reserveRequisitionLine,
  type RfqLineInput,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

type PickedLine = RfqLineInput & { prId: string; prLineId: string };

/**
 * Simplified single-page RFQ draft creation — picks Open lines from submitted/partially-sourced
 * requisitions, reserves each (Open -> InDraftRfq) then creates the draft. The full multi-step
 * wizard (evaluators, question builder, vendor picker) happens on the detail page after creation.
 */
export function RfqBuilderPage({ onSaved, onBack }: { onSaved: (id: string) => void; onBack: () => void }) {
  const [title, setTitle] = useState("");
  const [envelope, setEnvelope] = useState<"Single" | "Dual">("Dual");
  const [currency, setCurrency] = useState("MYR");
  const [picked, setPicked] = useState<PickedLine[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [expandedPr, setExpandedPr] = useState<string | null>(null);

  const { data: reqs, isPending } = useQuery({ queryKey: ["requisitions"], queryFn: listRequisitions });
  const sourceable = useMemo(
    () => (reqs ?? []).filter((r) => r.headerStatus === "Submitted" || r.headerStatus === "PartiallySourced"),
    [reqs],
  );

  const { data: expandedDetail } = useQuery({
    queryKey: ["requisition", expandedPr],
    queryFn: () => getRequisition(expandedPr!),
    enabled: expandedPr !== null,
  });

  const toggleLine = (prId: string, line: { id: string; itemCode: string; description: string; qty: number; uom: string }) => {
    setPicked((xs) => {
      const exists = xs.find((l) => l.prLineId === line.id);
      if (exists) return xs.filter((l) => l.prLineId !== line.id);
      const lineCode = `L${xs.length + 1}`;
      return [
        ...xs,
        {
          prId,
          prLineId: line.id,
          lineCode,
          itemCode: line.itemCode,
          description: line.description,
          qty: line.qty,
          uom: line.uom,
          prRef: undefined,
          sourcePrLineIds: [line.id],
        },
      ];
    });
  };

  const build = useMutation({
    mutationFn: async () => {
      // Reserve each picked line first (Open -> InDraftRfq) so release-time lineage works.
      await Promise.all(picked.map((l) => reserveRequisitionLine(l.prId, l.prLineId)));
      const prRefs = [...new Set(picked.map((l) => l.prId))];
      return createRfqDraft({
        title: title || null,
        envelope,
        currency,
        prRefs,
        lines: picked.map(({ prId: _p, prLineId: _l, ...line }) => line),
      });
    },
    onSuccess: (id) => onSaved(id),
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          RFQs
        </button>{" "}
        <Icon name="chev" size={12} /> New
      </div>
      <div className="pagehead">
        <div>
          <h1>New RFQ</h1>
          <p>Pick lines from open requisitions, set the envelope type, then build out the rest on the draft page.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Settings</h3>
        </div>
        <div className="cbody">
          <div className="grid g3">
            <div className="field">
              <label>Title</label>
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Q3 pipe fittings" />
            </div>
            <div className="field">
              <label>Currency</label>
              <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
                <option value="MYR">MYR</option>
                <option value="USD">USD</option>
                <option value="SGD">SGD</option>
              </select>
            </div>
            <div className="field">
              <label>Envelope</label>
              <select value={envelope} onChange={(e) => setEnvelope(e.target.value as "Single" | "Dual")}>
                <option value="Dual">Dual (sealed technical + commercial)</option>
                <option value="Single">Single</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="grid g2" style={{ marginTop: 14, alignItems: "start" }}>
        <div className="card">
          <div className="chead">
            <h3>Open requisition lines</h3>
          </div>
          <div className="cbody">
            {isPending ? (
              <Spinner label="Loading…" />
            ) : sourceable.length === 0 ? (
              <p className="hint" style={{ margin: 0 }}>
                No submitted requisitions with open lines.
              </p>
            ) : (
              sourceable.map((r) => (
                <div key={r.id} style={{ marginBottom: 8 }}>
                  <div
                    className="pickrow"
                    style={{ cursor: "pointer" }}
                    onClick={() => setExpandedPr(expandedPr === r.id ? null : r.id)}
                  >
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 700, fontSize: 13 }}>{r.code}</div>
                      <div className="hint">
                        {r.requestor} · {r.department} · {r.lineCount} line(s)
                      </div>
                    </div>
                    <Icon name={expandedPr === r.id ? "chev" : "chev"} size={13} />
                  </div>
                  {expandedPr === r.id && expandedDetail ? (
                    <div style={{ paddingLeft: 12, marginTop: 6 }}>
                      {expandedDetail.lines
                        .filter((l) => l.lifecycleStatus === "Open")
                        .map((l) => (
                          <div className="pickrow" key={l.id}>
                            <input
                              type="checkbox"
                              checked={picked.some((p) => p.prLineId === l.id)}
                              onChange={() => toggleLine(r.id, l)}
                            />
                            <div style={{ flex: 1, marginLeft: 8 }}>
                              <div style={{ fontSize: 12.5 }}>
                                {l.itemCode} — {l.description}
                              </div>
                              <div className="hint">
                                {l.qty} {l.uom}
                              </div>
                            </div>
                          </div>
                        ))}
                      {expandedDetail.lines.filter((l) => l.lifecycleStatus === "Open").length === 0 ? (
                        <p className="hint" style={{ margin: 0 }}>
                          No open lines on this requisition.
                        </p>
                      ) : null}
                    </div>
                  ) : null}
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card">
          <div className="chead">
            <h3>RFQ basket</h3>
            <span className="sub">· {picked.length} line(s)</span>
          </div>
          <div className="cbody">
            {picked.length === 0 ? (
              <p className="hint" style={{ margin: 0 }}>
                Select lines on the left to add them here.
              </p>
            ) : (
              picked.map((l) => (
                <div className="pickrow" key={l.prLineId}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 600, fontSize: 12.5 }}>
                      {l.lineCode} · {l.itemCode}
                    </div>
                    <div className="hint">
                      {l.description} · {l.qty} {l.uom}
                    </div>
                  </div>
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={() => setPicked((xs) => xs.filter((p) => p.prLineId !== l.prLineId))}
                  >
                    <Icon name="x" size={13} />
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      <div className="actionbar" style={{ marginTop: 14 }}>
        <span className="hint">Vendors, questions, and evaluators are set up on the draft page next.</span>
        <div className="spacer" style={{ flex: 1 }} />
        <button
          type="button"
          className="btn btn-pri"
          disabled={picked.length === 0 || build.isPending}
          onClick={() => build.mutate()}
        >
          Build RFQ draft <Icon name="chev" size={14} />
        </button>
      </div>
    </>
  );
}

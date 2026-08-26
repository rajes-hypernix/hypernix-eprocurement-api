import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getBidOpening, openCommercialEnvelope, openTechnicalEnvelope } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

export function BidOpeningDetailPage({
  rfqId,
  onBack,
  onScore,
  onAward,
}: {
  rfqId: string;
  onBack: () => void;
  onScore: () => void;
  onAward: () => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [authorise, setAuthorise] = useState(false);

  const { data: o, isPending } = useQuery({ queryKey: ["bid-opening", rfqId], queryFn: () => getBidOpening(rfqId) });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["bid-opening", rfqId] });
    void qc.invalidateQueries({ queryKey: ["rfqs"] });
  };
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const openT = useMutation({
    mutationFn: () => openTechnicalEnvelope(rfqId),
    onSuccess: () => {
      setAuthorise(false);
      refresh();
      onScore();
    },
    onError: (e: Error) => {
      setAuthorise(false);
      onErr(e);
    },
  });

  const openC = useMutation({
    mutationFn: () => openCommercialEnvelope(rfqId),
    onSuccess: refresh,
    onError: onErr,
  });

  if (isPending || !o) return <Spinner label="Loading…" />;

  const single = o.envelope === "Single";
  const evNames = (o.evaluators ?? []).map((e) => e.name).join(", ") || "—";
  const submitted = o.submittedBidCount;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Bid Openings
        </button>{" "}
        <Icon name="chev" size={13} /> <span>{o.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Open Sealed Bids — {o.code}</h1>
          <p>
            {single
              ? "Single envelope — technical and commercial open together."
              : "Dual envelope. Technical must be opened and scored before commercial unseals."}
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="grid g2">
        <div className="env tech">
          <div className="etop">
            <span className="seal" style={{ color: "#5b56b0" }}>
              <Icon name="lock" size={22} />
            </span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 15 }}>Technical envelope</div>
              <div className="hint">
                {submitted} sealed submission{submitted !== 1 ? "s" : ""}
              </div>
            </div>
          </div>
          <div className="ebody">
            <div className="locked-note" style={{ marginBottom: 12 }}>
              <Icon name="users" size={16} /> Restricted to assigned evaluators:{" "}
              <strong style={{ color: "var(--ink)" }}>{evNames}</strong>
            </div>
            {o.technicalOpened ? (
              <button type="button" className="btn btn-pri" onClick={onScore}>
                <Icon name="edit" size={15} /> {o.techFinalized ? "View technical scoring" : "Continue technical scoring"}
              </button>
            ) : (
              <button
                type="button"
                className="btn btn-pri"
                disabled={!o.canOpenTechnical}
                onClick={() => setAuthorise(true)}
              >
                <Icon name="unlock" size={15} /> Open technical envelope
              </button>
            )}
          </div>
        </div>

        <div className="env comm">
          <div className="etop">
            <span className="seal" style={{ color: "#2f7d56" }}>
              <Icon name="lock" size={22} />
            </span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 15 }}>Commercial envelope</div>
              <div className="hint">Pricing &amp; commercial terms</div>
            </div>
          </div>
          <div className="ebody">
            <div className="locked-note" style={{ marginBottom: 12 }}>
              <Icon name="lock" size={16} />{" "}
              {o.techFinalized || single
                ? "Technical finalized — commercial can be opened."
                : "Sealed until technical scoring is finalized."}
            </div>
            {o.commercialOpened ? (
              <button type="button" className="btn btn-pri" onClick={onAward}>
                <Icon name="award" size={15} /> View commercial evaluation
              </button>
            ) : (
              <button
                type="button"
                className={`btn ${o.canOpenCommercial ? "btn-pri" : "btn-out"}`}
                disabled={!o.canOpenCommercial}
                onClick={() => openC.mutate()}
              >
                <Icon name="unlock" size={15} /> Open commercial envelope
              </button>
            )}
            {!o.commercialOpened && !o.canOpenCommercial && (o.techFinalized || single) ? (
              <p className="hint" style={{ marginTop: 8 }}>
                A commercial evaluator must open this envelope.
              </p>
            ) : null}
          </div>
        </div>
      </div>

      {authorise ? (
        <ConfirmModal
          icon="lock"
          title="Authorise — Open Technical Envelope"
          body={
            <>
              <p className="hint" style={{ marginTop: 0 }}>
                This envelope is sealed. Only an assigned evaluator can open it. Confirm to unseal the technical envelope
                for scoring.
              </p>
              <div className="field">
                <label>Assigned evaluators</label>
                <div style={{ fontWeight: 600 }}>{evNames}</div>
              </div>
              <div className="locked-note">
                <Icon name="flag" size={15} /> Action is logged to the audit trail with your name &amp; timestamp.
              </div>
            </>
          }
          confirmLabel="Unseal technical"
          confirmIcon="unlock"
          busy={openT.isPending}
          onCancel={() => setAuthorise(false)}
          onConfirm={() => openT.mutate()}
        />
      ) : null}
    </>
  );
}

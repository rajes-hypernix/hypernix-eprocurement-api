import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getBidOpening, getRfq, openCommercialEnvelope, openTechnicalEnvelope } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

export function BidOpeningDetailPage({
  rfqId,
  onBack,
  onScore,
}: {
  rfqId: string;
  onBack: () => void;
  onScore: () => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [opening, setOpening] = useState<"technical" | "commercial" | null>(null);

  const { data: status, isPending } = useQuery({ queryKey: ["bid-opening", rfqId], queryFn: () => getBidOpening(rfqId) });
  const { data: rfq } = useQuery({ queryKey: ["rfq", rfqId], queryFn: () => getRfq(rfqId) });

  const refresh = () => void qc.invalidateQueries({ queryKey: ["bid-opening", rfqId] });
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const openTech = useMutation({
    mutationFn: () => openTechnicalEnvelope(rfqId),
    onSuccess: () => {
      setOpening(null);
      refresh();
      onScore();
    },
    onError: (e: Error) => {
      setOpening(null);
      onErr(e);
    },
  });

  const openComm = useMutation({
    mutationFn: () => openCommercialEnvelope(rfqId),
    onSuccess: () => {
      setOpening(null);
      refresh();
    },
    onError: (e: Error) => {
      setOpening(null);
      onErr(e);
    },
  });

  if (isPending || !status) return <Spinner label="Loading…" />;

  const isDual = status.envelope === "Dual";
  const canOpenTechnical = isDual && !status.technicalOpened && status.submittedBidCount > 0;
  const canOpenCommercial = isDual
    ? status.techFinalized && !status.commercialOpened
    : !status.commercialOpened && status.submittedBidCount > 0;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Bid Openings
        </button>{" "}
        <Icon name="chev" size={12} /> {rfq?.code}
      </div>
      <div className="pagehead">
        <div>
          <h1>{rfq?.code}</h1>
          <p>{rfq?.title}</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="ribbon">{status.submittedBidCount} of {status.invitedCount} vendor(s) submitted a bid.</div>

      <div className="grid g2" style={{ marginTop: 14 }}>
        {isDual ? (
          <div className="card">
            <div className="chead">
              <h3>
                <Icon name={status.technicalOpened ? "unlock" : "lock"} size={15} /> Technical envelope
              </h3>
            </div>
            <div className="cbody">
              {status.techFinalized ? (
                <p className="hint" style={{ margin: 0 }}>Finalized — scoring is locked.</p>
              ) : status.technicalOpened ? (
                <button type="button" className="btn btn-pri btn-sm" onClick={onScore}>
                  Go to scoring <Icon name="chev" size={13} />
                </button>
              ) : (
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  disabled={!canOpenTechnical}
                  onClick={() => setOpening("technical")}
                >
                  Open technical envelope
                </button>
              )}
              {!canOpenTechnical && !status.technicalOpened ? (
                <p className="hint" style={{ marginTop: 8 }}>Waiting for submitted bids.</p>
              ) : null}
            </div>
          </div>
        ) : null}

        <div className="card">
          <div className="chead">
            <h3>
              <Icon name={status.commercialOpened ? "unlock" : "lock"} size={15} /> Commercial envelope
            </h3>
          </div>
          <div className="cbody">
            {status.commercialOpened ? (
              <p className="hint" style={{ margin: 0 }}>Opened — pricing is revealed for award.</p>
            ) : (
              <button
                type="button"
                className="btn btn-pri btn-sm"
                disabled={!canOpenCommercial}
                onClick={() => setOpening("commercial")}
              >
                Open commercial envelope
              </button>
            )}
            {isDual && !status.techFinalized && !status.commercialOpened ? (
              <p className="hint" style={{ marginTop: 8 }}>A technical evaluator must finalize scoring first.</p>
            ) : null}
          </div>
        </div>
      </div>

      {opening ? (
        <ConfirmModal
          title={`Open ${opening} envelope`}
          icon="unlock"
          body="This action is logged to the audit trail with your name and timestamp."
          confirmLabel="Authorise"
          busy={openTech.isPending || openComm.isPending}
          onCancel={() => setOpening(null)}
          onConfirm={() => (opening === "technical" ? openTech.mutate() : openComm.mutate())}
        />
      ) : null}
    </>
  );
}

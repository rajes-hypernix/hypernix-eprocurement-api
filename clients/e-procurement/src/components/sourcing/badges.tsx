const TONE: Record<string, string> = {
  // PR header status
  Draft: "b-grey",
  Submitted: "b-blue",
  PartiallySourced: "b-amber",
  Sourced: "b-green",
  Cancelled: "b-red",
  // PR line / general lifecycle
  Open: "b-blue",
  InDraftRfq: "b-amber",
  InRfq: "b-amber",
  Awarded: "b-green",
  // RFQ status
  Closed: "b-amber",
  Evaluation: "b-teal",
  // Invitation status
  Invited: "b-grey",
  Viewed: "b-blue",
  IntendToBid: "b-blue",
  Declined: "b-red",
  BidSubmitted: "b-green",
  Rescinded: "b-red",
  // Award status
  PendingApproval: "b-amber",
  Approved: "b-green",
};

export function SourcingStatusBadge({ status }: { status: string | null | undefined }) {
  const tone = TONE[status ?? ""] ?? "b-grey";
  return <span className={`badge ${tone}`}>{status}</span>;
}

export function EnvelopeTag({ envelope }: { envelope: string }) {
  return (
    <span className="badge b-teal" title={envelope === "Dual" ? "Sealed technical + commercial" : "Single envelope"}>
      {envelope === "Dual" ? "🔒 Dual" : "Single"}
    </span>
  );
}

import { Icon } from "@/components/Icon";

/** RFQ header status — stage copy (POC rfqStatus). */
const RFQ_STATUS: Record<string, [string, string]> = {
  Draft: ["b-grey", "Draft"],
  Open: ["b-amber", "Open · Awaiting bids"],
  Closed: ["b-blue", "Closed · Ready to open"],
  Evaluation: ["b-teal", "Under evaluation"],
  Awarded: ["b-green", "Awarded"],
  Cancelled: ["b-red", "Cancelled"],
};

const INV_STATUS: Record<string, [string, string]> = {
  Invited: ["b-grey", "Invited"],
  Viewed: ["b-grey", "Viewed"],
  IntendToBid: ["b-teal", "Intends to bid"],
  Declined: ["b-amber", "Declined"],
  BidSubmitted: ["b-green", "Bid submitted"],
  Rescinded: ["b-grey", "Rescinded"],
};

const TONE: Record<string, string> = {
  Draft: "b-grey",
  Submitted: "b-blue",
  PartiallySourced: "b-amber",
  PartiallyOrdered: "b-amber",
  Sourced: "b-green",
  Cancelled: "b-red",
  Open: "b-blue",
  InDraftRfq: "b-amber",
  InRfq: "b-amber",
  Awarded: "b-green",
  Closed: "b-amber",
  Evaluation: "b-teal",
  Invited: "b-grey",
  Viewed: "b-blue",
  IntendToBid: "b-blue",
  Declined: "b-red",
  BidSubmitted: "b-green",
  Rescinded: "b-red",
  PendingApproval: "b-amber",
  Approved: "b-green",
};

/** Generic sourcing status (PR / award / fallback). Prefer RfqStatusBadge / InvitationStatusBadge on RFQ surfaces. */
export function SourcingStatusBadge({ status }: { status: string | null | undefined }) {
  const tone = TONE[status ?? ""] ?? "b-grey";
  return <span className={`badge ${tone}`}>{status}</span>;
}

export function RfqStatusBadge({ status }: { status: string | null | undefined }) {
  const [tone, label] = RFQ_STATUS[status ?? ""] ?? ["b-grey", status ?? "—"];
  return <span className={`badge ${tone}`}>{label}</span>;
}

export function InvitationStatusBadge({ status }: { status: string | null | undefined }) {
  const [tone, label] = INV_STATUS[status ?? ""] ?? ["b-grey", status ?? "—"];
  return <span className={`badge ${tone}`}>{label}</span>;
}

export function bidProgress(
  bidCount?: number,
  invitedCount?: number,
): { tone: string; label: string } | null {
  const inv = invitedCount ?? 0;
  const bids = bidCount ?? 0;
  if (inv === 0) return null;
  if (bids === 0) return { tone: "b-grey", label: "Awaiting bids" };
  if (bids >= inv) return { tone: "b-green", label: `All bids in · ${bids}/${inv}` };
  return { tone: "b-amber", label: `Partially bid · ${bids}/${inv}` };
}

export function BidProgressBadge({
  bidCount,
  invitedCount,
}: {
  bidCount?: number;
  invitedCount?: number;
}) {
  const p = bidProgress(bidCount, invitedCount);
  return p ? <span className={`badge ${p.tone}`}>{p.label}</span> : null;
}

export function EnvelopeTag({ envelope }: { envelope: string }) {
  return envelope === "Dual" ? (
    <span className="env-tag" title="Sealed technical + commercial">
      <Icon name="lock" size={12} /> Dual
    </span>
  ) : (
    <span className="env-tag" title="Single envelope">
      Single
    </span>
  );
}

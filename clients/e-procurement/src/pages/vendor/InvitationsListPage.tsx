import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { listMyInvitations } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { EnvelopeTag } from "@/components/sourcing/badges";
import { dateTimeMY } from "@/lib/format";

const OUTCOME: Record<string, { label: string; tone: string; cta: string }> = {
  BidSubmitted: { label: "Bid submitted", tone: "b-green", cta: "Edit bid" },
  Declined: { label: "Declined", tone: "b-red", cta: "Reconsider" },
  Rescinded: { label: "Invitation withdrawn", tone: "b-grey", cta: "View" },
};

/** Shared list for both "My RFQs" (mode=invitations) and "My Bids" (mode=bids) vendor nav entries. */
export function InvitationsListPage({ mode, onOpen }: { mode: "invitations" | "bids"; onOpen: (rfqId: string) => void }) {
  const { data, isPending } = useQuery({ queryKey: ["my-invitations"], queryFn: listMyInvitations });

  const rows = useMemo(() => {
    const items = data ?? [];
    return mode === "bids" ? items.filter((i) => i.hasSubmittedBid || i.invitationStatus !== "Invited") : items;
  }, [data, mode]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>{mode === "bids" ? "My Bids" : "My RFQs"}</h1>
          <p>{mode === "bids" ? "Bids you've drafted or submitted." : "RFQs you've been invited to bid on."}</p>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>RFQ</th>
                <th>Title</th>
                <th>Envelope</th>
                <th>Closes</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => {
                const outcome = OUTCOME[r.invitationStatus] ?? {
                  label: r.hasSubmittedBid ? "Bid submitted" : "Action needed",
                  tone: r.hasSubmittedBid ? "b-green" : "b-amber",
                  cta: r.hasSubmittedBid ? "Edit bid" : "Start bid",
                };
                return (
                  <tr key={r.rfqId} className="drillrow" onClick={() => onOpen(r.rfqId)}>
                    <td style={{ fontWeight: 700 }}>{r.rfqCode}</td>
                    <td>{r.title || <span className="hint">(untitled)</span>}</td>
                    <td>
                      <EnvelopeTag envelope={r.envelope} />
                    </td>
                    <td>{dateTimeMY(r.closesUtc)}</td>
                    <td>
                      <span className={`badge ${outcome.tone}`}>{outcome.label}</span>
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        {outcome.cta} <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={6}>
                    <EmptyState>Nothing here yet.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

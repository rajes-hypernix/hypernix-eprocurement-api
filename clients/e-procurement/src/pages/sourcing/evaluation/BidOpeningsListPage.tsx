import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { listRfqs } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { SourcingStatusBadge, EnvelopeTag } from "@/components/sourcing/badges";

const RELEVANT = new Set(["Closed", "Evaluation", "Awarded"]);

export function BidOpeningsListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const { data, isPending } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs });
  const rows = useMemo(() => (data ?? []).filter((r) => RELEVANT.has(r.status)), [data]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Bid Openings</h1>
          <p>Open sealed envelopes and score technical submissions.</p>
        </div>
      </div>
      <div className="card">
        {isPending ? (
          <Spinner label="Loading…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Title</th>
                <th>Envelope</th>
                <th className="amt">Bids</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                  <td style={{ fontWeight: 700 }}>{r.code}</td>
                  <td>{r.title}</td>
                  <td>
                    <EnvelopeTag envelope={r.envelope} />
                  </td>
                  <td className="amt">{r.invitedCount}</td>
                  <td>
                    <SourcingStatusBadge status={r.status} />
                  </td>
                  <td className="amt">
                    <span className="btn btn-ghost btn-sm">
                      Open <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={6}>
                    <EmptyState>Nothing ready to evaluate yet.</EmptyState>
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

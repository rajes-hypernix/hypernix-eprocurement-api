import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { listAwards, listRfqs } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { fmt } from "@/lib/format";

export function AwardsListPage({ onOpen }: { onOpen: (rfqId: string) => void }) {
  const { data: rfqs, isPending: rfqsPending } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs });
  const { data: awards, isPending: awardsPending } = useQuery({ queryKey: ["awards"], queryFn: listAwards });

  const readyToAward = useMemo(() => (rfqs ?? []).filter((r) => r.status === "Evaluation"), [rfqs]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Awards</h1>
          <p>Allocate awarded quantities/prices per vendor and route through Delegation-of-Authority approval.</p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Ready to award</h3>
        </div>
        <div className="cbody">
          {rfqsPending ? (
            <Spinner label="Loading…" />
          ) : readyToAward.length === 0 ? (
            <EmptyState>Nothing in evaluation right now.</EmptyState>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Title</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {readyToAward.map((r) => (
                  <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                    <td style={{ fontWeight: 700 }}>{r.code}</td>
                    <td>{r.title}</td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Award <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Award decisions</h3>
        </div>
        <div className="cbody">
          {awardsPending ? (
            <Spinner label="Loading…" />
          ) : (awards ?? []).length === 0 ? (
            <EmptyState>No awards yet.</EmptyState>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Award</th>
                  <th>RFQ</th>
                  <th className="amt">Total value</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {(awards ?? []).map((a) => (
                  <tr key={a.id} className="drillrow" onClick={() => onOpen(a.rfqId)}>
                    <td style={{ fontWeight: 700 }}>{a.code}</td>
                    <td>{a.rfqCode}</td>
                    <td className="amt">{fmt(a.totalValue)}</td>
                    <td>
                      <SourcingStatusBadge status={a.status} />
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        View <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}

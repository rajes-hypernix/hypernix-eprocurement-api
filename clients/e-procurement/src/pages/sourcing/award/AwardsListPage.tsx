import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listAwards, listRfqs } from "@/api/sourcing";
import { listPurchaseOrders } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { SourcingStatusBadge } from "@/components/sourcing/badges";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt } from "@/lib/format";

export function AwardsListPage({ onOpen }: { onOpen: (rfqId: string) => void }) {
  const { user } = useAuth();
  const canViewPos = user?.permissions.includes(FshPermissions.purchaseOrders.view) ?? false;
  const [q, setQ] = useState("");
  const [status, setStatus] = useState("all");

  const { data: rfqs, isPending: rfqsPending } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs });
  const { data: awards, isPending: awardsPending } = useQuery({ queryKey: ["awards"], queryFn: listAwards });
  const { data: pos } = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
    enabled: canViewPos,
  });

  const readyToAward = useMemo(() => (rfqs ?? []).filter((r) => r.status === "Evaluation"), [rfqs]);
  const posByAward = useMemo(() => {
    const map = new Map<string, string[]>();
    for (const p of pos ?? []) {
      if (!p.awardId) continue;
      const list = map.get(p.awardId) ?? [];
      list.push(p.code);
      map.set(p.awardId, list);
    }
    return map;
  }, [pos]);

  const rows = useMemo(() => {
    const needle = q.trim().toLowerCase();
    return (awards ?? []).filter((a) => {
      if (status !== "all" && a.status !== status) return false;
      if (!needle) return true;
      const codes = (posByAward.get(a.id) ?? []).join(" ");
      return `${a.code} ${a.rfqCode} ${codes}`.toLowerCase().includes(needle);
    });
  }, [awards, q, status, posByAward]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Awards &amp; Purchase Orders</h1>
          <p>Evaluate, allocate and award RFQs. Approval (DoA) generates one PO per vendor.</p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Ready to evaluate / award</h3>
        </div>
        <div className="cbody">
          {rfqsPending ? (
            <Spinner label="Loading…" />
          ) : readyToAward.length === 0 ? (
            <EmptyState>No RFQs under evaluation.</EmptyState>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>RFQ</th>
                  <th>Title</th>
                  <th>Envelope</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {readyToAward.map((r) => (
                  <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                    <td style={{ fontWeight: 700, color: "var(--teal)" }}>{r.code}</td>
                    <td>{r.title}</td>
                    <td>{r.envelope}</td>
                    <td>
                      <SourcingStatusBadge status={r.status} />
                    </td>
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

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Award</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Award or RFQ number…" />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>Status</label>
              <select value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="all">All</option>
                <option value="PendingApproval">Pending approval</option>
                <option value="Approved">Approved</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Award decisions</h3>
        </div>
        <div className="cbody">
          {awardsPending ? (
            <Spinner label="Loading…" />
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Award</th>
                  <th>RFQ</th>
                  <th>Status</th>
                  <th>Approver</th>
                  <th className="amt">Value</th>
                  <th>PO(s)</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((a) => (
                  <tr key={a.id} className="drillrow" onClick={() => onOpen(a.rfqId)}>
                    <td style={{ fontWeight: 700 }}>{a.code}</td>
                    <td style={{ color: "var(--teal)" }}>{a.rfqCode}</td>
                    <td>
                      <SourcingStatusBadge status={a.status} />
                    </td>
                    <td>{a.approverUserId ?? "—"}</td>
                    <td className="amt">RM {fmt(a.totalValue)}</td>
                    <td>
                      {(posByAward.get(a.id) ?? []).length === 0
                        ? "—"
                        : (posByAward.get(a.id) ?? []).map((c) => (
                            <span key={c} className="swchip" style={{ marginRight: 4 }}>
                              {c}
                            </span>
                          ))}
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
                    <td colSpan={7}>
                      <EmptyState>No awards match these filters.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}

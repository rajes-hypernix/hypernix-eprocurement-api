import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listAsns, listPurchaseOrders } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { AsnStatusBadge, PoStatusBadge } from "@/components/procurement/badges";
import { dateMY } from "@/lib/format";

const READY_TO_SHIP = new Set(["Acknowledged", "PartiallyReceived"]);

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

export function DeliveryListPage({
  onOpen,
  onNewAsn,
  onReceive,
}: {
  onOpen: (id: string) => void;
  onNewAsn: (poId: string) => void;
  onReceive: (asnId: string) => void;
}) {
  const { isVendor } = useAuth();
  const [q, setQ] = useState("");

  const { data: asns, isPending: asnsPending } = useQuery({
    queryKey: ["asns"],
    queryFn: () => listAsns(),
  });
  const { data: pos, isPending: posPending } = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
    enabled: isVendor,
  });

  const readyPos = useMemo(
    () => (pos ?? []).filter((p) => READY_TO_SHIP.has(p.status)),
    [pos],
  );

  const rows = useMemo(() => {
    const items = asns ?? [];
    if (!q.trim()) return items;
    const needle = q.trim().toLowerCase();
    return items.filter(
      (a) =>
        `${a.code} ${a.carrier} ${a.trackingNo} ${a.poId}`.toLowerCase().includes(needle),
    );
  }, [asns, q]);

  const isPending = asnsPending || (isVendor && posPending);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Deliveries</h1>
          <p>Advance shipping notices and goods receipt against purchase orders.</p>
        </div>
      </div>

      {isVendor ? (
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="chead">
            <h3>Ready to ship</h3>
            <span className="sub">· acknowledged POs awaiting ASN</span>
          </div>
          <div className="cbody">
            {posPending ? (
              <Spinner label="Loading POs…" />
            ) : readyPos.length === 0 ? (
              <EmptyState>No POs ready for shipping.</EmptyState>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>PO</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {readyPos.map((p) => (
                    <tr key={p.id}>
                      <td style={{ fontWeight: 700 }}>{p.code}</td>
                      <td>
                        <PoStatusBadge status={p.status} />
                      </td>
                      <td className="amt">
                        <button type="button" className="btn btn-pri btn-sm" onClick={() => onNewAsn(p.id)}>
                          Create ASN
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search code / carrier / tracking / PO</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="ASN-2026-…" />
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading deliveries…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>PO</th>
                <th>Carrier</th>
                <th>Tracking</th>
                <th>Status</th>
                <th>Expected</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((a) => (
                <tr key={a.id} className="drillrow" onClick={() => onOpen(a.id)}>
                  <td style={{ fontWeight: 700 }}>{a.code}</td>
                  <td>{shortId(a.poId)}</td>
                  <td>{a.carrier}</td>
                  <td>{a.trackingNo}</td>
                  <td>
                    <AsnStatusBadge status={a.status} />
                  </td>
                  <td>{dateMY(a.expectedDate)}</td>
                  <td className="amt">
                    {!isVendor && a.status === "InTransit" ? (
                      <button
                        type="button"
                        className="btn btn-pri btn-sm"
                        onClick={(e) => {
                          e.stopPropagation();
                          onReceive(a.id);
                        }}
                      >
                        Receive
                      </button>
                    ) : (
                      <span className="btn btn-ghost btn-sm">
                        Open <Icon name="chev" size={13} />
                      </span>
                    )}
                  </td>
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={7}>
                    <EmptyState>No shipping notices match the filter.</EmptyState>
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

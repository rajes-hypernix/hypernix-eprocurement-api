import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listAsns, listPurchaseOrders } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { AsnStatusBadge, PoStatusBadge } from "@/components/procurement/badges";
import { dateMY, fmt } from "@/lib/format";

const READY_TO_SHIP = new Set(["Acknowledged", "PartiallyReceived"]);

function poLabel(a: { poCode?: string | null; poId: string }): string {
  return a.poCode?.trim() || a.poId;
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
  const [status, setStatus] = useState("all");

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
    const needle = q.trim().toLowerCase();
    return (asns ?? []).filter((a) => {
      if (status !== "all" && a.status !== status) return false;
      if (!needle) return true;
      return `${a.code} ${poLabel(a)} ${a.carrier} ${a.trackingNo}`.toLowerCase().includes(needle);
    });
  }, [asns, q, status]);

  const isPending = asnsPending || (isVendor && posPending);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Deliveries</h1>
          <p>Advance shipping notices and goods receipts. The buyer receives against each ASN on delivery.</p>
        </div>
      </div>

      {isVendor ? (
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="chead">
            <h3>Ready to ship</h3>
            <span className="hint">acknowledged POs</span>
          </div>
          <div className="cbody">
            {posPending ? (
              <Spinner label="Loading POs…" />
            ) : readyPos.length === 0 ? (
              <EmptyState>No acknowledged POs ready to ship.</EmptyState>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>PO</th>
                    <th className="amt">Value</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {readyPos.map((p) => (
                    <tr key={p.id}>
                      <td style={{ fontWeight: 700, color: "var(--teal)" }}>{p.code}</td>
                      <td className="amt">
                        {p.currency} {fmt(p.totalValue)}
                      </td>
                      <td>
                        <PoStatusBadge status={p.status} />
                      </td>
                      <td className="amt">
                        <button type="button" className="btn btn-pri btn-sm" onClick={() => onNewAsn(p.id)}>
                          <Icon name="send" size={13} /> New shipping notice
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
              <label>ASN</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="ASN or PO number…" />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>Status</label>
              <select value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="all">All</option>
                <option value="InTransit">In transit</option>
                <option value="Received">Received</option>
              </select>
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
                <th>ASN</th>
                <th>PO</th>
                <th>Carrier</th>
                <th>Expected</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((a) => (
                <tr key={a.id} className="drillrow" onClick={() => onOpen(a.id)}>
                  <td style={{ fontWeight: 700, color: "var(--teal)" }}>{a.code}</td>
                  <td>{poLabel(a)}</td>
                  <td>{a.carrier || "—"}</td>
                  <td>{dateMY(a.expectedDate)}</td>
                  <td>
                    <AsnStatusBadge status={a.status} />
                  </td>
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
                  <td colSpan={6}>
                    <EmptyState>No shipping notices match these filters.</EmptyState>
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

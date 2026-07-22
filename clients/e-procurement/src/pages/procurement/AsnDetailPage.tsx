import { useQuery } from "@tanstack/react-query";
import { getAsn, getGrnByAsn, getPurchaseOrder } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { Spinner } from "@/components/ui";
import { AsnStatusBadge, PoStatusBadge } from "@/components/procurement/badges";
import { fmt, dateMY, dateTimeMY } from "@/lib/format";

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

export function AsnDetailPage({
  id,
  onBack,
  onReceive,
}: {
  id: string;
  onBack: () => void;
  onReceive: () => void;
}) {
  const { isVendor } = useAuth();

  const { data: asn, isPending: asnPending } = useQuery({
    queryKey: ["asn", id],
    queryFn: () => getAsn(id),
  });
  const { data: grn } = useQuery({
    queryKey: ["grn-by-asn", id],
    queryFn: () => getGrnByAsn(id),
    enabled: Boolean(asn?.status === "Received"),
  });
  const { data: po } = useQuery({
    queryKey: ["purchase-order", asn?.poId],
    queryFn: () => getPurchaseOrder(asn!.poId),
    enabled: Boolean(asn?.poId),
  });

  if (asnPending || !asn) return <Spinner label="Loading ASN…" />;

  const canReceive = !isVendor && asn.status === "InTransit";

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Deliveries
        </button>{" "}
        <Icon name="chev" size={12} /> {asn.code}
      </div>

      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {asn.code} <AsnStatusBadge status={asn.status} />
          </h1>
          <p>
            PO {po?.code ?? shortId(asn.poId)}
            {po ? (
              <>
                {" "}
                · <PoStatusBadge status={po.status} />
              </>
            ) : null}
          </p>
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Shipment</h3>
        </div>
        <div className="cbody">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 12 }}>
            <div>
              <div className="hint">Carrier</div>
              <div style={{ fontWeight: 600 }}>{asn.carrier}</div>
            </div>
            <div>
              <div className="hint">Tracking no.</div>
              <div style={{ fontWeight: 600 }}>{asn.trackingNo}</div>
            </div>
            <div>
              <div className="hint">Shipped</div>
              <div>{dateMY(asn.shippedDate)}</div>
            </div>
            <div>
              <div className="hint">Expected</div>
              <div>{dateMY(asn.expectedDate)}</div>
            </div>
            <div>
              <div className="hint">Created</div>
              <div>{dateTimeMY(asn.createdUtc)}</div>
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Lines</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th className="amt">Shipped qty</th>
                <th>Lot no.</th>
              </tr>
            </thead>
            <tbody>
              {(asn.lines ?? []).map((l) => (
                <tr key={l.id}>
                  <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                  <td className="amt">{fmt(l.shippedQty)}</td>
                  <td>{l.lotNo ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {grn ? (
        <div className="card" style={{ marginTop: 14 }}>
          <div className="chead">
            <h3>Goods receipt · {grn.code}</h3>
          </div>
          <div className="cbody">
            <table>
              <thead>
                <tr>
                  <th>Item</th>
                  <th className="amt">Expected</th>
                  <th className="amt">Received</th>
                  <th>Condition</th>
                </tr>
              </thead>
              <tbody>
                {(grn.lines ?? []).map((l) => (
                  <tr key={l.id}>
                    <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                    <td className="amt">{fmt(l.expectedQty)}</td>
                    <td className="amt">{fmt(l.receivedQty)}</td>
                    <td>{l.condition}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {canReceive ? (
        <div className="actionbar" style={{ marginTop: 14 }}>
          <div className="spacer" style={{ flex: 1 }} />
          <button type="button" className="btn btn-pri" onClick={onReceive}>
            Receive goods
          </button>
        </div>
      ) : null}
    </>
  );
}

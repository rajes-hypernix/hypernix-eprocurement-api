import { useQuery } from "@tanstack/react-query";
import { getAsn, getGrnByAsn } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { AsnStatusBadge } from "@/components/procurement/badges";
import { fmt, dateMY } from "@/lib/format";

function poLabel(asn: { poCode?: string | null; poId: string }): string {
  return asn.poCode?.trim() || asn.poId;
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

  const { data: asn, isPending: asnPending, isError } = useQuery({
    queryKey: ["asn", id],
    queryFn: () => getAsn(id),
    retry: false,
  });
  const { data: grn } = useQuery({
    queryKey: ["grn-by-asn", id],
    queryFn: () => getGrnByAsn(id),
    enabled: Boolean(asn),
    retry: false,
  });

  if (asnPending) return <Spinner label="Loading ASN…" />;

  if (isError || !asn) {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={onBack}>
            Deliveries
          </button>
        </div>
        <div className="pagehead">
          <div>
            <h1>Shipping notice</h1>
            <p>This ASN was not found.</p>
          </div>
          <button type="button" className="btn btn-out" onClick={onBack}>
            <Icon name="back" size={15} /> Back
          </button>
        </div>
        <div className="card">
          <div className="cbody">
            <EmptyState>No shipping notice for this id.</EmptyState>
          </div>
        </div>
      </>
    );
  }

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
          <h1>{asn.code}</h1>
          <p>
            {poLabel(asn)}
            {asn.vendorName ? ` · ${asn.vendorName}` : ""}
            {asn.expectedDate ? ` · expected ${dateMY(asn.expectedDate)}` : ""}
          </p>
        </div>
      </div>

      <div className="ribbon">
        <AsnStatusBadge status={asn.status} />
        {grn ? (
          <span className="badge b-green" style={{ marginLeft: 6 }}>
            <Icon name="check" size={11} /> Received · {grn.code}
          </span>
        ) : null}
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>Shipment lines</h3>
          <span className="hint">tracking {asn.trackingNo || "—"}</span>
        </div>
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Item</th>
              <th className="amt">Shipped</th>
              <th>Lot</th>
            </tr>
          </thead>
          <tbody>
            {(asn.lines ?? []).map((l) => (
              <tr key={l.id}>
                <td>{l.itemCode}</td>
                <td>{l.description || "—"}</td>
                <td className="amt">
                  {fmt(l.shippedQty)}
                  {l.uom ? ` ${l.uom}` : ""}
                </td>
                <td>{l.lotNo || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {grn ? (
        <div className="card">
          <div className="chead">
            <h3>Goods receipt · {grn.code}</h3>
          </div>
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th className="amt">Expected</th>
                <th className="amt">Received</th>
                <th>Condition</th>
              </tr>
            </thead>
            <tbody>
              {(grn.lines ?? []).map((l) => (
                <tr key={l.id}>
                  <td>{l.itemCode}</td>
                  <td className="amt">{fmt(l.expectedQty)}</td>
                  <td className="amt">{fmt(l.receivedQty)}</td>
                  <td>
                    <span className={`badge ${l.condition === "Short" ? "b-amber" : "b-green"}`}>{l.condition}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
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

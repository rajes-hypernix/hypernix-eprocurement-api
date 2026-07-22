import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getAsn, receiveAsn, type ReceiveLineInput } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { AsnStatusBadge } from "@/components/procurement/badges";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type LineDraft = { itemCode: string; shippedQty: number; receivedQty: number };

export function ReceiveAsnPage({
  asnId,
  onBack,
  onDone,
}: {
  asnId: string;
  onBack: () => void;
  onDone: () => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [hydrated, setHydrated] = useState(false);

  const { data: asn, isPending } = useQuery({
    queryKey: ["asn", asnId],
    queryFn: () => getAsn(asnId),
  });

  if (asn && !hydrated) {
    setHydrated(true);
    setLines(
      (asn.lines ?? []).map((l) => ({
        itemCode: l.itemCode,
        shippedQty: l.shippedQty,
        receivedQty: l.shippedQty,
      })),
    );
  }

  const totalReceived = useMemo(() => lines.reduce((s, l) => s + l.receivedQty, 0), [lines]);
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const receive = useMutation({
    mutationFn: () => {
      const payload: ReceiveLineInput[] = lines
        .filter((l) => l.receivedQty > 0)
        .map((l) => ({ itemCode: l.itemCode, receivedQty: Math.min(l.receivedQty, l.shippedQty) }));
      return receiveAsn(asnId, payload);
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["asn", asnId] });
      void qc.invalidateQueries({ queryKey: ["asns"] });
      void qc.invalidateQueries({ queryKey: ["purchase-orders"] });
      void qc.invalidateQueries({ queryKey: ["grn-by-asn", asnId] });
      onDone();
    },
    onError: onErr,
  });

  if (isPending || !asn) return <Spinner label="Loading ASN…" />;

  if (asn.status !== "InTransit") {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={onBack}>
            ASN
          </button>{" "}
          <Icon name="chev" size={12} /> {asn.code}
        </div>
        <Notice tone="warn" icon="flag">
          This ASN is already <AsnStatusBadge status={asn.status} /> — nothing to receive.
        </Notice>
      </>
    );
  }

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          ASN
        </button>{" "}
        <Icon name="chev" size={12} /> Receive · {asn.code}
      </div>

      <div className="pagehead">
        <div>
          <h1>Receive goods · {asn.code}</h1>
          <p>
            {asn.carrier} · {asn.trackingNo}
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Receipt quantities</h3>
          <span className="sub">· capped to shipped qty per line</span>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th className="amt">Shipped</th>
                <th className="amt">Receive qty</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l) => (
                <tr key={l.itemCode}>
                  <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                  <td className="amt">{fmt(l.shippedQty)}</td>
                  <td className="amt">
                    <input
                      type="number"
                      style={{ width: 80 }}
                      min={0}
                      max={l.shippedQty}
                      value={l.receivedQty}
                      onChange={(e) => {
                        const v = Math.min(Math.max(0, Number(e.target.value)), l.shippedQty);
                        setLines((xs) => xs.map((x) => (x.itemCode === l.itemCode ? { ...x, receivedQty: v } : x)));
                      }}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="actionbar" style={{ marginTop: 14 }}>
        <div className="spacer" style={{ flex: 1 }} />
        <button type="button" className="btn btn-out" onClick={onBack}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-pri"
          disabled={totalReceived <= 0 || receive.isPending}
          onClick={() => receive.mutate()}
        >
          Confirm receipt
        </button>
      </div>
    </>
  );
}

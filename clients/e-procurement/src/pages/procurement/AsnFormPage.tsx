import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createAsn, getPurchaseOrder, listAsns, type AsnLineInput } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { PoStatusBadge } from "@/components/procurement/badges";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type LineDraft = { itemCode: string; description: string; remaining: number; shippedQty: number; lotNo: string };

export function AsnFormPage({
  poId,
  onBack,
  onSaved,
}: {
  poId: string;
  onBack: () => void;
  onSaved: (asnId: string) => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [carrier, setCarrier] = useState("");
  const [trackingNo, setTrackingNo] = useState("");
  const [shippedDate, setShippedDate] = useState("");
  const [expectedDate, setExpectedDate] = useState("");
  const [lines, setLines] = useState<LineDraft[]>([]);

  const { data: po, isPending: poPending } = useQuery({
    queryKey: ["purchase-order", poId],
    queryFn: () => getPurchaseOrder(poId),
  });
  const { data: asns, isPending: asnsPending } = useQuery({
    queryKey: ["asns", poId],
    queryFn: () => listAsns(poId),
    enabled: Boolean(po),
  });

  const inTransitByItem = useMemo(() => {
    const map = new Map<string, number>();
    for (const asn of asns ?? []) {
      if (asn.status !== "InTransit") continue;
      for (const l of asn.lines ?? []) {
        map.set(l.itemCode, (map.get(l.itemCode) ?? 0) + l.shippedQty);
      }
    }
    return map;
  }, [asns]);

  useEffect(() => {
    if (!po || asns === undefined) return;
    setLines(
      (po.lines ?? []).map((l) => {
        const inTransit = inTransitByItem.get(l.itemCode) ?? 0;
        const remaining = Math.max(0, l.qty - l.receivedQty - inTransit);
        return {
          itemCode: l.itemCode,
          description: l.description,
          remaining,
          shippedQty: remaining > 0 ? remaining : 0,
          lotNo: "",
        };
      }),
    );
  }, [po, asns, inTransitByItem]);

  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const save = useMutation({
    mutationFn: () => {
      const payload: AsnLineInput[] = lines
        .filter((l) => l.shippedQty > 0)
        .map((l) => ({
          itemCode: l.itemCode,
          shippedQty: Math.min(l.shippedQty, l.remaining),
          lotNo: l.lotNo.trim() || null,
        }));
      return createAsn({
        poId,
        carrier: carrier.trim(),
        trackingNo: trackingNo.trim(),
        shippedDate: shippedDate || null,
        expectedDate: expectedDate || null,
        lines: payload,
      });
    },
    onSuccess: (asn) => {
      void qc.invalidateQueries({ queryKey: ["asns"] });
      void qc.invalidateQueries({ queryKey: ["asns", poId] });
      void qc.invalidateQueries({ queryKey: ["purchase-order", poId] });
      onSaved(asn.id);
    },
    onError: onErr,
  });

  if (poPending || asnsPending || !po) return <Spinner label="Loading PO…" />;

  const shippable = lines.some((l) => l.remaining > 0);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Deliveries
        </button>{" "}
        <Icon name="chev" size={12} /> New ASN · {po.code}
      </div>

      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            Create ASN · {po.code} <PoStatusBadge status={po.status} />
          </h1>
          <p>Ship against remaining quantities not already in transit or received.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      {!shippable ? (
        <Notice tone="warn" icon="flag">
          Nothing left to ship on this PO — all lines are in transit or fully received.
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Shipment details</h3>
        </div>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Carrier</label>
              <input value={carrier} onChange={(e) => setCarrier(e.target.value)} placeholder="DHL, Pos Laju…" />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Tracking no.</label>
              <input value={trackingNo} onChange={(e) => setTrackingNo(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Shipped date</label>
              <input type="date" value={shippedDate} onChange={(e) => setShippedDate(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Expected date</label>
              <input type="date" value={expectedDate} onChange={(e) => setExpectedDate(e.target.value)} />
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Ship plan</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Description</th>
                <th className="amt">Remaining</th>
                <th className="amt">Ship qty</th>
                <th>Lot no.</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l) => (
                <tr key={l.itemCode}>
                  <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                  <td>{l.description}</td>
                  <td className="amt">{fmt(l.remaining)}</td>
                  <td className="amt">
                    <input
                      type="number"
                      style={{ width: 80 }}
                      min={0}
                      max={l.remaining}
                      disabled={l.remaining <= 0}
                      value={l.shippedQty}
                      onChange={(e) => {
                        const v = Math.min(Math.max(0, Number(e.target.value)), l.remaining);
                        setLines((xs) => xs.map((x) => (x.itemCode === l.itemCode ? { ...x, shippedQty: v } : x)));
                      }}
                    />
                  </td>
                  <td>
                    <input
                      value={l.lotNo}
                      disabled={l.remaining <= 0}
                      onChange={(e) =>
                        setLines((xs) => xs.map((x) => (x.itemCode === l.itemCode ? { ...x, lotNo: e.target.value } : x)))
                      }
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
          disabled={!shippable || !carrier.trim() || !trackingNo.trim() || save.isPending}
          onClick={() => save.mutate()}
        >
          Create ASN
        </button>
      </div>
    </>
  );
}

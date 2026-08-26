import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createAsn, getPurchaseOrder, listAsns, type AsnLineInput } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type LineDraft = {
  itemCode: string;
  description: string;
  ordered: number;
  received: number;
  inTransit: number;
  remaining: number;
  shippedQty: number;
  lotNo: string;
};

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export function AsnFormPage({
  poId,
  onBack,
}: {
  poId: string;
  onBack: () => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [carrier, setCarrier] = useState("");
  const [trackingNo, setTrackingNo] = useState("");
  const [shippedDate, setShippedDate] = useState(todayIso());
  const [expectedDate, setExpectedDate] = useState("");
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [confirm, setConfirm] = useState(false);
  const [doneCode, setDoneCode] = useState<string | null>(null);

  const { data: po, isPending: poPending } = useQuery({
    queryKey: ["purchase-order", poId],
    queryFn: () => getPurchaseOrder(poId),
    retry: false,
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
          ordered: l.qty,
          received: l.receivedQty,
          inTransit,
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
      setConfirm(false);
      setDoneCode(asn.code);
    },
    onError: (e: Error) => {
      setConfirm(false);
      onErr(e);
    },
  });

  if (doneCode !== null) {
    return (
      <div className="card" style={{ maxWidth: 560, margin: "40px auto" }}>
        <div className="cbody" style={{ textAlign: "center" }}>
          <div className="health health-ok" style={{ marginBottom: 12 }}>
            <span className="dot" /> Shipping notice submitted
          </div>
          <p>
            ASN <strong>{doneCode}</strong> is now in transit against {po?.code ?? "the PO"}. The buyer will receive
            against it on delivery.
          </p>
          <button type="button" className="btn btn-pri" onClick={onBack}>
            Back to deliveries
          </button>
        </div>
      </div>
    );
  }

  if (poPending || asnsPending || !po) return <Spinner label="Loading PO…" />;

  const shippable = lines.some((l) => l.remaining > 0);
  const anyQty = lines.some((l) => l.shippedQty > 0);

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
          <h1>Advance shipping notice</h1>
          <p>Notify the buyer of an inbound shipment against {po.code}.</p>
        </div>
        <button
          type="button"
          className="btn btn-pri"
          disabled={!shippable || save.isPending}
          onClick={() => {
            if (!carrier.trim()) {
              setErr("Carrier is required.");
              return;
            }
            if (!anyQty) {
              setErr("Enter a shipment quantity on at least one line.");
              return;
            }
            setErr(null);
            setConfirm(true);
          }}
        >
          <Icon name="send" size={15} /> Submit ASN
        </button>
      </div>

      {!shippable ? (
        <Notice tone="warn" icon="lock">
          Nothing left to ship — all lines are already in transit or received.
        </Notice>
      ) : null}

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0, maxWidth: 360 }}>
              <label>Carrier</label>
              <input value={carrier} onChange={(e) => setCarrier(e.target.value)} placeholder="e.g. Tiong Nam Logistics" />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Tracking no.</label>
              <input value={trackingNo} onChange={(e) => setTrackingNo(e.target.value)} placeholder="Optional" />
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

      <div className="card">
        <div className="chead">
          <h3>Shipment lines</h3>
          <span className="hint">quantity is capped to remaining</span>
        </div>
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Item</th>
              <th className="amt">Ordered</th>
              <th className="amt">Recv</th>
              <th className="amt">In transit</th>
              <th className="amt">Remaining</th>
              <th className="amt">This shipment</th>
              <th>Lot</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((l) => (
              <tr key={l.itemCode}>
                <td>{l.itemCode}</td>
                <td>{l.description}</td>
                <td className="amt">{fmt(l.ordered)}</td>
                <td className="amt">{fmt(l.received)}</td>
                <td className="amt">{fmt(l.inTransit)}</td>
                <td className="amt">{fmt(l.remaining)}</td>
                <td className="amt" style={{ width: 110 }}>
                  <input
                    type="number"
                    min={0}
                    max={l.remaining}
                    disabled={l.remaining <= 0}
                    value={l.shippedQty}
                    aria-label={`Ship ${l.itemCode}`}
                    onChange={(e) => {
                      const v = Math.min(Math.max(0, Number(e.target.value)), l.remaining);
                      setLines((xs) => xs.map((x) => (x.itemCode === l.itemCode ? { ...x, shippedQty: v } : x)));
                    }}
                  />
                </td>
                <td style={{ width: 130 }}>
                  <input
                    value={l.lotNo}
                    disabled={l.remaining <= 0}
                    placeholder="Lot"
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

      {confirm ? (
        <ConfirmModal
          icon="send"
          title="Submit shipping notice?"
          body={`This notifies the buyer that goods are in transit against ${po.code}. The buyer will receive against this ASN on delivery.`}
          cancelLabel="Keep editing"
          confirmLabel="Submit ASN"
          busy={save.isPending}
          onCancel={() => setConfirm(false)}
          onConfirm={() => save.mutate()}
        />
      ) : null}
    </>
  );
}

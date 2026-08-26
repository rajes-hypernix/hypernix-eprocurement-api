import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getAsn, receiveAsn, type ReceiveLineInput } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { AsnStatusBadge } from "@/components/procurement/badges";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type LineDraft = { itemCode: string; description: string; shippedQty: number; receivedQty: number };

function poLabel(asn: { poCode?: string | null; poId: string }): string {
  return asn.poCode?.trim() || asn.poId;
}

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
  const [confirm, setConfirm] = useState(false);

  const { data: asn, isPending } = useQuery({
    queryKey: ["asn", asnId],
    queryFn: () => getAsn(asnId),
    retry: false,
  });

  if (asn && !hydrated) {
    setHydrated(true);
    setLines(
      (asn.lines ?? []).map((l) => ({
        itemCode: l.itemCode,
        description: l.description ?? "",
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
      setConfirm(false);
      onDone();
    },
    onError: (e: Error) => {
      setConfirm(false);
      onErr(e);
    },
  });

  if (isPending || !asn) return <Spinner label="Loading ASN…" />;

  if (asn.status !== "InTransit") {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={onBack}>
            Deliveries
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
          Deliveries
        </button>{" "}
        <Icon name="chev" size={12} /> Receive {asn.code}
      </div>

      <div className="pagehead">
        <div>
          <h1>Goods receipt — {asn.code}</h1>
          <p>
            {poLabel(asn)}
            {asn.vendorName ? ` · ${asn.vendorName}` : ""} · carrier {asn.carrier || "—"}
          </p>
        </div>
        <button
          type="button"
          className="btn btn-pri"
          disabled={totalReceived <= 0 || receive.isPending}
          onClick={() => setConfirm(true)}
        >
          <Icon name="box" size={15} /> Post receipt
        </button>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Receive lines</h3>
          <span className="hint">received is capped to shipped; under-receipt = Short</span>
        </div>
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Item</th>
              <th className="amt">Shipped</th>
              <th className="amt">Receive now</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((l) => (
              <tr key={l.itemCode}>
                <td>{l.itemCode}</td>
                <td>{l.description || "—"}</td>
                <td className="amt">{fmt(l.shippedQty)}</td>
                <td className="amt" style={{ width: 110 }}>
                  <input
                    type="number"
                    min={0}
                    max={l.shippedQty}
                    value={l.receivedQty}
                    aria-label={`Receive ${l.itemCode}`}
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

      {confirm ? (
        <ConfirmModal
          icon="box"
          title={`Post goods receipt for ${asn.code}?`}
          body={`This records the received quantities as a goods receipt and updates ${poLabel(asn)} and its 3-way match.`}
          cancelLabel="Not yet"
          confirmLabel="Post receipt"
          busy={receive.isPending}
          onCancel={() => setConfirm(false)}
          onConfirm={() => receive.mutate()}
        />
      ) : null}
    </>
  );
}

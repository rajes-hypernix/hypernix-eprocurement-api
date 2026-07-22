import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getPurchaseOrder, submitInvoice, type InvoiceLineInput } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { PoStatusBadge } from "@/components/procurement/badges";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type LineDraft = {
  itemCode: string;
  description: string;
  unitPrice: number;
  billable: number;
  qty: number;
};

export function InvoiceFormPage({
  poId,
  onBack,
  onSaved,
}: {
  poId: string;
  onBack: () => void;
  onSaved: (id: string) => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [invoiceNo, setInvoiceNo] = useState("");
  const [invoiceDate, setInvoiceDate] = useState("");
  const [whtRate, setWhtRate] = useState(0);
  const [lines, setLines] = useState<LineDraft[]>([]);

  const { data: po, isPending } = useQuery({
    queryKey: ["purchase-order", poId],
    queryFn: () => getPurchaseOrder(poId),
  });

  useEffect(() => {
    if (!po) return;
    setLines(
      (po.lines ?? []).map((l) => {
        const billable = Math.max(0, l.receivedQty - l.invoicedQty);
        return {
          itemCode: l.itemCode,
          description: l.description,
          unitPrice: l.unitPrice,
          billable,
          qty: billable,
        };
      }),
    );
  }, [po]);

  const subtotal = useMemo(
    () => lines.reduce((s, l) => s + (l.qty > 0 ? l.qty * l.unitPrice : 0), 0),
    [lines],
  );

  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const save = useMutation({
    mutationFn: () => {
      const payload: InvoiceLineInput[] = lines
        .filter((l) => l.qty > 0)
        .map((l) => ({
          itemCode: l.itemCode,
          qty: Math.min(l.qty, l.billable),
          unitPrice: l.unitPrice,
        }));
      return submitInvoice({
        poId,
        invoiceNo: invoiceNo.trim(),
        date: invoiceDate || null,
        whtRate,
        lines: payload,
      });
    },
    onSuccess: (inv) => {
      void qc.invalidateQueries({ queryKey: ["invoices"] });
      void qc.invalidateQueries({ queryKey: ["purchase-order", poId] });
      void qc.invalidateQueries({ queryKey: ["purchase-orders"] });
      onSaved(inv.id);
    },
    onError: onErr,
  });

  if (isPending || !po) return <Spinner label="Loading PO…" />;

  const billableLines = lines.some((l) => l.billable > 0);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Invoices
        </button>{" "}
        <Icon name="chev" size={12} /> Submit · {po.code}
      </div>

      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            Submit invoice · {po.code} <PoStatusBadge status={po.status} />
          </h1>
          <p>Bill received quantities not yet invoiced on this PO.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      {!billableLines ? (
        <Notice tone="warn" icon="flag">
          No billable quantities — received qty must exceed already invoiced qty per line.
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Invoice details</h3>
        </div>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Invoice no.</label>
              <input value={invoiceNo} onChange={(e) => setInvoiceNo(e.target.value)} placeholder="Vendor invoice ref" />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Invoice date</label>
              <input type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} />
            </div>
            <div className="field" style={{ margin: 0, maxWidth: 120 }}>
              <label>WHT rate (%)</label>
              <input
                type="number"
                min={0}
                step={0.01}
                value={whtRate}
                onChange={(e) => setWhtRate(Number(e.target.value))}
              />
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Billable lines</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Description</th>
                <th className="amt">Billable</th>
                <th className="amt">Bill qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Line total</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l) => (
                <tr key={l.itemCode}>
                  <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                  <td>{l.description}</td>
                  <td className="amt">{fmt(l.billable)}</td>
                  <td className="amt">
                    <input
                      type="number"
                      style={{ width: 80 }}
                      min={0}
                      max={l.billable}
                      disabled={l.billable <= 0}
                      value={l.qty}
                      onChange={(e) => {
                        const v = Math.min(Math.max(0, Number(e.target.value)), l.billable);
                        setLines((xs) => xs.map((x) => (x.itemCode === l.itemCode ? { ...x, qty: v } : x)));
                      }}
                    />
                  </td>
                  <td className="amt">{fmt(l.unitPrice)}</td>
                  <td className="amt">{fmt(l.qty * l.unitPrice)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p style={{ fontWeight: 700, marginTop: 12, marginBottom: 0 }}>Subtotal: {fmt(subtotal)}</p>
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
          disabled={!billableLines || !invoiceNo.trim() || subtotal <= 0 || save.isPending}
          onClick={() => save.mutate()}
        >
          Submit invoice
        </button>
      </div>
    </>
  );
}

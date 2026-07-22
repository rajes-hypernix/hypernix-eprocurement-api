import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { approveInvoice, getInvoice, resolveInvoiceException } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { InvoiceStatusBadge } from "@/components/procurement/badges";
import { fmt, dateMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

export function InvoiceDetailPage({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient();
  const { isVendor } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [confirmApprove, setConfirmApprove] = useState(false);
  const [confirmResolve, setConfirmResolve] = useState(false);

  const { data: invoice, isPending } = useQuery({
    queryKey: ["invoice", id],
    queryFn: () => getInvoice(id),
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["invoice", id] });
    void qc.invalidateQueries({ queryKey: ["invoices"] });
    void qc.invalidateQueries({ queryKey: ["purchase-orders"] });
  };
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const approve = useMutation({
    mutationFn: () => approveInvoice(id),
    onSuccess: () => {
      setConfirmApprove(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmApprove(false);
      onErr(e);
    },
  });

  const resolve = useMutation({
    mutationFn: () => resolveInvoiceException(id),
    onSuccess: () => {
      setConfirmResolve(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmResolve(false);
      onErr(e);
    },
  });

  if (isPending || !invoice) return <Spinner label="Loading invoice…" />;

  const canApprove = !isVendor && invoice.status === "Submitted";
  const canResolve = !isVendor && invoice.status === "Exception";

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Invoices
        </button>{" "}
        <Icon name="chev" size={12} /> {invoice.code}
      </div>

      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {invoice.invoiceNo} <InvoiceStatusBadge status={invoice.status} />
          </h1>
          <p>
            {invoice.code} · PO {shortId(invoice.poId)}
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      {invoice.status === "Exception" && invoice.exceptionReason ? (
        <Notice tone="warn" icon="flag">
          Exception: {invoice.exceptionReason}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Summary</h3>
        </div>
        <div className="cbody">
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 12 }}>
            <div>
              <div className="hint">Invoice date</div>
              <div>{dateMY(invoice.invoiceDate)}</div>
            </div>
            <div>
              <div className="hint">Subtotal</div>
              <div>{fmt(invoice.subtotal)}</div>
            </div>
            <div>
              <div className="hint">SST ({invoice.sstRate}%)</div>
              <div>{fmt(invoice.sstAmount)}</div>
            </div>
            <div>
              <div className="hint">WHT ({invoice.whtRate}%)</div>
              <div>{fmt(invoice.whtAmount)}</div>
            </div>
            <div>
              <div className="hint">Total</div>
              <div style={{ fontWeight: 700, fontSize: 16 }}>{fmt(invoice.total)}</div>
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
                <th className="amt">Qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Line total</th>
              </tr>
            </thead>
            <tbody>
              {(invoice.lines ?? []).map((l) => (
                <tr key={l.id}>
                  <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                  <td className="amt">{fmt(l.qty)}</td>
                  <td className="amt">{fmt(l.unitPrice)}</td>
                  <td className="amt">{fmt(l.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {!isVendor && (canResolve || canApprove) ? (
        <div className="actionbar" style={{ marginTop: 14 }}>
          <div className="spacer" style={{ flex: 1 }} />
          {canResolve ? (
            <button type="button" className="btn btn-out" onClick={() => setConfirmResolve(true)}>
              Resolve exception
            </button>
          ) : null}
          {canApprove ? (
            <button type="button" className="btn btn-pri" onClick={() => setConfirmApprove(true)}>
              Approve invoice
            </button>
          ) : null}
        </div>
      ) : null}

      {confirmApprove ? (
        <ConfirmModal
          title="Approve invoice"
          icon="check"
          body="This approves the invoice for payment processing."
          confirmLabel="Approve"
          busy={approve.isPending}
          onCancel={() => setConfirmApprove(false)}
          onConfirm={() => approve.mutate()}
        />
      ) : null}
      {confirmResolve ? (
        <ConfirmModal
          title="Resolve exception"
          icon="edit"
          body="Mark the invoice exception as resolved. You can approve it separately once ready."
          confirmLabel="Resolve"
          busy={resolve.isPending}
          onCancel={() => setConfirmResolve(false)}
          onConfirm={() => resolve.mutate()}
        />
      ) : null}
    </>
  );
}

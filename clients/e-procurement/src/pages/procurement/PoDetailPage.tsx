import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  acknowledgePurchaseOrder,
  getPurchaseOrder,
  listAsns,
  listInvoices,
  type PoLineDto,
  type PurchaseOrderDto,
} from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { CustomFieldsSection } from "@/components/customfields/CustomFieldsSection";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { ConfirmModal, EmptyState, Notice, Spinner } from "@/components/ui";
import { AsnStatusBadge, InvoiceStatusBadge, PoStatusBadge } from "@/components/procurement/badges";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt, dateMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

function openQty(line: PoLineDto): number {
  return line.openQty ?? Math.max(0, line.qty - line.receivedQty);
}

function shipToSummary(po: PurchaseOrderDto): string {
  if (po.shipToAdhoc) return po.shipToAdhoc;
  if (po.shipToLocationId) return "Set (location address)";
  return "Not set";
}

function incotermSummary(po: PurchaseOrderDto): string {
  const composed = `${po.incotermCode ?? ""} ${po.incotermSuffix ?? ""}`.trim();
  return composed || "—";
}

/** Fulfilment surface (`/pos/detail/:id`; vendor landing at `/pos/:id`). */
export function PoDetailPage({
  id,
  onBack,
  onNavigate,
  onOpenForm,
}: {
  id: string;
  onBack: () => void;
  onNavigate: (key: string) => void;
  onOpenForm?: () => void;
}) {
  const qc = useQueryClient();
  const { isVendor } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [confirmAck, setConfirmAck] = useState(false);

  const { data: po, isPending, isError } = useQuery({
    queryKey: ["purchase-order", id],
    queryFn: () => getPurchaseOrder(id),
    retry: false,
  });
  const { data: asns } = useQuery({
    queryKey: ["asns", id],
    queryFn: () => listAsns(id),
    enabled: Boolean(po),
  });
  const { data: allInvoices } = useQuery({
    queryKey: ["invoices"],
    queryFn: listInvoices,
    enabled: Boolean(po),
  });

  const invoices = useMemo(() => (allInvoices ?? []).filter((i) => i.poId === id), [allInvoices, id]);
  const inTransitAsns = useMemo(() => (asns ?? []).filter((a) => a.status === "InTransit"), [asns]);

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["purchase-order", id] });
    void qc.invalidateQueries({ queryKey: ["asns", id] });
    void qc.invalidateQueries({ queryKey: ["invoices"] });
    void qc.invalidateQueries({ queryKey: ["purchase-orders"] });
  };

  const acknowledge = useMutation({
    mutationFn: () => acknowledgePurchaseOrder(id),
    onSuccess: () => {
      setConfirmAck(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmAck(false);
      setErr(e instanceof ApiRequestError ? e.message : e.message);
    },
  });

  if (isPending) return <Spinner label="Loading purchase order…" />;

  if (isError || !po) {
    return (
      <>
        <div className="crumb">
          <button type="button" className="lnk" onClick={onBack}>
            Purchase Orders
          </button>
        </div>
        <div className="pagehead">
          <div>
            <h1>Purchase order</h1>
            <p>This purchase order was not found.</p>
          </div>
          <button type="button" className="btn btn-out" onClick={onBack}>
            <Icon name="back" size={15} /> Back
          </button>
        </div>
        <div className="card">
          <div className="cbody">
            <EmptyState>No purchase order for this id.</EmptyState>
          </div>
        </div>
      </>
    );
  }

  const canAck = isVendor && po.status === "Issued";
  const canCreateAsn = isVendor && (po.status === "Acknowledged" || po.status === "PartiallyReceived");
  const canSubmitInvoice = isVendor && po.lines.some((l) => l.receivedQty > l.invoicedQty);
  const subtotal = (po.lines ?? []).reduce((s, l) => s + l.lineTotal, 0);
  const acknowledged = po.status !== "Draft" && po.status !== "Verified" && po.status !== "Issued" && po.status !== "Cancelled";

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Purchase Orders
        </button>{" "}
        <Icon name="chev" size={12} /> {po.code}
      </div>

      <div className="pagehead">
        <div>
          <h1>{po.code}</h1>
          <p>
            {po.vendorName || "Vendor"}
            {po.rfqId ? " · from RFQ" : ""}
            {incotermSummary(po) !== "—" ? ` · ${incotermSummary(po)}` : ""}
          </p>
        </div>
        <div className="actbar">
          {!isVendor && onOpenForm ? (
            <button type="button" className="btn btn-out" onClick={onOpenForm}>
              <Icon name="edit" size={15} /> PO form
            </button>
          ) : null}
          {!isVendor && inTransitAsns.length > 0 ? (
            <button
              type="button"
              className="btn btn-pri"
              onClick={() => onNavigate(`deliveries/receive/${inTransitAsns[0]!.id}`)}
            >
              Receive shipment
            </button>
          ) : null}
          {canAck ? (
            <Gated permission={FshPermissions.purchaseOrders.acknowledge}>
              <button type="button" className="btn btn-pri" onClick={() => setConfirmAck(true)}>
                Acknowledge PO
              </button>
            </Gated>
          ) : null}
          {canCreateAsn ? (
            <button type="button" className="btn btn-out" onClick={() => onNavigate(`deliveries/new/${id}`)}>
              Create shipping notice
            </button>
          ) : null}
          {canSubmitInvoice ? (
            <button type="button" className="btn btn-out" onClick={() => onNavigate(`invoices/new/${id}`)}>
              Submit invoice
            </button>
          ) : null}
        </div>
      </div>

      <div className="ribbon">
        <PoStatusBadge status={po.status} />
        {acknowledged ? (
          <span className="badge b-green" style={{ marginLeft: 6 }}>
            <Icon name="check" size={11} /> Vendor acknowledged
          </span>
        ) : null}
        <span className="hint" style={{ marginLeft: 6 }}>
          Ship-to: <strong>{shipToSummary(po)}</strong>
        </span>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>PO lines</h3>
          <span className="hint">
            Subtotal {po.currency} {fmt(subtotal)}
          </span>
        </div>
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Item</th>
              <th className="amt">Qty</th>
              <th>UoM</th>
              <th className="amt">Unit price</th>
              <th className="amt">Line total</th>
              <th className="amt">Recv</th>
              <th className="amt">Inv</th>
              <th className="amt">Open</th>
            </tr>
          </thead>
          <tbody>
            {(po.lines ?? []).map((l) => (
              <tr key={l.id}>
                <td>{l.itemCode}</td>
                <td>{l.description}</td>
                <td className="amt">{fmt(l.qty)}</td>
                <td>{l.uom}</td>
                <td className="amt">{fmt(l.unitPrice)}</td>
                <td className="amt">{fmt(l.lineTotal)}</td>
                <td className="amt">{fmt(l.receivedQty)}</td>
                <td className="amt">{fmt(l.invoicedQty)}</td>
                <td className="amt">{fmt(openQty(l))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>Deliveries</h3>
          <span className="hint">{(asns ?? []).length} shipping notice(s)</span>
        </div>
        <div className="cbody">
          {(asns ?? []).length === 0 ? (
            <EmptyState>No deliveries yet.</EmptyState>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>ASN</th>
                  <th>Carrier</th>
                  <th>Tracking</th>
                  <th>Status</th>
                  <th>Shipped</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {(asns ?? []).map((a) => (
                  <tr key={a.id} className="drillrow" onClick={() => onNavigate(`deliveries/asn/${a.id}`)}>
                    <td style={{ fontWeight: 700 }}>{a.code}</td>
                    <td>{a.carrier}</td>
                    <td>{a.trackingNo}</td>
                    <td>
                      <AsnStatusBadge status={a.status} />
                    </td>
                    <td>{dateMY(a.shippedDate)}</td>
                    <td className="amt">
                      {!isVendor && a.status === "InTransit" ? (
                        <button
                          type="button"
                          className="btn btn-pri btn-sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            onNavigate(`deliveries/receive/${a.id}`);
                          }}
                        >
                          Receive
                        </button>
                      ) : (
                        <span className="btn btn-ghost btn-sm">
                          View <Icon name="chev" size={13} />
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead">
          <h3>Invoices</h3>
          <span className="hint">{invoices.length} invoice(s)</span>
        </div>
        <div className="cbody">
          {invoices.length === 0 ? (
            <EmptyState>No invoices yet.</EmptyState>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Invoice</th>
                  <th>Supplier ref</th>
                  <th className="amt">Total</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {invoices.map((inv) => (
                  <tr key={inv.id} className="drillrow" onClick={() => onNavigate(`invoices/${inv.id}`)}>
                    <td style={{ fontWeight: 700 }}>{inv.code}</td>
                    <td>{inv.invoiceNo}</td>
                    <td className="amt">{fmt(inv.total)}</td>
                    <td>
                      <InvoiceStatusBadge status={inv.status} />
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        View <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <CustomFieldsSection recordType="PurchaseOrder" recordId={po.id} readOnly />

      {confirmAck ? (
        <ConfirmModal
          title="Acknowledge purchase order"
          icon="check"
          body="Confirm you accept this purchase order and can fulfil it."
          confirmLabel="Acknowledge"
          busy={acknowledge.isPending}
          onCancel={() => setConfirmAck(false)}
          onConfirm={() => acknowledge.mutate()}
        />
      ) : null}
    </>
  );
}

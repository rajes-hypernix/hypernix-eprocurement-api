import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  acknowledgePurchaseOrder,
  getPurchaseOrder,
  issuePurchaseOrder,
  listAsns,
  listInvoices,
} from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { AsnStatusBadge, InvoiceStatusBadge, PoStatusBadge } from "@/components/procurement/badges";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt, dateMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

export function PoDetailPage({
  id,
  onBack,
  onNavigate,
}: {
  id: string;
  onBack: () => void;
  onNavigate: (key: string) => void;
}) {
  const qc = useQueryClient();
  const { isVendor } = useAuth();
  const [err, setErr] = useState<string | null>(null);
  const [confirmIssue, setConfirmIssue] = useState(false);
  const [confirmAck, setConfirmAck] = useState(false);

  const { data: po, isPending } = useQuery({
    queryKey: ["purchase-order", id],
    queryFn: () => getPurchaseOrder(id),
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
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const issue = useMutation({
    mutationFn: () => issuePurchaseOrder(id),
    onSuccess: () => {
      setConfirmIssue(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmIssue(false);
      onErr(e);
    },
  });

  const acknowledge = useMutation({
    mutationFn: () => acknowledgePurchaseOrder(id),
    onSuccess: () => {
      setConfirmAck(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmAck(false);
      onErr(e);
    },
  });

  if (isPending || !po) return <Spinner label="Loading purchase order…" />;

  const canIssue = !isVendor && po.status === "Draft";
  const canAck = isVendor && po.status === "Issued";
  const canCreateAsn = isVendor && (po.status === "Acknowledged" || po.status === "PartiallyReceived");
  const canSubmitInvoice = isVendor && po.lines.some((l) => l.receivedQty > l.invoicedQty);

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
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {po.code} <PoStatusBadge status={po.status} />
          </h1>
          <p>
            Vendor {shortId(po.vendorId)} · {po.currency} {fmt(po.totalValue)}
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
          <h3>Lines</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Description</th>
                <th className="amt">Qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Received</th>
                <th className="amt">Invoiced</th>
              </tr>
            </thead>
            <tbody>
              {(po.lines ?? []).map((l) => (
                <tr key={l.id}>
                  <td style={{ fontWeight: 600 }}>{l.itemCode}</td>
                  <td>{l.description}</td>
                  <td className="amt">
                    {fmt(l.qty)} {l.uom}
                  </td>
                  <td className="amt">{fmt(l.unitPrice)}</td>
                  <td className="amt">{fmt(l.receivedQty)}</td>
                  <td className="amt">{fmt(l.invoicedQty)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Shipping notices</h3>
        </div>
        <div className="cbody">
          {(asns ?? []).length === 0 ? (
            <p className="hint" style={{ margin: 0 }}>
              No ASNs yet.
            </p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
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

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Invoices</h3>
        </div>
        <div className="cbody">
          {invoices.length === 0 ? (
            <p className="hint" style={{ margin: 0 }}>
              No invoices yet.
            </p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Invoice no.</th>
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

      <div className="actionbar" style={{ marginTop: 14 }}>
        <div className="spacer" style={{ flex: 1 }} />
        {canIssue ? (
          <Gated permission={FshPermissions.purchaseOrders.issue}>
            <button type="button" className="btn btn-pri" onClick={() => setConfirmIssue(true)}>
              Issue PO
            </button>
          </Gated>
        ) : null}
        {canAck ? (
          <Gated permission={FshPermissions.purchaseOrders.acknowledge}>
            <button type="button" className="btn btn-pri" onClick={() => setConfirmAck(true)}>
              Acknowledge
            </button>
          </Gated>
        ) : null}
        {canCreateAsn ? (
          <button type="button" className="btn btn-out" onClick={() => onNavigate(`deliveries/new/${id}`)}>
            Create ASN
          </button>
        ) : null}
        {canSubmitInvoice ? (
          <button type="button" className="btn btn-out" onClick={() => onNavigate(`invoices/new/${id}`)}>
            Submit invoice
          </button>
        ) : null}
        {!isVendor && inTransitAsns.length > 0 ? (
          <button type="button" className="btn btn-out" onClick={() => onNavigate(`deliveries/receive/${inTransitAsns[0]!.id}`)}>
            Receive shipment
          </button>
        ) : null}
      </div>

      {confirmIssue ? (
        <ConfirmModal
          title="Issue purchase order"
          icon="send"
          body="This sends the PO to the vendor for acknowledgement."
          confirmLabel="Issue"
          busy={issue.isPending}
          onCancel={() => setConfirmIssue(false)}
          onConfirm={() => issue.mutate()}
        />
      ) : null}
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

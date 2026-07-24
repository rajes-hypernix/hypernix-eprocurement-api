import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  acknowledgePurchaseOrder,
  cancelPurchaseOrder,
  closePurchaseOrder,
  getPurchaseOrder,
  issuePurchaseOrder,
  listAsns,
  listInvoices,
  reopenPurchaseOrderDraft,
  setPurchaseOrderShipTo,
  updatePurchaseOrderLine,
  verifyPurchaseOrder,
  type PoLineDto,
  type PurchaseOrderDto,
} from "@/api/procurement";
import { listLocations } from "@/api/configuration";
import { useAuth } from "@/auth/use-auth";
import { CustomFieldsSection } from "@/components/customfields/CustomFieldsSection";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice, Spinner } from "@/components/ui";
import { AsnStatusBadge, InvoiceStatusBadge, PoStatusBadge } from "@/components/procurement/badges";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt, dateMY, dateTimeMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

const SOURCE_LABEL: Record<string, string> = {
  FromAward: "From award",
  FromRequisition: "From requisition",
  Standalone: "Standalone",
};

function shipToSummary(po: PurchaseOrderDto): string {
  if (po.shipToAdhoc) return po.shipToAdhoc;
  if (po.shipToLocationId) return "Set (location address)";
  return "Not set";
}

function DraftLineRow({
  line,
  onSave,
  saving,
}: {
  line: PoLineDto;
  onSave: (unitPrice: number, priceConfirmed: boolean) => void;
  saving: boolean;
}) {
  const [unitPrice, setUnitPrice] = useState(line.unitPrice);
  const [confirmed, setConfirmed] = useState(line.priceConfirmed);
  const dirty = unitPrice !== line.unitPrice || confirmed !== line.priceConfirmed;

  return (
    <tr>
      <td style={{ fontWeight: 600 }}>{line.itemCode}</td>
      <td>{line.description}</td>
      <td className="amt">
        {fmt(line.qty)} {line.uom}
      </td>
      <td className="amt">
        <input
          type="number"
          min={0.01}
          step="0.01"
          value={unitPrice}
          onChange={(e) => setUnitPrice(Number(e.target.value))}
          style={{ width: 90, textAlign: "right" }}
        />
      </td>
      <td className="amt">
        <label className="hint" style={{ display: "flex", alignItems: "center", gap: 4, justifyContent: "flex-end" }}>
          <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} /> Confirmed
        </label>
      </td>
      <td className="amt">
        <button
          type="button"
          className="btn btn-out btn-sm"
          disabled={!dirty || saving}
          onClick={() => onSave(unitPrice, confirmed)}
        >
          Save
        </button>
      </td>
    </tr>
  );
}

function ShipToEditor({ po, onSaved, onCancel }: { po: PurchaseOrderDto; onSaved: () => void; onCancel: () => void }) {
  const { data: locations } = useQuery({ queryKey: ["locations"], queryFn: () => listLocations(true) });
  const [mode, setMode] = useState<"location" | "adhoc">(po.shipToLocationId ? "location" : "adhoc");
  const [locationId, setLocationId] = useState(po.shipToLocationId ?? "");
  const [addressId, setAddressId] = useState(po.shipToAddressId ?? "");
  const [adhoc, setAdhoc] = useState(po.shipToAdhoc ?? "");
  const [err, setErr] = useState<string | null>(null);

  const location = (locations ?? []).find((l) => l.id === locationId);

  const save = useMutation({
    mutationFn: () =>
      mode === "location"
        ? setPurchaseOrderShipTo(po.id, { locationId, addressId, adhoc: null })
        : setPurchaseOrderShipTo(po.id, { locationId: null, addressId: null, adhoc }),
    onSuccess: onSaved,
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  return (
    <div style={{ marginTop: 10 }}>
      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}
      <div style={{ display: "flex", gap: 16, marginBottom: 10 }}>
        <label className="hint" style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <input type="radio" checked={mode === "location"} onChange={() => setMode("location")} /> Location address
        </label>
        <label className="hint" style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <input type="radio" checked={mode === "adhoc"} onChange={() => setMode("adhoc")} /> Ad-hoc address
        </label>
      </div>
      {mode === "location" ? (
        <div style={{ display: "flex", gap: 12 }}>
          <div className="field" style={{ flex: 1 }}>
            <label>Location</label>
            <select
              value={locationId}
              onChange={(e) => {
                setLocationId(e.target.value);
                setAddressId("");
              }}
            >
              <option value="">Select…</option>
              {(locations ?? []).map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field" style={{ flex: 1 }}>
            <label>Address</label>
            <select value={addressId} onChange={(e) => setAddressId(e.target.value)} disabled={!location}>
              <option value="">Select…</option>
              {(location?.addresses ?? []).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.label} — {a.line1}, {a.city}
                </option>
              ))}
            </select>
          </div>
        </div>
      ) : (
        <div className="field">
          <label>Ad-hoc address</label>
          <textarea value={adhoc} onChange={(e) => setAdhoc(e.target.value)} rows={3} maxLength={400} />
        </div>
      )}
      <div style={{ display: "flex", gap: 8, marginTop: 10 }}>
        <button type="button" className="btn btn-out btn-sm" onClick={onCancel}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-pri btn-sm"
          disabled={save.isPending || (mode === "location" ? !locationId || !addressId : !adhoc.trim())}
          onClick={() => save.mutate()}
        >
          Save ship-to
        </button>
      </div>
    </div>
  );
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
  const [confirmVerify, setConfirmVerify] = useState(false);
  const [confirmReopen, setConfirmReopen] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [confirmClose, setConfirmClose] = useState(false);
  const [editingShipTo, setEditingShipTo] = useState(false);

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

  const verify = useMutation({
    mutationFn: () => verifyPurchaseOrder(id),
    onSuccess: () => {
      setConfirmVerify(false);
      setErr(null);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmVerify(false);
      onErr(e);
    },
  });

  const reopenDraft = useMutation({
    mutationFn: () => reopenPurchaseOrderDraft(id),
    onSuccess: () => {
      setConfirmReopen(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmReopen(false);
      onErr(e);
    },
  });

  const cancel = useMutation({
    mutationFn: () => cancelPurchaseOrder(id, "Cancelled from PO detail"),
    onSuccess: () => {
      setConfirmCancel(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmCancel(false);
      onErr(e);
    },
  });

  const close = useMutation({
    mutationFn: () => closePurchaseOrder(id),
    onSuccess: () => {
      setConfirmClose(false);
      refresh();
    },
    onError: (e: Error) => {
      setConfirmClose(false);
      onErr(e);
    },
  });

  const updateLine = useMutation({
    mutationFn: (v: { lineId: string; unitPrice: number; priceConfirmed: boolean }) =>
      updatePurchaseOrderLine(id, v.lineId, { unitPrice: v.unitPrice, priceConfirmed: v.priceConfirmed }),
    onSuccess: () => {
      setErr(null);
      refresh();
    },
    onError: onErr,
  });

  if (isPending || !po) return <Spinner label="Loading purchase order…" />;

  const isDraft = po.status === "Draft";
  const canVerify = !isVendor && isDraft;
  const canReopenDraft = !isVendor && po.status === "Verified";
  const canIssue = !isVendor && po.status === "Verified";
  const canAck = isVendor && po.status === "Issued";
  const canCancel = !isVendor && (isDraft || po.status === "Verified");
  const canClose = !isVendor && ["Matched", "Received", "Discrepancy"].includes(po.status);
  const canEditShipTo = !isVendor && (isDraft || po.status === "Verified");
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
            Vendor {shortId(po.vendorId)} · {po.currency} {fmt(po.totalValue)} · {SOURCE_LABEL[po.sourceKind] ?? po.sourceKind}
            {po.verifiedUtc ? ` · Verified ${dateTimeMY(po.verifiedUtc)}` : ""}
            {po.issuedUtc ? ` · Issued ${dateTimeMY(po.issuedUtc)}` : ""}
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
                <th className="amt">{isDraft && !isVendor ? "Price" : "Received"}</th>
                <th className="amt">{isDraft && !isVendor ? "" : "Invoiced"}</th>
              </tr>
            </thead>
            <tbody>
              {isDraft && !isVendor
                ? (po.lines ?? []).map((l) => (
                    <DraftLineRow
                      key={l.id}
                      line={l}
                      saving={updateLine.isPending}
                      onSave={(unitPrice, priceConfirmed) => updateLine.mutate({ lineId: l.id, unitPrice, priceConfirmed })}
                    />
                  ))
                : (po.lines ?? []).map((l) => (
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
          <h3>Shipping</h3>
          {canEditShipTo && !editingShipTo ? (
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setEditingShipTo(true)}>
              Edit
            </button>
          ) : null}
        </div>
        <div className="cbody">
          {editingShipTo ? (
            <ShipToEditor
              po={po}
              onSaved={() => {
                setEditingShipTo(false);
                refresh();
              }}
              onCancel={() => setEditingShipTo(false)}
            />
          ) : (
            <p className="hint" style={{ margin: 0 }}>
              Ship-to: {shipToSummary(po)}
            </p>
          )}
        </div>
      </div>

      <CustomFieldsSection recordType="PurchaseOrder" recordId={po.id} readOnly={!isDraft || isVendor} />

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
        {canCancel ? (
          <Gated permission={FshPermissions.purchaseOrders.cancel}>
            <button type="button" className="btn btn-out" onClick={() => setConfirmCancel(true)}>
              Cancel PO
            </button>
          </Gated>
        ) : null}
        {canReopenDraft ? (
          <Gated permission={FshPermissions.purchaseOrders.issue}>
            <button type="button" className="btn btn-out" onClick={() => setConfirmReopen(true)}>
              Reopen to Draft
            </button>
          </Gated>
        ) : null}
        {canVerify ? (
          <Gated permission={FshPermissions.purchaseOrders.issue}>
            <button type="button" className="btn btn-out" onClick={() => setConfirmVerify(true)}>
              Verify
            </button>
          </Gated>
        ) : null}
        {canIssue ? (
          <Gated permission={FshPermissions.purchaseOrders.issue}>
            <button type="button" className="btn btn-pri" onClick={() => setConfirmIssue(true)}>
              Issue PO
            </button>
          </Gated>
        ) : null}
        {canClose ? (
          <Gated permission={FshPermissions.purchaseOrders.close}>
            <button type="button" className="btn btn-out" onClick={() => setConfirmClose(true)}>
              Close PO
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

      {confirmVerify ? (
        <ConfirmModal
          title="Verify purchase order"
          icon="check"
          body="Checks that a ship-to address is set and every line's price is confirmed. Fixes any gaps found before allowing Issue."
          confirmLabel="Verify"
          busy={verify.isPending}
          onCancel={() => setConfirmVerify(false)}
          onConfirm={() => verify.mutate()}
        />
      ) : null}
      {confirmReopen ? (
        <ConfirmModal
          title="Reopen to Draft"
          icon="box"
          body="Returns this PO to Draft so its lines and ship-to can be edited again."
          confirmLabel="Reopen"
          busy={reopenDraft.isPending}
          onCancel={() => setConfirmReopen(false)}
          onConfirm={() => reopenDraft.mutate()}
        />
      ) : null}
      {confirmCancel ? (
        <ConfirmModal
          title="Cancel purchase order"
          icon="x"
          danger
          body={
            po.sourceKind === "FromRequisition"
              ? "This is irreversible. Any quantity reserved against the source requisition will be released back."
              : "This is irreversible."
          }
          confirmLabel="Cancel PO"
          busy={cancel.isPending}
          onCancel={() => setConfirmCancel(false)}
          onConfirm={() => cancel.mutate()}
        />
      ) : null}
      {confirmClose ? (
        <ConfirmModal
          title="Close purchase order"
          icon="check"
          body="Marks this PO as administratively done. This does not change anything else about it."
          confirmLabel="Close"
          busy={close.isPending}
          onCancel={() => setConfirmClose(false)}
          onConfirm={() => close.mutate()}
        />
      ) : null}
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

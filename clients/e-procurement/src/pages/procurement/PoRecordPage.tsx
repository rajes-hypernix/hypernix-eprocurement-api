import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cancelPurchaseOrder,
  closePurchaseOrder,
  getPurchaseOrder,
  issuePurchaseOrder,
  reopenPurchaseOrderDraft,
  setPurchaseOrderShipTo,
  updatePurchaseOrderLine,
  verifyPurchaseOrder,
  type PoLineDto,
  type PurchaseOrderDto,
} from "@/api/procurement";
import { listLocations } from "@/api/configuration";
import { CustomFieldsSection } from "@/components/customfields/CustomFieldsSection";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { ConfirmModal, EmptyState, Notice, Spinner } from "@/components/ui";
import { PoStatusBadge } from "@/components/procurement/badges";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt, dateMY, dateTimeMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

const SOURCE_LABEL: Record<string, string> = {
  FromAward: "From award",
  FromRequisition: "From requisition",
  Standalone: "Standalone",
};

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
      <td className="amt">{fmt(line.qty * unitPrice)}</td>
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

/** Buyer record surface at `/pos/:id` — header, lines, verify/issue. Fulfilment lives at `/pos/detail/:id`. */
export function PoRecordPage({
  id,
  onBack,
  onOpenFulfilment,
}: {
  id: string;
  onBack: () => void;
  onOpenFulfilment: () => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [confirmIssue, setConfirmIssue] = useState(false);
  const [confirmVerify, setConfirmVerify] = useState(false);
  const [confirmReopen, setConfirmReopen] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [confirmClose, setConfirmClose] = useState(false);
  const [editingShipTo, setEditingShipTo] = useState(false);

  const { data: po, isPending, isError } = useQuery({
    queryKey: ["purchase-order", id],
    queryFn: () => getPurchaseOrder(id),
    retry: false,
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ["purchase-order", id] });
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
    mutationFn: () => cancelPurchaseOrder(id, "Cancelled from PO record"),
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

  const isDraft = po.status === "Draft";
  const canVerify = isDraft;
  const canReopenDraft = po.status === "Verified";
  const canIssue = po.status === "Verified";
  const canCancel = isDraft || po.status === "Verified";
  const canClose = ["Matched", "Received", "Discrepancy"].includes(po.status);
  const canEditShipTo = isDraft || po.status === "Verified";
  const subtotal = (po.lines ?? []).reduce((s, l) => s + l.lineTotal, 0);

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
            {po.vendorName || "Vendor"} · {po.currency} {fmt(po.totalValue)} · {SOURCE_LABEL[po.sourceKind] ?? po.sourceKind}
            {po.verifiedUtc ? ` · Verified ${dateTimeMY(po.verifiedUtc)}` : ""}
            {po.issuedUtc ? ` · Issued ${dateTimeMY(po.issuedUtc)}` : ""}
          </p>
        </div>
        <div className="actbar">
          <button type="button" className="btn btn-out" onClick={onOpenFulfilment}>
            Fulfilment &amp; matching
          </button>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Header</h3>
        </div>
        <div className="cbody">
          <div className="grid g3">
            <div className="field">
              <label>Vendor</label>
              <div>{po.vendorName || "—"}</div>
            </div>
            <div className="field">
              <label>Currency</label>
              <div>{po.currency}</div>
            </div>
            <div className="field">
              <label>Source</label>
              <div>{SOURCE_LABEL[po.sourceKind] ?? po.sourceKind}</div>
            </div>
            <div className="field">
              <label>Required date</label>
              <div>{dateMY(po.requiredDate)}</div>
            </div>
            <div className="field">
              <label>Delivery date</label>
              <div>{dateMY(po.deliveryDate)}</div>
            </div>
            <div className="field">
              <label>Vendor ref</label>
              <div>{po.vendorRef || "—"}</div>
            </div>
            <div className="field" style={{ gridColumn: "1 / -1" }}>
              <label>Memo</label>
              <div>{po.memo || "—"}</div>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>PO lines</h3>
          <span className="hint">Subtotal {po.currency} {fmt(subtotal)}</span>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Description</th>
                <th className="amt">Qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Amount</th>
                {isDraft ? <th className="amt">Price</th> : (
                  <>
                    <th className="amt">Received</th>
                    <th className="amt">Billed</th>
                    <th className="amt">Returned</th>
                    <th className="amt">Open</th>
                  </>
                )}
                {isDraft ? <th /> : null}
              </tr>
            </thead>
            <tbody>
              {isDraft
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
                      <td className="amt">{fmt(l.lineTotal)}</td>
                      <td className="amt">{fmt(l.receivedQty)}</td>
                      <td className="amt">{fmt(l.invoicedQty)}</td>
                      <td className="amt">—</td>
                      <td className="amt">{fmt(openQty(l))}</td>
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
            <div className="grid g2">
              <div className="field">
                <label>Incoterm</label>
                <div>{incotermSummary(po)}</div>
              </div>
              <div className="field">
                <label>Ship-to</label>
                <div>{shipToSummary(po)}</div>
              </div>
            </div>
          )}
        </div>
      </div>

      <CustomFieldsSection recordType="PurchaseOrder" recordId={po.id} readOnly={!isDraft} />

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
          body={`Vendor: ${po.vendorName || "—"}. This sends the PO to the vendor for acknowledgement.`}
          confirmLabel="Issue"
          busy={issue.isPending}
          onCancel={() => setConfirmIssue(false)}
          onConfirm={() => issue.mutate()}
        />
      ) : null}
    </>
  );
}

import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listPurchaseOrders } from "@/api/procurement";
import { getViewFields, listViews, rowId, runView, type SavedViewDto } from "@/api/views";
import { useAuth } from "@/auth/use-auth";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { PoStatusBadge } from "@/components/procurement/badges";
import { ViewBuilder, ViewPicker } from "@/components/views/SavedViewControls";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt, dateMY } from "@/lib/format";

const OPEN_STATUSES = new Set(["Issued", "Acknowledged", "PartiallyReceived"]);
const CLOSED_STATUSES = new Set(["Received", "Matched", "Closed"]);

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

type Tab = "all" | "open" | "exceptions" | "draft" | "closed";

export function PoListPage({
  onOpen,
  onNavigate,
  onNewStandalone,
}: {
  onOpen: (id: string) => void;
  onNavigate: (key: string) => void;
  onNewStandalone: () => void;
}) {
  const { isVendor } = useAuth();
  const [tab, setTab] = useState<Tab>("all");
  const [q, setQ] = useState("");

  const { data, isPending } = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
  });

  const items = data ?? [];

  const kpis = useMemo(() => {
    const open = items.filter((p) => OPEN_STATUSES.has(p.status)).length;
    const draft = items.filter((p) => p.status === "Draft").length;
    const exceptions = items.filter((p) => p.status === "Discrepancy").length;
    const openValue = items.filter((p) => OPEN_STATUSES.has(p.status)).reduce((s, p) => s + p.totalValue, 0);
    return { open, draft, exceptions, openValue };
  }, [items]);

  const rows = useMemo(() => {
    return items.filter((p) => {
      if (tab === "open" && !OPEN_STATUSES.has(p.status)) return false;
      if (tab === "exceptions" && p.status !== "Discrepancy") return false;
      if (tab === "draft" && p.status !== "Draft") return false;
      if (tab === "closed" && !CLOSED_STATUSES.has(p.status)) return false;
      if (q.trim()) {
        const hay = `${p.code} ${p.vendorId} ${p.rfqId ?? ""}`.toLowerCase();
        if (!hay.includes(q.trim().toLowerCase())) return false;
      }
      return true;
    });
  }, [items, tab, q]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Purchase Orders</h1>
          <p>
            {isVendor
              ? "Acknowledge issued POs and track deliveries and invoices for your company."
              : "Issue POs to vendors, track acknowledgement, receipts, and invoice matching."}
          </p>
        </div>
        {!isVendor ? (
          <div style={{ display: "flex", gap: 8 }}>
            <Gated permission={FshPermissions.purchaseOrders.createFromRequisition}>
              <button type="button" className="btn btn-out btn-sm" onClick={() => onNavigate("order-builder")}>
                <Icon name="box" size={15} /> Order builder
              </button>
            </Gated>
            <Gated permission={FshPermissions.purchaseOrders.createStandalone}>
              <button type="button" className="btn btn-pri btn-sm" onClick={onNewStandalone}>
                <Icon name="plus" size={15} /> New standalone PO
              </button>
            </Gated>
          </div>
        ) : null}
      </div>

      {!isVendor ? (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 12, marginBottom: 14 }}>
          <div className="card">
            <div className="cbody" style={{ padding: "14px 16px" }}>
              <div className="hint">Open POs</div>
              <div style={{ fontSize: 22, fontWeight: 700 }}>{kpis.open}</div>
            </div>
          </div>
          <div className="card">
            <div className="cbody" style={{ padding: "14px 16px" }}>
              <div className="hint">Draft</div>
              <div style={{ fontSize: 22, fontWeight: 700 }}>{kpis.draft}</div>
            </div>
          </div>
          <div className="card">
            <div className="cbody" style={{ padding: "14px 16px" }}>
              <div className="hint">Exceptions</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: kpis.exceptions ? "var(--red)" : undefined }}>{kpis.exceptions}</div>
            </div>
          </div>
          <div className="card">
            <div className="cbody" style={{ padding: "14px 16px" }}>
              <div className="hint">Open value</div>
              <div style={{ fontSize: 22, fontWeight: 700 }}>{fmt(kpis.openValue)}</div>
            </div>
          </div>
        </div>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search code / vendor</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="PO-2026-…" />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>View</label>
              <select value={tab} onChange={(e) => setTab(e.target.value as Tab)}>
                <option value="all">All</option>
                <option value="open">Open</option>
                <option value="exceptions">Exceptions</option>
                <option value="draft">Draft</option>
                <option value="closed">Closed</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading purchase orders…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                {!isVendor ? <th>Vendor</th> : null}
                <th className="amt">Total</th>
                <th>Status</th>
                <th>Created</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <tr key={p.id} className="drillrow" onClick={() => onOpen(p.id)}>
                  <td style={{ fontWeight: 700 }}>{p.code}</td>
                  {!isVendor ? <td>{shortId(p.vendorId)}</td> : null}
                  <td className="amt">
                    {p.currency} {fmt(p.totalValue)}
                  </td>
                  <td>
                    <PoStatusBadge status={p.status} />
                  </td>
                  <td>{dateMY(p.createdUtc)}</td>
                  <td className="amt">
                    <span className="btn btn-ghost btn-sm">
                      Open <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={isVendor ? 5 : 6}>
                    <EmptyState>No purchase orders match the filter.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

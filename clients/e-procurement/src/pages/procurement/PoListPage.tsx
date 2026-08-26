import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listPurchaseOrders } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Gated } from "@/components/Gated";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { PoStatusBadge } from "@/components/procurement/badges";
import { PoFromPrModal } from "@/pages/procurement/PoFromPrModal";
import { FshPermissions } from "@/lib/fsh-permissions";
import { fmt } from "@/lib/format";

const AWAITING = new Set(["Issued", "Acknowledged", "PartiallyReceived"]);
const OPEN_VALUE_EXCLUDE = new Set(["Draft", "Closed", "Cancelled"]);

function vendorLabel(p: { vendorName?: string; vendorId: string }): string {
  return p.vendorName?.trim() || p.vendorId;
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
  const [fromPrOpen, setFromPrOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  const { data, isPending } = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
  });

  const items = data ?? [];
  const exceptions = items.filter((p) => p.status === "Discrepancy").length;
  const openVal = items.filter((p) => !OPEN_VALUE_EXCLUDE.has(p.status)).reduce((a, p) => a + p.totalValue, 0);
  const awaiting = items.filter((p) => AWAITING.has(p.status)).length;

  const rows = useMemo(() => {
    return items.filter((p) => {
      if (tab === "open" && !AWAITING.has(p.status)) return false;
      if (tab === "exceptions" && p.status !== "Discrepancy") return false;
      if (tab === "draft" && p.status !== "Draft") return false;
      if (tab === "closed" && !["Received", "Matched", "Closed"].includes(p.status)) return false;
      if (q.trim()) {
        const hay = `${p.code} ${vendorLabel(p)} ${p.rfqId ?? ""}`.toLowerCase();
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
          <p>PO lifecycle and 3-way matching — purchase order vs goods receipt vs supplier invoice.</p>
        </div>
        {!isVendor ? (
          <div style={{ position: "relative" }}>
            <button type="button" className="btn btn-pri" onClick={() => setMenuOpen((o) => !o)}>
              + New PO <span aria-hidden>▾</span>
            </button>
            {menuOpen ? (
              <div className="qmenu" role="menu" aria-label="New PO options" style={{ right: 0, left: "auto" }}>
                <Gated permission={FshPermissions.purchaseOrders.createFromRequisition}>
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setMenuOpen(false);
                      setFromPrOpen(true);
                    }}
                  >
                    From requisition…
                  </button>
                </Gated>
                <Gated permission={FshPermissions.purchaseOrders.createFromRequisition}>
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setMenuOpen(false);
                      onNavigate("order-builder");
                    }}
                  >
                    Build from requisitions…
                  </button>
                </Gated>
                <Gated permission={FshPermissions.purchaseOrders.createStandalone}>
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setMenuOpen(false);
                      onNewStandalone();
                    }}
                  >
                    Standalone
                  </button>
                </Gated>
              </div>
            ) : null}
          </div>
        ) : null}
      </div>

      {exceptions > 0 ? (
        <div className="ribbon ribbon-warn" style={{ marginBottom: 12 }}>
          <strong>
            {exceptions} invoice discrepancy{exceptions === 1 ? "" : "(ies)"} pending exception review.
          </strong>
        </div>
      ) : null}

      {!isVendor ? (
        <div className="grid g4" style={{ marginBottom: 14 }}>
          <div className="card stat">
            <div className="lbl">Total POs</div>
            <div className="num">{items.length}</div>
            <div className="sub">all statuses</div>
          </div>
          <div className="card stat">
            <div className="lbl">Open PO value</div>
            <div className="num" style={{ fontSize: 20 }}>
              RM {fmt(openVal)}
            </div>
            <div className="sub">issued, not closed</div>
          </div>
          <div className="card stat">
            <div className="lbl">Awaiting receipt</div>
            <div className="num">{awaiting}</div>
            <div className="sub">in delivery</div>
          </div>
          <div className="card stat">
            <div className="lbl">Exceptions</div>
            <div className="num" style={{ color: exceptions ? "var(--red)" : undefined }}>
              {exceptions}
            </div>
            <div className="sub">invoice variance</div>
          </div>
        </div>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>PO</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="PO number or vendor…" />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 180 }}>
              <label>Status</label>
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
                <th>PO</th>
                {!isVendor ? <th>Vendor</th> : null}
                <th>From RFQ</th>
                <th className="amt">Value</th>
                <th>Goods receipt</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => {
                const totalQty = p.totalQty ?? 0;
                const receivedQty = p.receivedQty ?? 0;
                const pct = totalQty > 0 ? Math.round((receivedQty / totalQty) * 100) : 0;
                return (
                  <tr key={p.id} className="drillrow" onClick={() => onOpen(p.id)}>
                    <td style={{ fontWeight: 700, color: "var(--teal)" }}>{p.code}</td>
                    {!isVendor ? <td>{vendorLabel(p)}</td> : null}
                    <td>
                      <span style={{ color: "var(--teal)" }}>{p.rfqId ? "RFQ" : "—"}</span>
                    </td>
                    <td className="amt">
                      {p.currency} {fmt(p.totalValue)}
                    </td>
                    <td>
                      <div style={{ minWidth: 130 }}>
                        <div className="pbar">
                          <i style={{ width: `${pct}%` }} />
                        </div>
                        <div className="hint" style={{ marginTop: 3 }}>
                          {fmt(receivedQty)}/{fmt(totalQty)} received
                        </div>
                      </div>
                    </td>
                    <td>
                      <PoStatusBadge status={p.status} />
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Open <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={isVendor ? 6 : 7}>
                    <EmptyState>No POs match these filters.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>

      {fromPrOpen ? (
        <PoFromPrModal
          onClose={() => setFromPrOpen(false)}
          onCreated={(id) => {
            setFromPrOpen(false);
            onOpen(id);
          }}
        />
      ) : null}
    </>
  );
}

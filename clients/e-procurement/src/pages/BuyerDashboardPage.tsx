import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listRequisitions, listRfqs } from "@/api/sourcing";
import { listAsns, listPurchaseOrders, listInvoices } from "@/api/procurement";
import { getUnreadCount } from "@/api/notifications";
import { Icon } from "@/components/Icon";
import { Spinner } from "@/components/ui";
import { FshPermissions } from "@/lib/fsh-permissions";
import { useAuth } from "@/auth/use-auth";

type Tile = {
  key: string;
  label: string;
  value: number | string;
  hint: string;
  tone: string;
  href: string;
  perm?: string;
};

/** Buyer home — KPI shortcuts over existing list APIs (not the old arrangeable portlets). */
export function BuyerDashboardPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const can = (p?: string) => !p || granted.includes(p);

  const reqs = useQuery({
    queryKey: ["requisitions"],
    queryFn: listRequisitions,
    enabled: can(FshPermissions.requisitions.view),
  });
  const rfqs = useQuery({
    queryKey: ["rfqs"],
    queryFn: listRfqs,
    enabled: can(FshPermissions.rfqs.view),
  });
  const pos = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
    enabled: can(FshPermissions.purchaseOrders.view),
  });
  const asns = useQuery({
    queryKey: ["asns"],
    queryFn: () => listAsns(),
    enabled: can(FshPermissions.deliveries.view),
  });
  const invoices = useQuery({
    queryKey: ["invoices"],
    queryFn: listInvoices,
    enabled: can(FshPermissions.invoices.view),
  });
  const unread = useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: getUnreadCount,
    enabled: can(FshPermissions.notifications.view),
  });

  const loading =
    (reqs.isPending && can(FshPermissions.requisitions.view)) ||
    (rfqs.isPending && can(FshPermissions.rfqs.view)) ||
    (pos.isPending && can(FshPermissions.purchaseOrders.view));

  const openPrs = (reqs.data ?? []).filter((r) =>
    ["Submitted", "PartiallySourced", "Draft"].includes(r.headerStatus),
  ).length;
  const openRfqs = (rfqs.data ?? []).filter((r) =>
    ["Draft", "Open", "Closed", "Evaluation"].includes(r.status),
  ).length;
  const openPos = (pos.data ?? []).filter((p) =>
    ["Draft", "Issued", "Acknowledged", "PartiallyReceived"].includes(p.status),
  ).length;
  const inTransit = (asns.data ?? []).filter((a) => a.status === "InTransit").length;
  const pendingInv = (invoices.data ?? []).filter((i) =>
    ["Submitted", "Exception"].includes(i.status),
  ).length;

  const tiles: Tile[] = [
    {
      key: "prs",
      label: "Active requisitions",
      value: openPrs,
      hint: "Draft / submitted / partial",
      tone: "tone-teal",
      href: "/reqs",
      perm: FshPermissions.requisitions.view,
    },
    {
      key: "rfqs",
      label: "Open RFQs",
      value: openRfqs,
      hint: "In flight sourcing events",
      tone: "tone-amber",
      href: "/rfqs",
      perm: FshPermissions.rfqs.view,
    },
    {
      key: "pos",
      label: "Open POs",
      value: openPos,
      hint: "Awaiting receipt / match",
      tone: "tone-sage",
      href: "/pos",
      perm: FshPermissions.purchaseOrders.view,
    },
    {
      key: "asns",
      label: "In-transit ASNs",
      value: inTransit,
      hint: "Ready to receive",
      tone: "tone-clay",
      href: "/deliveries",
      perm: FshPermissions.deliveries.view,
    },
    {
      key: "inv",
      label: "Invoices to review",
      value: pendingInv,
      hint: "Submitted or exception",
      tone: "tone-velvet",
      href: "/invoices",
      perm: FshPermissions.invoices.view,
    },
    {
      key: "notif",
      label: "Unread notifications",
      value: unread.data ?? 0,
      hint: "Inbox",
      tone: "tone-teal",
      href: "/notifications",
      perm: FshPermissions.notifications.view,
    },
  ].filter((t) => can(t.perm));

  const shortcuts = [
    { label: "New requisition", href: "/reqs/new", icon: "plus" as const },
    { label: "New RFQ", href: "/rfqs/new", icon: "rfq" as const },
    { label: "Vendor Master", href: "/vendors", icon: "vendor" as const },
    { label: "Onboarding queue", href: "/onboarding", icon: "clip" as const },
    { label: "Bid openings", href: "/openings", icon: "lock" as const },
    { label: "Clarifications", href: "/chats", icon: "msg" as const },
  ];

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Dashboard</h1>
          <p>At-a-glance counts and shortcuts across sourcing and procure-to-pay.</p>
        </div>
      </div>

      {loading ? <Spinner label="Loading dashboard…" /> : null}

      {!loading && tiles.length > 0 ? (
        <div className="grid g3" style={{ marginBottom: 16 }}>
          {tiles.map((t) => (
            <button
              key={t.key}
              type="button"
              className={`card stat ${t.tone}`}
              style={{ textAlign: "left", cursor: "pointer", width: "100%", fontFamily: "inherit" }}
              onClick={() => void navigate(t.href)}
            >
              <div className="lbl">{t.label}</div>
              <div className="num">{t.value}</div>
              <div className="sub">{t.hint}</div>
            </button>
          ))}
        </div>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Shortcuts</h3>
        </div>
        <div className="cbody" style={{ display: "flex", flexWrap: "wrap", gap: 10 }}>
          {shortcuts.map((s) => (
            <button
              key={s.href}
              type="button"
              className="btn btn-out btn-sm"
              onClick={() => void navigate(s.href)}
            >
              <Icon name={s.icon} size={14} /> {s.label}
            </button>
          ))}
        </div>
      </div>
    </>
  );
}

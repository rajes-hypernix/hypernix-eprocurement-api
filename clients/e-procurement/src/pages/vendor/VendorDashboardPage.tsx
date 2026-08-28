import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listMyInvitations } from "@/api/sourcing";
import { listPurchaseOrders } from "@/api/procurement";
import { getUnreadCount } from "@/api/notifications";
import { Spinner } from "@/components/ui";
import { FshPermissions } from "@/lib/fsh-permissions";
import { useAuth } from "@/auth/use-auth";
import { InvitationsListPage } from "@/pages/vendor/InvitationsListPage";

/** Vendor home — work-queue KPIs (POC DASH-SYS-VENDOR) plus invitations. */
export function VendorDashboardPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const can = (p?: string) => !p || granted.includes(p);

  const invitations = useQuery({
    queryKey: ["my-invitations"],
    queryFn: listMyInvitations,
    enabled: can(FshPermissions.bids.viewMine),
  });
  const pos = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: listPurchaseOrders,
    enabled: can(FshPermissions.purchaseOrders.view),
  });
  const unread = useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: getUnreadCount,
    enabled: can(FshPermissions.notifications.view),
  });

  const loading =
    (invitations.isPending && can(FshPermissions.bids.viewMine)) ||
    (pos.isPending && can(FshPermissions.purchaseOrders.view));

  const rows = invitations.data ?? [];
  const toBid = rows.filter((r) => r.rfqStatus === "Open" && !r.hasSubmittedBid && r.invitationStatus !== "Declined" && r.invitationStatus !== "Rescinded").length;
  const submitted = rows.filter((r) => r.hasSubmittedBid).length;
  const toAck = (pos.data ?? []).filter((p) => p.status === "Issued").length;
  const openPos = (pos.data ?? []).filter((p) =>
    ["Issued", "Acknowledged", "PartiallyReceived"].includes(p.status),
  ).length;

  const tiles = [
    {
      key: "to-bid",
      label: "RFQs to bid",
      value: toBid,
      hint: "Open invitations still awaiting your bid",
      tone: "tone-amber",
      href: "/dashboard",
    },
    {
      key: "bids",
      label: "Bids submitted",
      value: submitted,
      hint: "Your drafts and submitted bids",
      tone: "tone-teal",
      href: "/bids",
    },
    {
      key: "ack",
      label: "POs to acknowledge",
      value: toAck,
      hint: "Issued orders waiting for you",
      tone: "tone-sage",
      href: "/pos",
    },
    {
      key: "open-pos",
      label: "Open POs",
      value: openPos,
      hint: "Issued through receipt",
      tone: "tone-clay",
      href: "/pos",
    },
    {
      key: "notif",
      label: "Unread notifications",
      value: unread.data ?? 0,
      hint: "Inbox",
      tone: "tone-velvet",
      href: "/notifications",
    },
  ];

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Dashboard</h1>
          <p>Your work queue — invitations, bids and purchase orders assigned to your company only.</p>
        </div>
      </div>

      {loading ? <Spinner label="Loading dashboard…" /> : null}

      {!loading ? (
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

      {can(FshPermissions.bids.viewMine) ? (
        <InvitationsListPage
          mode="invitations"
          embedded
          onOpen={(rfqId) => void navigate(`/dashboard/${rfqId}`)}
        />
      ) : null}
    </>
  );
}

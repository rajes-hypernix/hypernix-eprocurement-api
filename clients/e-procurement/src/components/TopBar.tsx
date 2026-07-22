import { useAuth } from "@/auth/use-auth";
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { TopBarNav } from "@/components/TopBarNav";
import { initials } from "@/lib/format";

export function TopBar() {
  const { user, isVendor, logout } = useAuth();
  const name = user?.name ?? user?.email ?? "User";
  const sub = isVendor ? "Vendor" : user?.tenant ? `Tenant · ${user.tenant}` : "Buyer";

  return (
    <div className="topbar">
      <div className="brand">
        <span className="logo">
          <svg
            width="17"
            height="17"
            viewBox="0 0 24 24"
            fill="none"
            stroke="#fff"
            strokeWidth={2.1}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M3 7l9-4 9 4-9 4-9-4z" />
            <path d="M3 7v10l9 4 9-4V7" />
            <path d="M12 11v10" />
          </svg>
        </span>
        <span>
          <span className="brand-title">Hypernix eProcure</span>
          <small>Sourcing &amp; Vendor Portal</small>
        </span>
      </div>
      <div className="tb-center">
        <TopBarNav />
      </div>
      <div className="spacer" />
      <NotificationBell />
      <div className="whoami">
        <div className="prof-chip" style={{ marginLeft: 4 }}>
          <span className="av">{initials(name)}</span>
          <div className="ptext">
            <div className="pname">{name}</div>
            <div className="prole">{sub}</div>
          </div>
        </div>
        <button
          type="button"
          className="btn ghost"
          style={{
            marginLeft: 10,
            color: "#fff",
            borderColor: "rgba(255,255,255,.35)",
            background: "rgba(255,255,255,.1)",
          }}
          onClick={() => logout()}
        >
          Sign out
        </button>
      </div>
    </div>
  );
}

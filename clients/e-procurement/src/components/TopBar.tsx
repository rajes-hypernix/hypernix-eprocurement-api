import { Link } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { RoleActAsSelect } from "@/components/RoleActAsSelect";
import { TopBarNav } from "@/components/TopBarNav";
import { initials, roleLabel } from "@/lib/format";
import { evaluatorKind, isEvaluatorRoleName } from "@/lib/workspace";

export function TopBar({ onOpenNav }: { onOpenNav?: () => void }) {
  const { user, isVendor, isEvaluatorWorkspace, logout, rolePreview } = useAuth();
  const name = user?.name ?? user?.email ?? "User";
  const kind = evaluatorKind({
    roles: user?.roles ?? [],
    permissions: user?.permissions,
    previewRoleName: rolePreview?.roleName,
  });
  const evaluatorCaption =
    kind.tech && kind.commercial
      ? "Evaluator"
      : kind.tech
        ? roleLabel("TechEvaluator")
        : kind.commercial
          ? roleLabel("CommEvaluator")
          : "Evaluator";
  const sub = rolePreview
    ? `Preview · ${isEvaluatorRoleName(rolePreview.roleName) ? roleLabel(rolePreview.roleName) : rolePreview.roleName}`
    : isVendor
      ? "Vendor"
      : isEvaluatorWorkspace
        ? evaluatorCaption
        : user?.tenant
          ? `Tenant · ${user.tenant}`
          : "Buyer";

  return (
    <div className="topbar">
      {onOpenNav ? (
        <button type="button" className="tb-menu-btn" aria-label="Open navigation" onClick={onOpenNav}>
          <Icon name="menu" size={18} />
        </button>
      ) : null}
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
          <small className="brand-sub">Sourcing &amp; Vendor Portal</small>
        </span>
      </div>
      <div className="tb-center">
        <TopBarNav />
      </div>
      <div className="spacer" />
      <NotificationBell />
      <div className="whoami">
        <RoleActAsSelect />
        <Link to="/profile" className="prof-chip prof-chip-link" title="Edit profile" style={{ marginLeft: 4 }}>
          <span className="av">{initials(name)}</span>
          <div className="ptext">
            <div className="pname">{name}</div>
            <div className="prole">{sub}</div>
          </div>
        </Link>
        <button
          type="button"
          className="btn ghost tb-signout"
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

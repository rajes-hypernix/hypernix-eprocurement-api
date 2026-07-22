import { useMemo } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { TopBar } from "@/components/TopBar";
import { Sidebar } from "@/components/Sidebar";
import { useAuth } from "@/auth/use-auth";
import { gateNav } from "@/lib/permissions-map";
import { BUYER_NAV, VENDOR_NAV } from "@/nav";

const EMPTY_PERMS: readonly string[] = [];

/** Layout mirrors original eprocure/web App.tsx shell (TopBar + .shell + Sidebar + .main). */
export function AppShell() {
  const { isVendor, user, permissionsHydrated } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const pageKey = pathname.split("/").filter(Boolean)[0] ?? "dashboard";
  const baseNav = isVendor ? VENDOR_NAV : BUYER_NAV;
  const granted = user?.permissions ?? EMPTY_PERMS;
  const permKey = granted.join("\0");
  const nav = useMemo(() => {
    if (!permissionsHydrated) return [];
    return gateNav(baseNav, granted);
  }, [baseNav, granted, permKey, permissionsHydrated]);

  return (
    <>
      <TopBar />
      <div className="shell">
        <Sidebar nav={nav} active={pageKey} onSelect={(key) => void navigate(`/${key}`)} />
        <main className="main">
          <Outlet />
        </main>
      </div>
    </>
  );
}

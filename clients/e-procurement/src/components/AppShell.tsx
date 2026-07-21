import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { TopBar } from "@/components/TopBar";
import { Sidebar } from "@/components/Sidebar";
import { useAuth } from "@/auth/use-auth";
import { BUYER_NAV, VENDOR_NAV } from "@/nav";

/** Layout mirrors original eprocure/web App.tsx shell (TopBar + .shell + Sidebar + .main). */
export function AppShell() {
  const { isVendor } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const pageKey = pathname.split("/").filter(Boolean)[0] ?? "dashboard";
  const nav = isVendor ? VENDOR_NAV : BUYER_NAV;

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

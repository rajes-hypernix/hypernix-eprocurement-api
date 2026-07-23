import { useEffect, useMemo, useState } from "react";
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
  const { pathname, search } = useLocation();
  const [navOpen, setNavOpen] = useState(false);
  const activeHref = `${pathname}${search}`;
  const baseNav = isVendor ? VENDOR_NAV : BUYER_NAV;
  const granted = user?.permissions ?? EMPTY_PERMS;
  const permKey = granted.join("\0");
  const nav = useMemo(() => {
    if (!permissionsHydrated) return [];
    return gateNav(baseNav, granted);
  }, [baseNav, granted, permKey, permissionsHydrated]);

  useEffect(() => {
    setNavOpen(false);
  }, [pathname, search]);

  useEffect(() => {
    if (!navOpen) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setNavOpen(false);
    };
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = prev;
      window.removeEventListener("keydown", onKey);
    };
  }, [navOpen]);

  const go = (href: string) => {
    setNavOpen(false);
    void navigate(href.startsWith("/") ? href : `/${href}`);
  };

  return (
    <>
      <TopBar onOpenNav={() => setNavOpen(true)} />
      <div className="shell">
        <Sidebar nav={nav} activeHref={activeHref} onSelect={go} />
        <div className={`side-drawer-layer${navOpen ? " open" : ""}`} aria-hidden={!navOpen}>
          <button
            type="button"
            className="side-backdrop"
            aria-label="Close navigation"
            tabIndex={navOpen ? 0 : -1}
            onClick={() => setNavOpen(false)}
          />
          <Sidebar
            variant="drawer"
            nav={nav}
            activeHref={activeHref}
            onSelect={go}
            onClose={() => setNavOpen(false)}
          />
        </div>
        <main className="main">
          <Outlet />
        </main>
      </div>
    </>
  );
}

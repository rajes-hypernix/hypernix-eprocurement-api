import { useState } from "react";
import { BUYER_NAV, type NavGroup } from "@/nav";
import { Icon } from "@/components/Icon";

const LS_KEY = "sidebarCollapsed";
const initialCollapsed = () => {
  try {
    return localStorage.getItem(LS_KEY) === "1";
  } catch {
    return false;
  }
};

export function Sidebar({
  nav = BUYER_NAV,
  active,
  onSelect,
  variant = "rail",
  onClose,
}: {
  nav?: NavGroup[];
  active: string;
  onSelect: (key: string) => void;
  /** Desktop sticky rail (default) or mobile/tablet overlay drawer. */
  variant?: "rail" | "drawer";
  onClose?: () => void;
}) {
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const isDrawer = variant === "drawer";
  const showLabels = isDrawer || !collapsed;

  const toggle = () => {
    setCollapsed((c) => {
      try {
        localStorage.setItem(LS_KEY, c ? "0" : "1");
      } catch {
        /* non-browser */
      }
      return !c;
    });
  };

  return (
    <aside
      className={`side${collapsed && !isDrawer ? " collapsed" : ""}${isDrawer ? " side-drawer" : " side-rail"}`}
      aria-label={isDrawer ? "Mobile navigation" : "Main navigation"}
    >
      {isDrawer ? (
        <div className="side-drawer-head">
          <span className="side-drawer-title">Menu</span>
          <button type="button" className="side-drawer-close" aria-label="Close menu" onClick={onClose}>
            <Icon name="x" size={16} />
          </button>
        </div>
      ) : (
        <button
          type="button"
          className="side-toggle"
          aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          aria-expanded={!collapsed}
          title={collapsed ? "Expand" : "Collapse"}
          onClick={toggle}
        >
          <Icon name={collapsed ? "chev" : "back"} size={15} />
        </button>
      )}
      {nav.map((group) => (
        <div key={group.title}>
          <div className="grp">{showLabels ? group.title : "\u00a0"}</div>
          {group.items.map((item) => (
            <div
              key={item.key}
              className={`nav${active === item.key ? " on" : ""}`}
              role="button"
              tabIndex={0}
              aria-current={active === item.key ? "page" : undefined}
              aria-label={!showLabels ? item.label : undefined}
              title={!showLabels ? item.label : undefined}
              onClick={() => onSelect(item.key)}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") onSelect(item.key);
              }}
            >
              <span className="ic">
                <Icon name={item.icon} />
              </span>
              {showLabels ? item.label : null}
            </div>
          ))}
        </div>
      ))}
    </aside>
  );
}

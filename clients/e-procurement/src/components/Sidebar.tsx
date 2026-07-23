import { useEffect, useState } from "react";
import { BUYER_NAV, type NavGroup, type NavItem } from "@/nav";
import { Icon } from "@/components/Icon";

const LS_KEY = "sidebarCollapsed";
const initialCollapsed = () => {
  try {
    return localStorage.getItem(LS_KEY) === "1";
  } catch {
    return false;
  }
};

function itemHref(item: NavItem): string {
  return item.href ?? `/${item.key}`;
}

function isItemActive(item: NavItem, activeHref: string, forParent = false): boolean {
  const href = itemHref(item);
  if (activeHref === href) return true;
  if (item.children?.some((c) => isItemActive(c, activeHref))) return true;
  // Parent /masters matches any /masters?tab=* (not used for leaf highlight).
  if (forParent && href === "/masters" && activeHref.startsWith("/masters")) return true;
  return false;
}

export function Sidebar({
  nav = BUYER_NAV,
  activeHref,
  onSelect,
  variant = "rail",
  onClose,
}: {
  nav?: NavGroup[];
  /** Full path+search used for highlighting, e.g. `/masters?tab=banks`. */
  activeHref: string;
  onSelect: (href: string) => void;
  variant?: "rail" | "drawer";
  onClose?: () => void;
}) {
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const [openKeys, setOpenKeys] = useState<Record<string, boolean>>({});
  const isDrawer = variant === "drawer";
  const showLabels = isDrawer || !collapsed;

  useEffect(() => {
    const next: Record<string, boolean> = {};
    for (const g of nav) {
      for (const item of g.items) {
        if (item.children?.length && isItemActive(item, activeHref, true)) {
          next[item.key] = true;
        }
      }
    }
    if (Object.keys(next).length) {
      setOpenKeys((prev) => ({ ...prev, ...next }));
    }
  }, [activeHref, nav]);

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

  const toggleOpen = (key: string) => {
    setOpenKeys((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const renderItem = (item: NavItem, depth = 0) => {
    const hasChildren = !!item.children?.length;
    const open = !!openKeys[item.key];
    const active = isItemActive(item, activeHref, hasChildren);
    const childActive = item.children?.some((c) => isItemActive(c, activeHref));

    return (
      <div key={item.key} className={depth > 0 ? "nav-branch" : undefined}>
        <div
          className={`nav${active && !hasChildren ? " on" : ""}${hasChildren && (active || childActive) ? " nav-parent-on" : ""}${depth > 0 ? " nav-child" : ""}`}
          role="button"
          tabIndex={0}
          aria-current={active && !hasChildren ? "page" : undefined}
          aria-expanded={hasChildren ? open : undefined}
          aria-label={!showLabels ? item.label : undefined}
          title={!showLabels ? item.label : undefined}
          onClick={() => {
            if (hasChildren && showLabels) {
              setOpenKeys((prev) => ({ ...prev, [item.key]: true }));
              onSelect(itemHref(item));
              return;
            }
            onSelect(itemHref(item));
          }}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              if (hasChildren && showLabels) {
                setOpenKeys((prev) => ({ ...prev, [item.key]: true }));
                onSelect(itemHref(item));
              } else {
                onSelect(itemHref(item));
              }
            }
          }}
        >
          <span className="ic">
            <Icon name={item.icon} />
          </span>
          {showLabels ? <span className="nav-label">{item.label}</span> : null}
          {showLabels && hasChildren ? (
            <button
              type="button"
              className={`nav-caret${open ? " open" : ""}`}
              aria-label={open ? `Collapse ${item.label}` : `Expand ${item.label}`}
              onClick={(e) => {
                e.stopPropagation();
                toggleOpen(item.key);
              }}
            >
              <Icon name="chev" size={12} />
            </button>
          ) : null}
        </div>
        {showLabels && hasChildren && open
          ? item.children!.map((child) => renderItem(child, depth + 1))
          : null}
      </div>
    );
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
          {group.items.map((item) => renderItem(item))}
        </div>
      ))}
    </aside>
  );
}

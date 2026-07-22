import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { GlobalSearch } from "@/components/search/GlobalSearch";
import { Icon } from "@/components/Icon";
import { FshPermissions } from "@/lib/fsh-permissions";
import { clearRecents, loadRecents, type RecentEntry } from "@/lib/recents";

type NewItem = {
  label: string;
  path: string;
  permission?: string;
};

const NEW_ITEMS: NewItem[] = [
  { label: "Requisition", path: "/reqs/new", permission: FshPermissions.requisitions.manage },
  { label: "RFQ", path: "/rfqs/new", permission: FshPermissions.rfqs.manageDraft },
  { label: "Vendor", path: "/vendors/new", permission: FshPermissions.vendors.create },
  { label: "Onboarding invite", path: "/onboarding", permission: FshPermissions.onboarding.invite },
];

function NewMenu() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const wrapRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const granted = user?.permissions ?? [];
  const items = NEW_ITEMS.filter((item) => !item.permission || granted.includes(item.permission));

  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, []);

  if (items.length === 0) return null;

  return (
    <div className="tb-menuwrap" ref={wrapRef}>
      <button type="button" className="tb-btn" aria-expanded={open} onClick={() => setOpen((v) => !v)}>
        <Icon name="plus" size={14} />
        New
      </button>
      {open ? (
        <div className="tb-menu" role="menu">
          {items.map((item) => (
            <button
              key={item.path}
              type="button"
              role="menuitem"
              className="tb-item"
              onClick={() => {
                setOpen(false);
                void navigate(item.path);
              }}
            >
              {item.label}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function RecentsMenu() {
  const navigate = useNavigate();
  const wrapRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [recents, setRecents] = useState<RecentEntry[]>(() => loadRecents());

  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, []);

  const refresh = () => setRecents(loadRecents());

  return (
    <div className="tb-menuwrap" ref={wrapRef}>
      <button
        type="button"
        className="tb-btn"
        aria-expanded={open}
        onClick={() => {
          refresh();
          setOpen((v) => !v);
        }}
      >
        <Icon name="clock" size={14} />
        Recents
      </button>
      {open ? (
        <div className="tb-menu" role="menu">
          {recents.length === 0 ? (
            <div className="tb-item muted" style={{ cursor: "default" }}>
              No recent pages
            </div>
          ) : (
            recents.map((entry) => (
              <button
                key={entry.path}
                type="button"
                role="menuitem"
                className="tb-item"
                onClick={() => {
                  setOpen(false);
                  void navigate(entry.path);
                }}
              >
                <span>{entry.label}</span>
                {entry.code ? <span className="muted"> · {entry.code}</span> : null}
              </button>
            ))
          )}
          {recents.length > 0 ? (
            <button
              type="button"
              className="tb-item muted"
              onClick={() => {
                clearRecents();
                refresh();
              }}
            >
              Clear recents
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

export function TopBarNav() {
  return (
    <>
      <GlobalSearch />
      <NewMenu />
      <RecentsMenu />
    </>
  );
}

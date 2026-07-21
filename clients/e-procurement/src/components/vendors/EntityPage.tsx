import { useState, type ReactNode } from "react";
import { Icon } from "@/components/Icon";

export interface EntityTab {
  key: string;
  label: string;
  content: ReactNode;
}

export function EntityPage({
  crumbParent,
  onCrumbParent,
  crumbCurrent,
  title,
  subtitle,
  actions,
  tabs,
  tab,
  onTabChange,
  children,
}: {
  crumbParent: string;
  onCrumbParent: () => void;
  crumbCurrent: string;
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  tabs: EntityTab[];
  tab?: string;
  onTabChange?: (key: string) => void;
  children?: ReactNode;
}) {
  const [internal, setInternal] = useState(tabs[0]?.key);
  const active = tab ?? internal;
  const select = (k: string) => {
    onTabChange?.(k);
    if (tab === undefined) setInternal(k);
  };

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onCrumbParent}>
          {crumbParent}
        </button>{" "}
        <Icon name="chev" size={13} /> <span>{crumbCurrent}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>{title}</h1>
          {subtitle ? <p style={{ display: "flex", alignItems: "center", gap: 8 }}>{subtitle}</p> : null}
        </div>
        <div className="spacer" />
        {actions}
      </div>

      <div className="vtabs">
        {tabs.map((t) => (
          <button
            key={t.key}
            type="button"
            className={`vtab${active === t.key ? " on" : ""}`}
            onClick={() => select(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tabs.find((t) => t.key === active)?.content}

      {children}
    </>
  );
}

export function Kv({ k, v }: { k: string; v: ReactNode }) {
  return (
    <div className="kv">
      <span className="k">{k}</span>
      <span className="v">{v}</span>
    </div>
  );
}

export function Stat({ label, value, sub }: { label: string; value: ReactNode; sub?: string }) {
  return (
    <div className="card stat">
      <div className="lbl">{label}</div>
      <div className="num">{value}</div>
      {sub ? <div className="sub">{sub}</div> : null}
    </div>
  );
}

import type { CSSProperties, ReactNode } from "react";
import { Icon } from "@/components/Icon";

export function Spinner({ label = "Loading…" }: { label?: string }) {
  return <div className="spinner">{label}</div>;
}

export function EmptyState({ children, icon }: { children: ReactNode; icon?: string }) {
  return (
    <div className="empty">
      {icon ? (
        <div className="ic">
          <Icon name={icon} size={30} />
        </div>
      ) : null}
      {children}
    </div>
  );
}

type Tone = "info" | "error" | "warn" | "success";

export function Notice({
  tone = "info",
  icon,
  children,
  style,
}: {
  tone?: Tone;
  icon?: string;
  children: ReactNode;
  style?: CSSProperties;
}) {
  return (
    <div className={`ribbon ribbon-${tone}`} style={{ marginBottom: 14, ...style }}>
      {icon ? <Icon name={icon} size={14} /> : null} {children}
    </div>
  );
}

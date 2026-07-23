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

export function Modal({
  title,
  icon = "check",
  children,
  footer,
}: {
  title: ReactNode;
  icon?: string;
  children: ReactNode;
  footer: ReactNode;
}) {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal">
        <div className="mhead">
          <Icon name={icon} size={20} />
          <h3>{title}</h3>
        </div>
        <div className="mbody">{children}</div>
        <div className="mfoot">{footer}</div>
      </div>
    </div>
  );
}

export function AlertModal({
  title = "Something went wrong",
  icon = "x",
  body,
  okLabel = "OK",
  onClose,
}: {
  title?: ReactNode;
  icon?: string;
  body: ReactNode;
  okLabel?: string;
  onClose: () => void;
}) {
  return (
    <Modal
      title={title}
      icon={icon}
      footer={
        <button type="button" className="btn btn-pri" onClick={onClose} autoFocus>
          {okLabel}
        </button>
      }
    >
      {typeof body === "string" ? <p className="hint" style={{ marginTop: 0, whiteSpace: "pre-wrap" }}>{body}</p> : body}
    </Modal>
  );
}

export function ConfirmModal({
  title,
  icon = "check",
  body,
  confirmLabel,
  cancelLabel = "Cancel",
  danger = false,
  busy = false,
  onCancel,
  onConfirm,
}: {
  title: ReactNode;
  icon?: string;
  body: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  danger?: boolean;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <Modal
      title={title}
      icon={icon}
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button
            type="button"
            className={`btn btn-pri${danger ? " btn-danger" : ""}`}
            disabled={busy}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </>
      }
    >
      {typeof body === "string" ? <p className="hint" style={{ marginTop: 0 }}>{body}</p> : body}
    </Modal>
  );
}

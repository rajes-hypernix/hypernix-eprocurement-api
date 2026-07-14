import type { ReactNode, CSSProperties } from 'react'
import { Icon } from './Icon'

/** Loading placeholder. */
export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return <div className="spinner">{label}</div>
}

/** Empty-table / empty-list state. */
export function EmptyState({ children, icon }: { children: ReactNode; icon?: string }) {
  return (
    <div className="empty">
      {icon && <div className="ic"><Icon name={icon} size={30} /></div>}
      {children}
    </div>
  )
}

type Tone = 'info' | 'error' | 'warn' | 'success'

/** Inline status banner. Replaces the per-screen colour-styled ribbons. */
export function Notice({ tone = 'info', icon, children, style }: { tone?: Tone; icon?: string; children: ReactNode; style?: CSSProperties }) {
  return (
    <div className={`ribbon ribbon-${tone}`} style={{ marginBottom: 14, ...style }}>
      {icon && <Icon name={icon} size={14} />} {children}
    </div>
  )
}

/** Generic centred modal shell (header + body + footer). CFF-T5: an optional `headerAction`
 * renders the primary Save/Submit TOP-RIGHT of the modal header (consistent with the top-right
 * placement on full-page create/edit panels). */
export function Modal({ title, icon = 'check', children, footer, headerAction }: {
  title: ReactNode; icon?: string; children: ReactNode; footer: ReactNode; headerAction?: ReactNode
}) {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal">
        <div className="mhead"><Icon name={icon} size={20} /><h3>{title}</h3>{headerAction && <span className="mhead-action">{headerAction}</span>}</div>
        <div className="mbody">{children}</div>
        <div className="mfoot">{footer}</div>
      </div>
    </div>
  )
}

/** Confirm dialog with a cancel + a primary (optionally danger) action. */
export function ConfirmModal({
  title, icon = 'check', body, confirmLabel, confirmIcon, cancelLabel = 'Cancel',
  danger = false, busy = false, onCancel, onConfirm,
}: {
  title: ReactNode
  icon?: string
  body: ReactNode
  confirmLabel: string
  confirmIcon?: string
  cancelLabel?: string
  danger?: boolean
  busy?: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  return (
    <Modal
      title={title}
      icon={icon}
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onCancel}>{cancelLabel}</button>
          <button type="button" className={`btn btn-pri${danger ? ' btn-danger' : ''}`} disabled={busy} onClick={onConfirm}>
            {confirmIcon && <Icon name={confirmIcon} size={15} />} {confirmLabel}
          </button>
        </>
      }
    >
      {typeof body === 'string' ? <p className="hint" style={{ marginTop: 0 }}>{body}</p> : body}
    </Modal>
  )
}

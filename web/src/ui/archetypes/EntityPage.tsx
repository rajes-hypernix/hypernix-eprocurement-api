import { useState, type ReactNode } from 'react'
import { Icon } from '../../components/Icon'

/**
 * EntityPage — the Entity archetype (D2 Phase 1), derived from VendorDetail
 * (retrofit target) + RfqDetailHub and the PO detail (Step 0c relatives).
 * Absorbs (Step 0d): crumb, master pagehead with inline badges + header
 * actions, .vtab subtabs, per-tab body sections/sublists. Tab state may be
 * controlled (VendorDetail gates its audit query on the active tab) or left
 * internal.
 */

export interface EntityTab { key: string; label: string; content: ReactNode }

export function EntityPage({
  crumbParent, onCrumbParent, crumbCurrent,
  title, subtitle, actions, tabs, tab, onTabChange, children,
}: {
  crumbParent: string
  onCrumbParent: () => void
  crumbCurrent: string
  /** master header line — ReactNode so type badges render inline */
  title: ReactNode
  /** second header line — code · region · status badge */
  subtitle?: ReactNode
  /** header action buttons (right-aligned) */
  actions?: ReactNode
  tabs: EntityTab[]
  /** controlled active tab (optional; internal state otherwise) */
  tab?: string
  onTabChange?: (key: string) => void
  /** modals / pickers */
  children?: ReactNode
}) {
  const [internal, setInternal] = useState(tabs[0]?.key)
  const active = tab ?? internal
  const select = (k: string) => { onTabChange?.(k); if (tab === undefined) setInternal(k) }

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onCrumbParent}>{crumbParent}</button>{' '}
        <Icon name="chev" size={13} /> <span>{crumbCurrent}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: 'flex', alignItems: 'center', gap: 10 }}>{title}</h1>
          {subtitle && <p style={{ display: 'flex', alignItems: 'center', gap: 8 }}>{subtitle}</p>}
        </div>
        <div className="spacer" />
        {actions}
      </div>

      <div className="vtabs">
        {tabs.map((t) => (
          <button key={t.key} type="button" className={`vtab${active === t.key ? ' on' : ''}`} onClick={() => select(t.key)}>
            {t.label}
          </button>
        ))}
      </div>

      {tabs.find((t) => t.key === active)?.content}

      {children}
    </>
  )
}

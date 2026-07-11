import type { ReactNode } from 'react'
import { Notice } from '../../components/ui'

/**
 * SetupPage — the Setup archetype (D2 Phase 1), the rail + detail layout
 * generalised from AdminCustomLists (retrofit target; AdminUsers and Forms
 * are the Step 0c relatives). Absorbs (Step 0d): pagehead with primary
 * action, error Notice, the obwrap/obside/obstep rail (item + hint sub-line)
 * with the detail pane alongside; modal editors ride as children.
 */

export interface SetupRailItem {
  key: string
  label: string
  /** sub-line under the label (codes, dependency notes) */
  hint?: string
}

export function SetupPage({
  title, subtitle, primaryAction, error,
  railItems, selectedKey, onSelect, railEmpty = 'Nothing here yet.',
  detail, children,
}: {
  title: string
  subtitle?: string
  primaryAction?: ReactNode
  error?: string | null
  railItems: SetupRailItem[]
  selectedKey: string | null
  onSelect: (key: string) => void
  railEmpty?: ReactNode
  /** the detail pane for the selected rail item */
  detail: ReactNode
  /** modals */
  children?: ReactNode
}) {
  return (
    <>
      <div className="pagehead">
        <div><h1>{title}</h1>{subtitle && <p>{subtitle}</p>}</div>
        <div className="spacer" />
        {primaryAction}
      </div>

      {error && <Notice tone="error" icon="x">{error}</Notice>}

      <div className="obwrap">
        <div className="obside" style={{ position: 'static' }}>
          {railItems.map((it) => (
            <button type="button" key={it.key} className={`obstep ${selectedKey === it.key ? 'on' : ''}`} onClick={() => onSelect(it.key)}>
              <span style={{ display: 'flex', flexDirection: 'column', gap: 2, textAlign: 'left' }}>
                <span>{it.label}</span>
                {it.hint && <span className="hint" style={{ fontWeight: 400 }}>{it.hint}</span>}
              </span>
            </button>
          ))}
          {railItems.length === 0 && <p className="hint">{railEmpty}</p>}
        </div>
        <div className="obmain">{detail}</div>
      </div>

      {children}
    </>
  )
}

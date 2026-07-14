import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Icon } from '../../components/Icon'
import { Notice } from '../../components/ui'
import type { FieldSpec } from '../fieldSpec'
import { renderField } from '../renderField'

/**
 * TransactionPage — the Transaction archetype (D2 Phase 1), derived from
 * PrForm (retrofit target) + DeliveryScreens ASN-create + InvoiceScreens
 * entry (D0 census: pagehead, crumb, card sections, frow field rows, the
 * duplicated top+bottom action row, status pill, dirty guard).
 *
 * Body-field sections render from FieldSpec ARRAYS through renderField —
 * charter rule 3. Sublists (line grids) and modals are children/slots.
 */

export interface TransactionSection {
  title: string
  /** rows of specs — each row renders as one .frow (the 3-across layout) */
  rows: FieldSpec[][]
  /** full-width specs after the rows (memo-style) */
  fullWidth?: FieldSpec[]
  /** CF5-T3: this section starts the SECOND column (two-column canvas, ruled D3) */
  columnBreak?: boolean
}

/**
 * Guard hash navigation while a form is dirty: any hash change (sidebar,
 * crumb, back button) asks before leaving; declining restores the hash.
 * beforeunload covers tab close/refresh. Dirtiness lives in a ref checked at
 * event time, and `markClean()` clears it SYNCHRONOUSLY — screens call it
 * right before intentional navigation (save → back), so the guard only ever
 * challenges ABANDONING edits, never completing them.
 */
export function useDirtyNavigationGuard(dirty: boolean, message = 'Discard unsaved changes?') {
  const dirtyRef = useRef(dirty)
  dirtyRef.current = dirty
  const current = useRef(window.location.hash)
  const markClean = useRef(() => { dirtyRef.current = false }).current
  useEffect(() => {
    current.current = window.location.hash
    const onHash = () => {
      if (!dirtyRef.current || window.location.hash === current.current) return
      if (window.confirm(message)) { current.current = window.location.hash; return }
      window.location.hash = current.current
    }
    const onUnload = (e: BeforeUnloadEvent) => { if (dirtyRef.current) e.preventDefault() }
    window.addEventListener('hashchange', onHash)
    window.addEventListener('beforeunload', onUnload)
    return () => { window.removeEventListener('hashchange', onHash); window.removeEventListener('beforeunload', onUnload) }
  }, [message])
  return { markClean }
}

export interface TransactionTab {
  key: string
  label: string
  content?: ReactNode
  /** D7: definition-driven subtab bodies — FieldSpec sections rendered through the SAME
   * pipeline as the main body (fields only; sublists keep their built-in homes). */
  sections?: TransactionSection[]
}

export function TransactionPage({
  crumbParent, onCrumbParent, crumbCurrent,
  title, subtitle, statusBadge,
  error, actions, sections, values, onFieldChange, fieldErrors,
  tabs, dirty = false, onGuardReady, children, topSlot,
}: {
  crumbParent: string
  onCrumbParent: () => void
  crumbCurrent: string
  title: string
  subtitle?: string
  /** status pill slot (PrHeaderBadge and friends) */
  statusBadge?: ReactNode
  /** error banner text — rendered as the standard error Notice */
  error?: string | null
  /** status-adaptive action bar; rendered at the top AND bottom (they can't drift) */
  actions: ReactNode
  sections: TransactionSection[]
  values: Record<string, string>
  onFieldChange: (key: string, v: string) => void
  /** per-field runtime errors, keyed by spec key (chrome-level state) */
  fieldErrors?: Record<string, string>
  /** optional subtabs below the sections (vtab idiom) */
  tabs?: TransactionTab[]
  /** unsaved edits → hash-navigation + unload guard */
  dirty?: boolean
  /** receives markClean — call it right before intentional navigation (save → back) */
  onGuardReady?: (markClean: () => void) => void
  /** sublist slots: line grids, notes, modals */
  children?: ReactNode
  /** CF-FIX5-T2: a slot ABOVE the sections (the form picker — chosen first, it drives the layout) */
  topSlot?: ReactNode
}) {
  const { markClean } = useDirtyNavigationGuard(dirty)
  onGuardReady?.(markClean)
  const [tab, setTab] = useState(tabs?.[0]?.key)
  const actionBar = <div className="pr-actions">{actions}</div>
  const renderSection = (s: TransactionSection) => (
    <div className="card" style={{ padding: 18, marginBottom: 16 }} key={s.title}>
      <h3 style={{ marginTop: 0 }}>{s.title}</h3>
      {s.rows.map((row, ri) => (
        <div className="frow" key={ri}>
          {row.map((spec) => renderField(spec, values[spec.key] ?? '', (v) => onFieldChange(spec.key, v), { error: fieldErrors?.[spec.key] }))}
        </div>
      ))}
      {s.fullWidth?.map((spec) => renderField(spec, values[spec.key] ?? '', (v) => onFieldChange(spec.key, v), { error: fieldErrors?.[spec.key] }))}
    </div>
  )

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onCrumbParent}>{crumbParent}</button>{' '}
        <Icon name="chev" size={13} /> <span>{crumbCurrent}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>{title}</h1>
          {subtitle && <p>{subtitle}</p>}
        </div>
        <div className="spacer" />
        {statusBadge}
      </div>

      {error && <Notice tone="error" icon="x" style={{ marginBottom: 14 }}>{error}</Notice>}

      {actionBar}

      {topSlot}

      {(() => {
        // CF5-T3 column break: sections after the FIRST break render in a second column
        // (two-column canvas only — ruled D3; extra breaks fold into the right column).
        const br = sections.findIndex((s) => s.columnBreak)
        if (br <= 0) return sections.map((s) => renderSection(s))
        return (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, alignItems: 'start' }} data-cols="2">
            <div>{sections.slice(0, br).map((s) => renderSection(s))}</div>
            <div>{sections.slice(br).map((s) => renderSection(s))}</div>
          </div>
        )
      })()}

      {tabs && tabs.length > 0 && (
        <>
          <div className="vtabs">
            {tabs.map((t) => (
              <button type="button" key={t.key} className={`vtab ${tab === t.key ? 'on' : ''}`} onClick={() => setTab(t.key)}>{t.label}</button>
            ))}
          </div>
          {tabs.filter((t) => t.key === tab).map((t) => (
            <div key={t.key}>
              {t.sections?.map((s) => renderSection(s))}
              {t.content}
            </div>
          ))}
        </>
      )}

      {children}

      {actionBar}
    </>
  )
}

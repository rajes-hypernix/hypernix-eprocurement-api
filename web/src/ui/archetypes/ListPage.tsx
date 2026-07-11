import { useMemo, useState, type ReactNode } from 'react'
import { Icon } from '../../components/Icon'
import { EmptyState } from '../../components/ui'

/**
 * ListPage — the List archetype (D2 Phase 1), derived from RfqList (retrofit
 * target) + Requisitions and VendorMaster (Step 0c relatives). Absorbs
 * (Step 0d): pagehead with primary action + view-toggle slot, the filterbar2
 * faceted multi-select pattern (msel/mbtn/mpop/mscrim, facets derived from
 * data, freset), search box, row-actions column, EmptyState (none vs
 * filtered), and census misfit F31 — bulk row-select with indeterminate
 * select-all — which lands HERE rather than a third hand-rolled copy.
 *
 * Sorting is OPT-IN per column and default OFF (operator ruling: the prompt
 * mandates the capability; the census shows zero sortable headers, so nothing
 * sorts unless a column declares sortValue).
 */

export interface ListColumn<T> {
  key: string
  header: ReactNode
  /** right-aligned (.amt) */
  amt?: boolean
  render: (row: T) => ReactNode
  /** OPT-IN client-side sorting; omit = column not sortable (census default) */
  sortValue?: (row: T) => string | number
}

export interface ListFacet<T> {
  key: string
  label: string
  value: (row: T) => string
}

export interface BulkSelect<T> {
  selected: Set<string>
  onChange: (next: Set<string>) => void
  /** rows that may be selected (disabled checkbox otherwise) */
  selectable?: (row: T) => boolean
  ariaLabel?: (row: T) => string
}

export function ListPage<T>({
  title, subtitle, primaryAction, viewToggle,
  searchLabel, searchPlaceholder, searchAriaLabel, searchMatch,
  facets = [], rows, rowKey, columns, rowActions, bulkSelect,
  emptyNone, emptyFiltered, alternateBody, children,
}: {
  title: string
  subtitle?: string
  /** right side of the pagehead (New … button) */
  primaryAction?: ReactNode
  /** view-toggle slot (table/board buttons) — rendered left of the primary action */
  viewToggle?: ReactNode
  /** search box (first filterbar cell); omit for no search */
  searchLabel?: string
  searchPlaceholder?: string
  searchAriaLabel?: string
  searchMatch?: (row: T, q: string) => boolean
  /** faceted multi-select filters; values derived from the data */
  facets?: ListFacet<T>[]
  rows: T[]
  rowKey: (row: T) => string
  columns: ListColumn<T>[]
  /** last-cell action buttons */
  rowActions?: (row: T) => ReactNode
  /** census F31 — bulk row-select with indeterminate select-all */
  bulkSelect?: BulkSelect<T>
  emptyNone: ReactNode
  emptyFiltered: ReactNode
  /** when provided, replaces the table (board view); receives the FILTERED rows */
  alternateBody?: (filtered: T[]) => ReactNode
  /** modals etc. */
  children?: ReactNode
}) {
  const [search, setSearch] = useState('')
  const [sel, setSel] = useState<Record<string, string[]>>({})
  const [openFacet, setOpenFacet] = useState<string | null>(null)
  const [sort, setSort] = useState<{ key: string; dir: 1 | -1 } | null>(null)

  const uniq = (f: ListFacet<T>) => [...new Set(rows.map(f.value).filter(Boolean))]
  const q = search.trim().toLowerCase()
  const filtered = useMemo(() => {
    let out = rows.filter((r) =>
      facets.every((f) => !(sel[f.key]?.length) || sel[f.key].includes(f.value(r))) &&
      (!q || !searchMatch || searchMatch(r, q)))
    if (sort) {
      const col = columns.find((c) => c.key === sort.key)
      if (col?.sortValue) {
        const sv = col.sortValue
        out = [...out].sort((a, b) => (sv(a) < sv(b) ? -sort.dir : sv(a) > sv(b) ? sort.dir : 0))
      }
    }
    return out
  }, [rows, facets, sel, q, searchMatch, sort, columns])

  const reset = () => { setSel({}); setSearch('') }
  const toggleVal = (key: string, v: string, on: boolean) =>
    setSel((x) => ({ ...x, [key]: on ? [...(x[key] ?? []), v] : (x[key] ?? []).filter((y) => y !== v) }))

  const hasFilterbar = !!searchMatch || facets.length > 0
  const selectableRows = bulkSelect ? filtered.filter((r) => bulkSelect.selectable?.(r) ?? true) : []
  const allSelected = !!bulkSelect && selectableRows.length > 0 && selectableRows.every((r) => bulkSelect.selected.has(rowKey(r)))
  const someSelected = !!bulkSelect && selectableRows.some((r) => bulkSelect.selected.has(rowKey(r)))

  const headerCell = (c: ListColumn<T>) => {
    if (!c.sortValue) return <th key={c.key} className={c.amt ? 'amt' : undefined}>{c.header}</th>
    const active = sort?.key === c.key
    return (
      <th key={c.key} className={c.amt ? 'amt' : undefined} aria-sort={active ? (sort!.dir === 1 ? 'ascending' : 'descending') : undefined}>
        <button
          type="button" className="lnk"
          onClick={() => setSort(active && sort!.dir === -1 ? null : { key: c.key, dir: active ? -1 : 1 })}
        >
          {c.header}{active ? (sort!.dir === 1 ? ' ↑' : ' ↓') : ''}
        </button>
      </th>
    )
  }

  const totalCols = columns.length + (bulkSelect ? 1 : 0) + (rowActions ? 1 : 0)

  return (
    <>
      <div className="pagehead">
        <div><h1>{title}</h1>{subtitle && <p>{subtitle}</p>}</div>
        <div className="spacer" />
        {viewToggle}
        {primaryAction}
      </div>

      {openFacet && <div className="mscrim" onClick={() => setOpenFacet(null)} />}
      {hasFilterbar && (
        <div className="filterbar2">
          {searchMatch && (
            <div className="msel" style={{ flex: 1, minWidth: 200 }}>
              <label>{searchLabel}</label>
              <input type="text" value={search} placeholder={searchPlaceholder} aria-label={searchAriaLabel ?? searchLabel} onChange={(e) => setSearch(e.target.value)} />
            </div>
          )}
          {facets.map((f) => {
            const s = sel[f.key] ?? []
            const summ = s.length === 0 ? 'All' : s.length === 1 ? s[0] : `${s.length} selected`
            const open = openFacet === f.key
            return (
              <div className="msel" key={f.key}>
                <label>{f.label}</label>
                <button type="button" className={`mbtn ${s.length ? 'has' : ''}`} aria-label={`Filter by ${f.label}`} onClick={(e) => { e.stopPropagation(); setOpenFacet(open ? null : f.key) }}>
                  <span>{summ}</span><span className={`mchev ${open ? 'up' : ''}`}><Icon name="chev" size={12} /></span>
                </button>
                {open && (
                  <div className="mpop" onClick={(e) => e.stopPropagation()}>
                    {uniq(f).length === 0 && <div className="mopt hint">No values</div>}
                    {uniq(f).map((v) => (
                      <label className="mopt" key={v}>
                        <input type="checkbox" checked={s.includes(v)} onChange={(e) => toggleVal(f.key, v, e.target.checked)} /> {v}
                      </label>
                    ))}
                    {s.length > 0 && <div className="mpopf"><button type="button" onClick={() => setSel((x) => ({ ...x, [f.key]: [] }))}>Clear</button></div>}
                  </div>
                )}
              </div>
            )
          })}
          <button type="button" className="freset" onClick={reset}>Reset</button>
        </div>
      )}

      {alternateBody ? alternateBody(filtered) : (
        <div className="card">
          <table>
            <thead>
              <tr>
                {bulkSelect && (
                  <th style={{ width: 36 }}>
                    <input
                      type="checkbox" style={{ width: 'auto' }} aria-label="Select all"
                      checked={allSelected}
                      ref={(el) => { if (el) el.indeterminate = !allSelected && someSelected }}
                      disabled={selectableRows.length === 0}
                      onChange={(e) => {
                        const next = new Set(bulkSelect.selected)
                        for (const r of selectableRows) { if (e.target.checked) next.add(rowKey(r)); else next.delete(rowKey(r)) }
                        bulkSelect.onChange(next)
                      }}
                    />
                  </th>
                )}
                {columns.map(headerCell)}
                {rowActions && <th />}
              </tr>
            </thead>
            <tbody>
              {filtered.map((r) => {
                const k = rowKey(r)
                const canSelect = bulkSelect ? (bulkSelect.selectable?.(r) ?? true) : false
                return (
                  <tr key={k}>
                    {bulkSelect && (
                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="checkbox" style={{ width: 'auto' }}
                          aria-label={bulkSelect.ariaLabel?.(r) ?? `Select ${k}`}
                          checked={bulkSelect.selected.has(k)}
                          disabled={!canSelect}
                          onChange={(e) => {
                            const next = new Set(bulkSelect.selected)
                            if (e.target.checked) next.add(k); else next.delete(k)
                            bulkSelect.onChange(next)
                          }}
                        />
                      </td>
                    )}
                    {columns.map((c) => <td key={c.key} className={c.amt ? 'amt' : undefined}>{c.render(r)}</td>)}
                    {rowActions && <td className="amt"><div className="rowactions">{rowActions(r)}</div></td>}
                  </tr>
                )
              })}
              {filtered.length === 0 && (
                <tr><td colSpan={totalCols}><EmptyState>{rows.length === 0 ? emptyNone : emptyFiltered}</EmptyState></td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {children}
    </>
  )
}

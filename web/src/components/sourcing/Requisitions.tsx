import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRequisitions, createRfqDraft, cancelPrLine, releasePrLine, reopenPrLine,
  type RequisitionDto, type RfqLineDto,
} from '../../api/client'
import { Icon } from '../Icon'
import { Modal } from '../ui'
import { PrForm } from './PrForm'
import { PrHeaderBadge, LineChip, PR_KANBAN_COLUMNS, isLineSourceable } from '../../lib/prStatus'

type PrLine = NonNullable<RequisitionDto['lines']>[number]
const lstat = (l: PrLine) => l.status ?? 'available'
const isAvailable = (l: PrLine) => lstat(l) === 'available'   // line-level display only
// Source-eligible lines: Open AND the PR header is Submitted/PartiallySourced (shared gate).
const availLines = (pr: RequisitionDto) => (pr.lines ?? []).filter((l) => isLineSourceable(pr, l))
const lineKey = (prCode: string, itemCode: string) => `${prCode}:${itemCode}`

type FilterKey = 'requestor' | 'department' | 'location' | 'job' | 'category' | 'headerStatus'
const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'requestor', label: 'Requestor' },
  { key: 'department', label: 'Dept' },
  { key: 'location', label: 'Location' },
  { key: 'job', label: 'Job' },
  { key: 'category', label: 'Category' },
  { key: 'headerStatus', label: 'Status' },
]
const emptyFilters = (): Record<FilterKey, string[]> => ({ requestor: [], department: [], location: [], job: [], category: [], headerStatus: [] })
// Header-status tokens get friendly labels in the filter (e.g. PartiallySourced → "Partially sourced").
const STATUS_LABELS: Record<string, string> = Object.fromEntries(PR_KANBAN_COLUMNS.map((c) => [c.key, c.label]))
const filterLabel = (key: FilterKey, v: string) => (key === 'headerStatus' ? STATUS_LABELS[v] ?? v : v)

type LineModal = { kind: 'cancel' | 'release'; prId: string; lineId: string; item: string }

export function Requisitions({ onOpenRfq, onConsolidate, addTo }: {
  onOpenRfq: (id: string) => void
  onConsolidate?: () => void
  addTo?: { existing: Set<string>; onAdd: (lines: RfqLineDto[]) => void }
}) {
  const qc = useQueryClient()
  const standalone = !addTo   // PR-management features only outside the RFQ add-lines picker
  const { data: prs = [] } = useQuery({ queryKey: ['requisitions'], queryFn: getRequisitions })
  // Source-eligible (header-gated) AND, in "add to draft" mode, not already in the draft.
  const selectable = (pr: RequisitionDto, l: PrLine) => isLineSourceable(pr, l) && !(addTo?.existing.has(lineKey(pr.code!, l.itemCode!)))
  const avail = (pr: RequisitionDto) => (pr.lines ?? []).filter((l) => selectable(pr, l))

  const [filters, setFilters] = useState<Record<FilterKey, string[]>>(emptyFilters)
  // Management view (standalone) shows all PRs by default so Drafts are visible to open/submit;
  // the line-picker (addTo) defaults to source-eligible lines only.
  const [show, setShow] = useState<'avail' | 'all'>(addTo ? 'avail' : 'all')
  const [openFilter, setOpenFilter] = useState<FilterKey | null>(null)
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [grouped, setGrouped] = useState<Set<string>>(new Set())
  const [drawer, setDrawer] = useState(false)
  const [lineSel, setLineSel] = useState<Set<string>>(new Set())

  // PR-management state (standalone only)
  const [view, setView] = useState<'table' | 'kanban'>('table')
  const [editingId, setEditingId] = useState<string | null | undefined>(undefined) // undefined=list · null=new · id=edit
  const [lineModal, setLineModal] = useState<LineModal | null>(null)
  const [reason, setReason] = useState('')

  const invalidate = () => qc.invalidateQueries({ queryKey: ['requisitions'] })
  const lineMut = useMutation({
    mutationFn: (m: LineModal & { reason: string }) =>
      m.kind === 'cancel' ? cancelPrLine(m.prId, m.lineId, m.reason) : releasePrLine(m.prId, m.lineId, m.reason),
    onSuccess: () => { void invalidate(); setLineModal(null); setReason('') },
  })
  const reopenMut = useMutation({
    mutationFn: (v: { prId: string; lineId: string }) => reopenPrLine(v.prId, v.lineId),
    onSuccess: () => void invalidate(),
  })

  const uniq = (key: FilterKey) => [...new Set(prs.map((p) => (p[key] ?? '') as string).filter(Boolean))]
  const inFilter = (sel: string[], v: string) => sel.length === 0 || sel.includes(v)
  const list = prs.filter((p) =>
    FILTERS.every((f) => inFilter(filters[f.key], (p[f.key] ?? '') as string)) &&
    (show === 'all' || availLines(p).length > 0))

  const toggleFilterVal = (key: FilterKey, v: string, on: boolean) =>
    setFilters((f) => ({ ...f, [key]: on ? [...f[key], v] : f[key].filter((x) => x !== v) }))
  const reset = () => { setFilters(emptyFilters()); setShow(addTo ? 'avail' : 'all'); setOpenFilter(null) }

  const setMember = (s: Set<string>, code: string, on: boolean) => { const n = new Set(s); if (on) n.add(code); else n.delete(code); return n }
  const toggleExpand = (code: string) => setExpanded((p) => setMember(p, code, !p.has(code)))
  const groupable = list.filter((p) => avail(p).length > 0)
  const allGrouped = groupable.length > 0 && groupable.every((p) => grouped.has(p.code!))
  const someGrouped = groupable.some((p) => grouped.has(p.code!))
  const setGroup = (code: string, on: boolean) => setGrouped((p) => setMember(p, code, on))
  const selectAllPrs = (on: boolean) => setGrouped((p) => { const n = new Set(p); groupable.forEach((pr) => { if (on) n.add(pr.code!); else n.delete(pr.code!) }); return n })

  const groupedPrs = prs.filter((p) => grouped.has(p.code!))
  const gLines = groupedPrs.reduce((n, p) => n + avail(p).length, 0)

  const openDrawer = () => {
    const sel = new Set<string>()
    groupedPrs.forEach((pr) => (pr.lines ?? []).forEach((l) => { if (selectable(pr, l)) sel.add(lineKey(pr.code!, l.itemCode!)) }))
    setLineSel(sel)
    setDrawer(true)
  }
  const toggleLine = (key: string, on: boolean) => setLineSel((p) => setMember(p, key, on))
  const selAllForPr = (pr: RequisitionDto) => {
    const keys = avail(pr).map((l) => lineKey(pr.code!, l.itemCode!))
    const allOn = keys.every((k) => lineSel.has(k))
    setLineSel((p) => { const n = new Set(p); keys.forEach((k) => (allOn ? n.delete(k) : n.add(k))); return n })
  }
  const markAll = (on: boolean) => {
    const keys: string[] = []
    groupedPrs.forEach((pr) => avail(pr).forEach((l) => keys.push(lineKey(pr.code!, l.itemCode!))))
    setLineSel(() => new Set(on ? keys : []))
  }
  const gAvail = groupedPrs.reduce((n, p) => n + avail(p).length, 0)
  const selCount = [...lineSel].length

  const selectedLines = () => groupedPrs.flatMap((pr) => (pr.lines ?? [])
    .filter((l) => lineSel.has(lineKey(pr.code!, l.itemCode!)))
    .map((l) => ({ itemCode: l.itemCode!, description: l.description ?? '', qty: l.qty ?? 0, uom: l.uom ?? 'Unit', prRef: pr.code })))

  const create = useMutation({
    mutationFn: () => {
      const lines = selectedLines()
      const prRefs = [...new Set(lines.map((l) => l.prRef!).filter(Boolean))]
      return createRfqDraft({ title: '', prRefs, lines })
    },
    onSuccess: (rfq) => rfq.id && onOpenRfq(rfq.id),
  })
  const confirmPrimary = () => { if (addTo) addTo.onAdd(selectedLines()); else create.mutate() }

  // PR create/edit form takes over the page (standalone only).
  if (standalone && editingId !== undefined) {
    return <PrForm id={editingId} onBack={() => { setEditingId(undefined); void invalidate() }} />
  }

  const cols = standalone ? 10 : 9

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Requisitions</h1>
          <p>{standalone
            ? 'All purchase requisitions. Expand a PR to act on its lines; group PRs to source them.'
            : 'All purchase requisitions. Group PRs, then pick lines to source.'}</p>
        </div>
        {standalone && (
          <>
            <div className="spacer" />
            <div className="viewtoggle">
              <button type="button" className={view === 'table' ? 'on' : ''} onClick={() => setView('table')}><Icon name="doc" size={14} /> Table</button>
              <button type="button" className={view === 'kanban' ? 'on' : ''} onClick={() => setView('kanban')}><Icon name="dashboard" size={14} /> Board</button>
            </div>
            {onConsolidate && <button type="button" className="btn btn-out btn-sm" onClick={onConsolidate}><Icon name="box" size={14} /> Build RFQ</button>}
            <button type="button" className="btn btn-pri btn-sm" onClick={() => setEditingId(null)}><Icon name="plus" size={15} /> Create PR</button>
          </>
        )}
      </div>

      {openFilter && <div className="mscrim" onClick={() => setOpenFilter(null)} />}
      <div className="filterbar2">
        {FILTERS.map((f) => {
          const sel = filters[f.key]
          const summ = sel.length === 0 ? 'All' : sel.length === 1 ? filterLabel(f.key, sel[0]) : `${sel.length} selected`
          const open = openFilter === f.key
          return (
            <div className="msel" key={f.key}>
              <label>{f.label}</label>
              <button type="button" className={`mbtn ${sel.length ? 'has' : ''}`} aria-label={`Filter by ${f.label}`} onClick={(e) => { e.stopPropagation(); setOpenFilter(open ? null : f.key) }}>
                <span>{summ}</span><span className={`mchev ${open ? 'up' : ''}`}><Icon name="chev" size={12} /></span>
              </button>
              {open && (
                <div className="mpop" onClick={(e) => e.stopPropagation()}>
                  {uniq(f.key).map((v) => (
                    <label className="mopt" key={v}>
                      <input type="checkbox" checked={sel.includes(v)} onChange={(e) => toggleFilterVal(f.key, v, e.target.checked)} /> {filterLabel(f.key, v)}
                    </label>
                  ))}
                  {sel.length > 0 && <div className="mpopf"><button type="button" onClick={() => setFilters((x) => ({ ...x, [f.key]: [] }))}>Clear</button></div>}
                </div>
              )}
            </div>
          )
        })}
        <div className="msel">
          <label>Show</label>
          <select value={show} onChange={(e) => setShow(e.target.value as 'avail' | 'all')} style={{ minWidth: 170 }}>
            <option value="avail">With available lines</option>
            <option value="all">All PRs</option>
          </select>
        </div>
        <button type="button" className="freset" onClick={reset}>Reset</button>
      </div>

      {standalone && view === 'kanban' ? (
        <div className="kanban">
          {PR_KANBAN_COLUMNS.map((col) => {
            const items = list.filter((p) => p.headerStatus === col.key)
            return (
              <div className="kcol" key={col.key}>
                <div className="khead">{col.label}<span className="kcount">{items.length}</span></div>
                <div className="kbody">
                  {items.map((pr) => (
                    <div className="kcard" key={pr.id} onClick={() => pr.id && setEditingId(pr.id)}>
                      <div className="kc-top"><span className="kc-code">{pr.code}</span><span className="hint">{pr.requiredDate}</span></div>
                      <div className="kc-title">{pr.memo || '—'}</div>
                      <div className="kc-meta">{pr.requestor} · {pr.department}</div>
                    </div>
                  ))}
                  {items.length === 0 && <div className="kempty">—</div>}
                </div>
              </div>
            )
          })}
        </div>
      ) : (
        <>
          {grouped.size > 0 ? (
            <div className="abar2 live">
              <span className="info"><b>{grouped.size}</b> PR grouped · <b>{gLines}</b> available lines</span>
              <div style={{ flex: 1 }} />
              <button type="button" className="btn btn-pri" onClick={openDrawer}><Icon name="rfq" size={15} /> Confirm lines</button>
            </div>
          ) : (
            <div className="abar2">
              <span className="info">Tick the PRs you want to source, then confirm which lines go into the RFQ.</span>
              <div style={{ flex: 1 }} />
              <button type="button" className="btn btn-pri" disabled><Icon name="rfq" size={15} /> Confirm lines</button>
            </div>
          )}

          <div className="actbar" style={{ justifyContent: 'flex-end', marginBottom: 8 }}>
            <button type="button" className="freset" onClick={() => setExpanded(new Set(list.map((p) => p.code!)))}>Expand all</button>
            <button type="button" className="freset" onClick={() => setExpanded(new Set())}>Collapse all</button>
          </div>

          <div className="card">
            <table>
              <thead>
                <tr>
                  <th style={{ width: 34 }}><input type="checkbox" style={{ width: 'auto' }} checked={allGrouped} ref={(el) => { if (el) el.indeterminate = someGrouped && !allGrouped }} onChange={(e) => selectAllPrs(e.target.checked)} aria-label="Select all PRs" /></th>
                  <th>PR #</th><th>Requestor</th><th>Dept</th><th>Location</th><th>Job</th><th>Category</th><th>Required by</th>
                  {standalone ? <th>PR status</th> : <th>Lines</th>}
                  {standalone && <th />}
                </tr>
              </thead>
              <tbody>
                {list.map((pr) => {
                  const open = expanded.has(pr.code!)
                  const canGroup = availLines(pr).length > 0
                  const a = (pr.lines ?? []).filter((l) => lstat(l) === 'available').length
                  const r = (pr.lines ?? []).filter((l) => lstat(l) === 'rfq').length
                  const c = (pr.lines ?? []).length - a - r
                  return (
                    <FragmentRows key={pr.id}>
                      <tr className={`prrow rowlink ${open ? 'open' : ''} ${grouped.has(pr.code!) ? 'grp' : ''}`} onClick={() => toggleExpand(pr.code!)}>
                        <td onClick={(e) => e.stopPropagation()}><input type="checkbox" style={{ width: 'auto' }} disabled={!canGroup} checked={grouped.has(pr.code!)} onChange={(e) => setGroup(pr.code!, e.target.checked)} aria-label={`Group ${pr.code}`} /></td>
                        <td><span className="chev"><Icon name="chev" size={13} /></span> <span style={{ fontWeight: 700, color: 'var(--teal)' }}>{pr.code}</span></td>
                        <td>{pr.requestor}</td><td>{pr.department}</td><td title={pr.memo ?? ''}>{pr.location}</td><td>{pr.job}</td><td>{pr.category}</td><td>{pr.requiredDate}</td>
                        {standalone ? (
                          <td><PrHeaderBadge status={pr.headerStatus} /></td>
                        ) : (
                          <td>
                            {a > 0 && <span className="lst a"><span className="d" />{a}</span>}
                            {r > 0 && <span className="lst r"><span className="d" />{r}</span>}
                            {c > 0 && <span className="lst c"><span className="d" />{c}</span>}
                            {a + r + c === 0 && <span className="hint">—</span>}
                          </td>
                        )}
                        {standalone && (
                          <td className="amt" onClick={(e) => e.stopPropagation()}>
                            <div className="rowactions">
                              <button type="button" className="btn btn-ghost btn-sm" onClick={() => pr.id && setEditingId(pr.id)}><Icon name="edit" size={14} /> Open</button>
                            </div>
                          </td>
                        )}
                      </tr>
                      {open && (
                        <tr><td colSpan={cols} style={{ padding: 0, background: '#fbfbf9' }}>
                          <table>
                            <thead><tr><th>Item</th><th>Description</th><th className="amt">Qty</th><th>UoM</th><th className="amt">Est. amount</th><th>Line status</th>{standalone && <th>Action</th>}</tr></thead>
                            <tbody>
                              {(pr.lines ?? []).map((l) => (
                                <tr key={l.itemCode}>
                                  <td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.qty}</td><td>{l.uom}</td>
                                  <td className="amt">{((l.qty ?? 0) * (l.estUnitPrice ?? 0)).toLocaleString()}</td>
                                  <td>{standalone
                                    ? <LineChip line={l} />
                                    : isAvailable(l)
                                      ? <span className="lst a"><span className="d" />Available</span>
                                      : lstat(l) === 'rfq'
                                        ? <span className="lst r"><span className="d" />{l.ref ?? 'In RFQ'}</span>
                                        : <span className="lst c"><span className="d" />{l.ref ?? 'Closed'}</span>}
                                  </td>
                                  {standalone && (
                                    <td className="amt">
                                      <div className="rowactions">
                                        {l.lifecycleStatus === 'Open' && !l.noQuotes && (
                                          <button type="button" className="btn btn-ghost btn-sm" style={{ color: 'var(--red)' }} onClick={() => { setReason(''); setLineModal({ kind: 'cancel', prId: pr.id!, lineId: l.id!, item: l.description ?? '' }) }}>Cancel line</button>
                                        )}
                                        {l.lifecycleStatus === 'Open' && l.noQuotes && (
                                          <>
                                            <button type="button" className="btn btn-out btn-sm" onClick={() => { setReason(''); setLineModal({ kind: 'release', prId: pr.id!, lineId: l.id!, item: l.description ?? '' }) }}>Release for re-sourcing</button>
                                            <button type="button" className="btn btn-ghost btn-sm" style={{ color: 'var(--red)' }} onClick={() => { setReason(''); setLineModal({ kind: 'cancel', prId: pr.id!, lineId: l.id!, item: l.description ?? '' }) }}>Cancel line</button>
                                          </>
                                        )}
                                        {l.lifecycleStatus === 'InRfq' && <span className="hint">Locked to {l.ref ?? 'RFQ'} (live)</span>}
                                        {l.lifecycleStatus === 'Awarded' && <span className="hint">Awarded — PO issued</span>}
                                        {l.lifecycleStatus === 'Cancelled' && <button type="button" className="btn btn-ghost btn-sm" onClick={() => reopenMut.mutate({ prId: pr.id!, lineId: l.id! })}>Re-open</button>}
                                      </div>
                                    </td>
                                  )}
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </td></tr>
                      )}
                    </FragmentRows>
                  )
                })}
                {list.length === 0 && <tr><td colSpan={cols} style={{ padding: 24, textAlign: 'center', color: 'var(--muted)' }}>No PRs match these filters.</td></tr>}
              </tbody>
            </table>
          </div>
        </>
      )}

      {drawer && (
        <>
          <div className="rqscrim" onClick={() => setDrawer(false)} />
          <div className="rqdrawer">
            <div className="dh"><Icon name="rfq" size={18} /><h3>{addTo ? 'Add lines to RFQ' : 'Select lines for RFQ'}</h3><button type="button" className="x" onClick={() => setDrawer(false)}>×</button></div>
            <div className="dsub"><span className="lbl">{gAvail} available {gAvail === 1 ? 'line' : 'lines'} across {groupedPrs.length} {groupedPrs.length === 1 ? 'PR' : 'PRs'}</span><div style={{ flex: 1 }} /><button type="button" onClick={() => markAll(true)}>Mark all</button><button type="button" onClick={() => markAll(false)}>Unmark all</button></div>
            <div className="db">
              {groupedPrs.map((pr) => (
                <div className="psec2" key={pr.id}>
                  <div className="ph"><span className="pid">{pr.code}</span><span className="pc">{pr.requestor} · {pr.category}</span><button type="button" className="selall" onClick={() => selAllForPr(pr)}>Select all available</button></div>
                  {(pr.lines ?? []).map((l) => {
                    const key = lineKey(pr.code!, l.itemCode!)
                    if (selectable(pr, l)) {
                      return (
                        <label className="lrow2" key={l.itemCode}>
                          <input type="checkbox" checked={lineSel.has(key)} onChange={(e) => toggleLine(key, e.target.checked)} aria-label={`Select ${l.itemCode}`} />
                          <span className="lc"><div className="desc">{l.description}</div><div className="sub">{l.itemCode} · {l.qty} {l.uom}</div></span>
                        </label>
                      )
                    }
                    const inThisDraft = addTo?.existing.has(key)
                    return (
                      <div className="lrow2 lock" key={l.itemCode}>
                        <span style={{ width: 15 }} />
                        <span className="lc"><div className="desc">{l.description}</div><div className="lockchip"><Icon name="lock" size={12} /> {inThisDraft ? 'Already in this RFQ' : lstat(l) === 'rfq' ? `In ${l.ref ?? 'RFQ'}` : lstat(l) === 'awarded' ? `Awarded · ${l.ref ?? ''}` : 'Sourced'}</div></span>
                      </div>
                    )
                  })}
                </div>
              ))}
              {groupedPrs.length === 0 && <div style={{ padding: 24, color: 'var(--muted)' }}>No grouped PRs.</div>}
            </div>
            <div className="df">
              <div className="note"><Icon name="eye" size={13} /> Unticked lines stay available for future RFQs.</div>
              <div className="frow">
                <span className="tot">{selCount} {selCount === 1 ? 'line' : 'lines'} selected</span>
                <div style={{ flex: 1 }} />
                <button type="button" className="btn btn-out" onClick={() => setDrawer(false)}>Cancel</button>
                <button type="button" className="btn btn-pri" disabled={selCount === 0} onClick={confirmPrimary}>{addTo ? `Add ${selCount} ${selCount === 1 ? 'line' : 'lines'} to RFQ` : 'Create RFQ →'}</button>
              </div>
            </div>
          </div>
        </>
      )}

      {lineModal && (
        <Modal
          title={lineModal.kind === 'cancel' ? 'Cancel line' : 'Release for re-sourcing'}
          icon={lineModal.kind === 'cancel' ? 'x' : 'rfq'}
          footer={<>
            <button type="button" className="btn btn-out" onClick={() => setLineModal(null)}>{lineModal.kind === 'cancel' ? 'Keep line' : 'Cancel'}</button>
            <button type="button" className={`btn btn-pri${lineModal.kind === 'cancel' ? ' btn-danger' : ''}`} disabled={lineMut.isPending} onClick={() => lineMut.mutate({ ...lineModal, reason })}>
              {lineModal.kind === 'cancel' ? 'Cancel line' : 'Release to Open'}
            </button>
          </>}
        >
          <p style={{ marginTop: 0 }}>
            {lineModal.kind === 'cancel'
              ? <>Cancel <b>{lineModal.item}</b>? The demand is withdrawn — this is terminal for the line.</>
              : <>Release <b>{lineModal.item}</b> back to <b>Open</b> so it can go into a new RFQ? Its sourcing link is marked <b>Returned</b>; provenance is preserved.</>}
          </p>
          <div className="field"><label>Reason</label>
            <textarea rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder={lineModal.kind === 'cancel' ? 'e.g. Duplicate / no longer required' : 'e.g. No vendor quoted this line'} />
          </div>
        </Modal>
      )}
    </>
  )
}

// React fragment wrapper that accepts a key, so we can return paired rows.
function FragmentRows({ children }: { children: React.ReactNode }) {
  return <>{children}</>
}

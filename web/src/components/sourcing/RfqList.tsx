import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getRfqs, closeRfq, cancelRfq, type RfqListItem } from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, EmptyState } from '../ui'
import { RfqStatusBadge, EnvTag, BidProgressBadge } from '../../lib/rfqStatus'
import { fmtDay } from '../../lib/format'

const BOARD_COLS: { status: string; label: string }[] = [
  { status: 'Draft', label: 'Draft' },
  { status: 'Open', label: 'Open · awaiting bids' },
  { status: 'Closed', label: 'Closed · ready to open' },
  { status: 'Evaluation', label: 'Under evaluation' },
  { status: 'Awarded', label: 'Awarded' },
  { status: 'Cancelled', label: 'Cancelled' },
]

type FKey = 'envelope' | 'closes' | 'status'
const FILTERS: { key: FKey; label: string }[] = [
  { key: 'envelope', label: 'Envelope' },
  { key: 'closes', label: 'Closes' },
  { key: 'status', label: 'Status' },
]
const facetVal = (r: RfqListItem, key: FKey): string =>
  key === 'closes' ? (r.closesUtc ? fmtDay(r.closesUtc) : '') : ((r[key] ?? '') as string)

export function RfqList({ onOpen, onNew }: { onOpen: (id: string) => void; onNew: () => void }) {
  const qc = useQueryClient()
  const { data: rfqs = [] } = useQuery({ queryKey: ['rfqs'], queryFn: getRfqs })
  const [confirm, setConfirm] = useState<{ kind: 'close' | 'cancel'; rfq: RfqListItem } | null>(null)
  const [view, setView] = useState<'table' | 'board'>('table')
  const [search, setSearch] = useState('')
  const [filters, setFilters] = useState<Record<FKey, string[]>>({ envelope: [], closes: [], status: [] })
  const [openFilter, setOpenFilter] = useState<FKey | null>(null)
  const inval = () => { void qc.invalidateQueries({ queryKey: ['rfqs'] }) }
  const close = useMutation({ mutationFn: (id: string) => closeRfq(id), onSuccess: () => { inval(); setConfirm(null) } })
  const cancel = useMutation({ mutationFn: (id: string) => cancelRfq(id), onSuccess: () => { inval(); setConfirm(null) } })

  const uniq = (key: FKey) => [...new Set(rfqs.map((r) => facetVal(r, key)).filter(Boolean))]
  const matchSearch = (r: RfqListItem) => {
    const q = search.trim().toLowerCase()
    return !q || [r.code, r.title].some((v) => (v ?? '').toLowerCase().includes(q))
  }
  const filtered = rfqs.filter((r) =>
    FILTERS.every((f) => filters[f.key].length === 0 || filters[f.key].includes(facetVal(r, f.key))) && matchSearch(r))
  const reset = () => { setFilters({ envelope: [], closes: [], status: [] }); setSearch('') }
  const toggleVal = (key: FKey, v: string, on: boolean) =>
    setFilters((x) => ({ ...x, [key]: on ? [...x[key], v] : x[key].filter((y) => y !== v) }))

  return (
    <>
      <div className="pagehead">
        <div><h1>RFQs</h1><p>All sourcing events and their stage.</p></div>
        <div className="spacer" />
        <div className="viewtoggle">
          <button type="button" className={view === 'table' ? 'on' : ''} onClick={() => setView('table')} title="Table view"><Icon name="doc" size={14} /> Table</button>
          <button type="button" className={view === 'board' ? 'on' : ''} onClick={() => setView('board')} title="Board view"><Icon name="dashboard" size={14} /> Board</button>
        </div>
        <button type="button" className="btn btn-pri btn-sm" onClick={onNew}><Icon name="plus" size={15} /> New RFQ</button>
      </div>

      {openFilter && <div className="mscrim" onClick={() => setOpenFilter(null)} />}
      <div className="filterbar2">
        <div className="msel" style={{ flex: 1, minWidth: 200 }}>
          <label>RFQ #</label>
          <input type="text" value={search} placeholder="RFQ number or title…" aria-label="Search RFQs" onChange={(e) => setSearch(e.target.value)} />
        </div>
        {FILTERS.map((f) => {
          const sel = filters[f.key]
          const summ = sel.length === 0 ? 'All' : sel.length === 1 ? sel[0] : `${sel.length} selected`
          const open = openFilter === f.key
          return (
            <div className="msel" key={f.key}>
              <label>{f.label}</label>
              <button type="button" className={`mbtn ${sel.length ? 'has' : ''}`} aria-label={`Filter by ${f.label}`} onClick={(e) => { e.stopPropagation(); setOpenFilter(open ? null : f.key) }}>
                <span>{summ}</span><span className={`mchev ${open ? 'up' : ''}`}><Icon name="chev" size={12} /></span>
              </button>
              {open && (
                <div className="mpop" onClick={(e) => e.stopPropagation()}>
                  {uniq(f.key).length === 0 && <div className="mopt hint">No values</div>}
                  {uniq(f.key).map((v) => (
                    <label className="mopt" key={v}>
                      <input type="checkbox" checked={sel.includes(v)} onChange={(e) => toggleVal(f.key, v, e.target.checked)} /> {v}
                    </label>
                  ))}
                  {sel.length > 0 && <div className="mpopf"><button type="button" onClick={() => setFilters((x) => ({ ...x, [f.key]: [] }))}>Clear</button></div>}
                </div>
              )}
            </div>
          )
        })}
        <button type="button" className="freset" onClick={reset}>Reset</button>
      </div>

      {view === 'table' ? (
        <div className="card">
          <table>
            <thead>
              <tr><th>RFQ</th><th>Title</th><th>Envelope</th><th className="amt">Vendors</th><th>Bid progress</th><th>Closes</th><th>Status</th><th /></tr>
            </thead>
            <tbody>
              {filtered.map((r: RfqListItem) => {
                const draft = r.status === 'Draft'
                const open = r.status === 'Open'
                return (
                  <tr key={r.id}>
                    <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{r.code}</td>
                    <td>{r.title}</td>
                    <td><EnvTag envelope={r.envelope} /></td>
                    <td className="amt">{r.invitedCount}</td>
                    <td>{open ? <BidProgressBadge bidCount={r.bidCount} invitedCount={r.invitedCount} /> : draft ? '—' : `${r.bidCount}/${r.invitedCount}`}</td>
                    <td>{draft ? '—' : fmtDay(r.closesUtc)}</td>
                    <td><RfqStatusBadge status={r.status} /></td>
                    <td className="amt">
                      <div className="rowactions">
                        {open && <button type="button" className="btn btn-out btn-sm" onClick={() => setConfirm({ kind: 'close', rfq: r })}><Icon name="clock" size={13} /> Close bids</button>}
                        {(open || draft) && <button type="button" className="btn btn-ghost btn-sm" style={{ color: 'var(--red)' }} onClick={() => setConfirm({ kind: 'cancel', rfq: r })}>Cancel</button>}
                        {draft ? (
                          <button type="button" className="btn btn-pri btn-sm" onClick={() => r.id && onOpen(r.id)}><Icon name="edit" size={14} /> Continue</button>
                        ) : (
                          <button type="button" className="btn btn-ghost btn-sm" onClick={() => r.id && onOpen(r.id)}>Open <Icon name="chev" size={14} /></button>
                        )}
                      </div>
                    </td>
                  </tr>
                )
              })}
              {filtered.length === 0 && <tr><td colSpan={8}><EmptyState>{rfqs.length === 0 ? 'No RFQs yet — create one from Requisitions.' : 'No RFQs match these filters.'}</EmptyState></td></tr>}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="kanban">
          {BOARD_COLS.map((col) => {
            const cards = filtered.filter((r) => r.status === col.status)
            return (
              <div className="kcol" key={col.status}>
                <div className="khead"><span className={`kdot ${col.status.toLowerCase()}`} />{col.label}<span className="kcount">{cards.length}</span></div>
                <div className="kbody">
                  {cards.map((r) => (
                    <div className="kcard" key={r.id} onClick={() => r.id && onOpen(r.id)}>
                      <div className="kc-top"><span className="kc-code">{r.code}</span><EnvTag envelope={r.envelope} /></div>
                      <div className="kc-title">{r.title || '—'}</div>
                      <div className="kc-meta">
                        {r.status === 'Open' ? <BidProgressBadge bidCount={r.bidCount} invitedCount={r.invitedCount} />
                          : <span className="hint">{r.invitedCount} vendor(s){r.status !== 'Draft' ? ` · ${r.bidCount}/${r.invitedCount} bids` : ''}</span>}
                      </div>
                    </div>
                  ))}
                  {cards.length === 0 && <div className="kempty">—</div>}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {confirm?.kind === 'close' && (
        <ConfirmModal
          icon="clock" title={`Close bids for ${confirm.rfq.code}?`}
          body={`This closes the bid window now (ahead of ${confirm.rfq.closesUtc ? fmtDay(confirm.rfq.closesUtc) : 'the deadline'}) — useful when all invited vendors have already submitted (${confirm.rfq.bidCount}/${confirm.rfq.invitedCount}). The RFQ moves to ready to open.`}
          cancelLabel="Not yet" confirmLabel="Close bids now" confirmIcon="clock" busy={close.isPending}
          onCancel={() => setConfirm(null)} onConfirm={() => confirm.rfq.id && close.mutate(confirm.rfq.id)}
        />
      )}
      {confirm?.kind === 'cancel' && (
        <ConfirmModal
          icon="x" title={`Cancel ${confirm.rfq.code}?`}
          body="This cancels the RFQ. Invited vendors can no longer bid and it won't proceed to evaluation. This cannot be undone."
          cancelLabel="Keep RFQ" confirmLabel="Cancel RFQ" danger busy={cancel.isPending}
          onCancel={() => setConfirm(null)} onConfirm={() => confirm.rfq.id && cancel.mutate(confirm.rfq.id)}
        />
      )}
    </>
  )
}

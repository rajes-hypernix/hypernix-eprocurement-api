import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getRfq, closeRfq, cancelRfq, getViews, runView, type RfqListItem, type SavedViewDto } from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal } from '../ui'
import { RfqStatusBadge, EnvTag, BidProgressBadge } from '../../lib/rfqStatus'
import { fmtDay } from '../../lib/format'
import { ListPage, type ListColumn, type ListFacet } from '../../ui/archetypes/ListPage'
import { QuickView } from '../../ui/QuickView'
import { Button } from '../../ui/Button'
import { ViewPicker, ViewBuilder } from '../views/SavedViewControls'

const BOARD_COLS: { status: string; label: string }[] = [
  { status: 'Draft', label: 'Draft' },
  { status: 'Open', label: 'Open · awaiting bids' },
  { status: 'Closed', label: 'Closed · ready to open' },
  { status: 'Evaluation', label: 'Under evaluation' },
  { status: 'Awarded', label: 'Awarded' },
  { status: 'Cancelled', label: 'Cancelled' },
]

/** The seeded system view's columns — the column seed for NEW views built from this screen. */
const DEFAULT_RFQ_COLUMNS = ['Code', 'Title', 'Envelope', 'InvitedCount', 'BidCount', 'ClosesUtc', 'Status']

/** Run-result row → the typed item this screen renders. Columns a picked view omits map to
 *  undefined and the cells fall back to "—" — the view controls rows; rendering stays typed. */
const toItem = (row: Record<string, unknown>): RfqListItem => ({
  id: row.Id as string,
  code: (row.Code as string | undefined) ?? null,
  title: (row.Title as string | undefined) ?? null,
  envelope: (row.Envelope as string | undefined) ?? null,
  status: (row.Status as string | undefined) ?? null,
  closesUtc: (row.ClosesUtc as string | undefined) ?? null,
  invitedCount: (row.InvitedCount as number | undefined) ?? 0,
  bidCount: (row.BidCount as number | undefined) ?? 0,
} as RfqListItem)

export function RfqList({ onOpen, onNew, initialViewId }: { onOpen: (id: string) => void; onNew: () => void; initialViewId?: string }) {
  const qc = useQueryClient()
  // Saved-view-driven (D3 proof screen): rows come from the picked view's run — the system
  // "All RFQs" view by default (reproduces the old getRfqs list exactly). The quick filters
  // below stay LAYERED on the run's rows, per the ruling (parity of the pinned e2e).
  const { data: views = [] } = useQuery({ queryKey: ['views', 'Rfq'], queryFn: () => getViews('Rfq') })
  const [picked, setPicked] = useState<string | null>(initialViewId ?? null)
  const viewId = picked ?? views.find((v) => v.isSystem)?.id ?? null
  const { data: run } = useQuery({
    queryKey: ['view-run', viewId], queryFn: () => runView(viewId!), enabled: viewId !== null,
  })
  const rfqs = (run?.rows ?? []).map(toItem)
  const [builder, setBuilder] = useState<{ open: boolean; existing: SavedViewDto | null }>({ open: false, existing: null })
  const [confirm, setConfirm] = useState<{ kind: 'close' | 'cancel'; rfq: RfqListItem } | null>(null)
  const [view, setView] = useState<'table' | 'board'>('table')
  const inval = () => { void qc.invalidateQueries({ queryKey: ['view-run'] }) }
  const close = useMutation({ mutationFn: (id: string) => closeRfq(id), onSuccess: () => { inval(); setConfirm(null) } })
  const cancel = useMutation({ mutationFn: (id: string) => cancelRfq(id), onSuccess: () => { inval(); setConfirm(null) } })

  // QuickView on RFQ code references — the existing GET detail endpoint, no new API.
  const preview = (r: RfqListItem) => async () => {
    const d = await getRfq(r.id!)
    return {
      title: d.title || d.code || '',
      fields: [
        { label: 'Status', value: d.status ?? '—' },
        { label: 'Envelope', value: d.envelope ?? '—' },
        { label: 'Closes', value: fmtDay(d.closesUtc) },
        { label: 'Invited', value: `${(d.invitations ?? []).length} vendor(s)` },
      ],
    }
  }

  const columns: ListColumn<RfqListItem>[] = [
    {
      key: 'code', header: 'RFQ',
      render: (r) => (
        <QuickView fetcher={preview(r)} title={r.code ?? undefined}>
          <span style={{ fontWeight: 700, color: 'var(--teal)' }}>{r.code}</span>
        </QuickView>
      ),
    },
    { key: 'title', header: 'Title', render: (r) => r.title },
    { key: 'envelope', header: 'Envelope', render: (r) => <EnvTag envelope={r.envelope} /> },
    { key: 'vendors', header: 'Vendors', amt: true, render: (r) => r.invitedCount },
    {
      key: 'progress', header: 'Bid progress',
      render: (r) => r.status === 'Open'
        ? <BidProgressBadge bidCount={r.bidCount} invitedCount={r.invitedCount} />
        : r.status === 'Draft' ? '—' : `${r.bidCount}/${r.invitedCount}`,
    },
    { key: 'closes', header: 'Closes', render: (r) => (r.status === 'Draft' ? '—' : fmtDay(r.closesUtc)) },
    { key: 'status', header: 'Status', render: (r) => <RfqStatusBadge status={r.status} /> },
  ]

  const facets: ListFacet<RfqListItem>[] = [
    { key: 'envelope', label: 'Envelope', value: (r) => r.envelope ?? '' },
    { key: 'closes', label: 'Closes', value: (r) => (r.closesUtc ? fmtDay(r.closesUtc) : '') },
    { key: 'status', label: 'Status', value: (r) => r.status ?? '' },
  ]

  const rowActions = (r: RfqListItem) => {
    const draft = r.status === 'Draft'
    const open = r.status === 'Open'
    return (
      <>
        {open && <Button variant="outline" size="sm" icon="clock" onClick={() => setConfirm({ kind: 'close', rfq: r })}>Close bids</Button>}
        {(open || draft) && <Button variant="ghost" size="sm" red onClick={() => setConfirm({ kind: 'cancel', rfq: r })}>Cancel</Button>}
        {draft
          ? <Button variant="primary" size="sm" icon="edit" onClick={() => r.id && onOpen(r.id)}>Continue</Button>
          : <Button variant="ghost" size="sm" onClick={() => r.id && onOpen(r.id)}>Open <Icon name="chev" size={14} /></Button>}
      </>
    )
  }

  const board = (filtered: RfqListItem[]) => (
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
  )

  return (
    <ListPage<RfqListItem>
      title="RFQs"
      subtitle="All sourcing events and their stage."
      viewToggle={
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <ViewPicker
            views={views}
            selectedId={viewId}
            onSelect={(id) => setPicked(id)}
            onNew={() => setBuilder({ open: true, existing: null })}
            onEdit={(v) => setBuilder({ open: true, existing: v })}
          />
          <div className="viewtoggle">
            <button type="button" className={view === 'table' ? 'on' : ''} onClick={() => setView('table')} title="Table view"><Icon name="doc" size={14} /> Table</button>
            <button type="button" className={view === 'board' ? 'on' : ''} onClick={() => setView('board')} title="Board view"><Icon name="dashboard" size={14} /> Board</button>
          </div>
        </div>
      }
      primaryAction={<Button variant="primary" size="sm" icon="plus" onClick={onNew}>New RFQ</Button>}
      searchLabel="RFQ #" searchPlaceholder="RFQ number or title…" searchAriaLabel="Search RFQs"
      searchMatch={(r, q) => [r.code, r.title].some((v) => (v ?? '').toLowerCase().includes(q))}
      facets={facets}
      rows={rfqs}
      rowKey={(r) => r.id ?? ''}
      columns={columns}
      rowActions={rowActions}
      emptyNone="No RFQs yet — create one from Requisitions."
      emptyFiltered="No RFQs match these filters."
      alternateBody={view === 'board' ? board : undefined}
    >
      {builder.open && (
        <ViewBuilder
          recordType="Rfq"
          existing={builder.existing}
          defaultColumns={DEFAULT_RFQ_COLUMNS}
          onClose={() => setBuilder({ open: false, existing: null })}
          onSaved={(v) => { setBuilder({ open: false, existing: null }); setPicked(v.id); void qc.invalidateQueries({ queryKey: ['view-run'] }) }}
        />
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
    </ListPage>
  )
}

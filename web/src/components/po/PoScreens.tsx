import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getPos, getPo, issuePo, acknowledgePo, getAsns, getInvoices, getPoAudit, type PoListItem } from '../../api/client'
import { Icon } from '../Icon'
import { CustomFieldsSection } from '../customfields/CustomFieldsSection'
import { ConfirmModal, EmptyState, Notice, Spinner } from '../ui'
import { fmt, fmtDay } from '../../lib/format'
import { useIdentity } from '../../identity'

const STATUS_TONE: Record<string, string> = {
  Draft: 'b-grey', Issued: 'b-blue', Acknowledged: 'b-blue', PartiallyReceived: 'b-amber',
  Received: 'b-blue', Matched: 'b-green', Closed: 'b-grey', Discrepancy: 'b-red',
}
const label = (s: string | null | undefined) => (s === 'PartiallyReceived' ? 'Partially received' : s ?? '')

export function PoPage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  if (route.startsWith('pos/'))
    return <PoDetailView id={route.slice('pos/'.length)} onBack={() => onNavigate('pos')} onNavigate={onNavigate} />
  return <PoList onOpen={(id) => onNavigate(`pos/${id}`)} />
}

const PO_OPEN = ['Issued', 'Acknowledged', 'PartiallyReceived', 'Received']
const PO_TABS: { key: string; label: string }[] = [
  { key: 'all', label: 'All' }, { key: 'open', label: 'Open' }, { key: 'exceptions', label: 'Exceptions' },
  { key: 'draft', label: 'Drafts' }, { key: 'closed', label: 'Closed' },
]
const poInFilter = (status: string, f: string) => {
  if (f === 'all') return true
  if (f === 'open') return PO_OPEN.includes(status)
  if (f === 'exceptions') return status === 'Discrepancy'
  if (f === 'draft') return status === 'Draft'
  if (f === 'closed') return ['Matched', 'Closed'].includes(status)
  return true
}

function PoList({ onOpen }: { onOpen: (id: string) => void }) {
  const { isVendor } = useIdentity()
  const { data: pos = [] } = useQuery({ queryKey: ['pos'], queryFn: getPos })
  const [tab, setTab] = useState('all')

  const shown = pos.filter((p) => poInFilter(p.status ?? '', tab))
  const exceptions = pos.filter((p) => p.status === 'Discrepancy').length
  const openVal = pos.filter((p) => !['Draft', 'Closed'].includes(p.status ?? '')).reduce((a, p) => a + (p.total ?? 0), 0)
  const awaiting = pos.filter((p) => ['Issued', 'Acknowledged', 'PartiallyReceived'].includes(p.status ?? '')).length

  return (
    <>
      <div className="pagehead"><div><h1>Purchase Orders</h1><p>PO lifecycle and 3-way matching — purchase order vs goods receipt vs supplier invoice.</p></div></div>
      {exceptions > 0 && (
        <div className="ribbon"><strong>{exceptions} invoice discrepancy(ies) pending exception review.</strong></div>
      )}

      {!isVendor && (
        <div className="grid g4" style={{ marginBottom: 16 }}>
          <div className="card stat"><div className="lbl">Total POs</div><div className="num">{pos.length}</div><div className="sub">all statuses</div></div>
          <div className="card stat"><div className="lbl">Open PO value</div><div className="num" style={{ fontSize: 20 }}>RM {fmt(openVal)}</div><div className="sub">issued, not closed</div></div>
          <div className="card stat"><div className="lbl">Awaiting receipt</div><div className="num">{awaiting}</div><div className="sub">in delivery</div></div>
          <div className="card stat"><div className="lbl">Exceptions</div><div className="num" style={{ color: exceptions ? 'var(--red)' : 'inherit' }}>{exceptions}</div><div className="sub">invoice variance</div></div>
        </div>
      )}

      <div style={{ display: 'flex', gap: 8, marginBottom: 14, flexWrap: 'wrap' }}>
        {PO_TABS.map((t) => (
          <button key={t.key} type="button" className={`btn btn-sm ${tab === t.key ? 'btn-pri' : 'btn-out'}`} onClick={() => setTab(t.key)}>{t.label}</button>
        ))}
      </div>

      <div className="card">
        <table>
          <thead>
            <tr><th>PO</th>{!isVendor && <th>Vendor</th>}<th>From RFQ</th><th className="amt">Value</th><th>Goods receipt</th><th>Status</th><th /></tr>
          </thead>
          <tbody>
            {shown.map((p: PoListItem) => {
              const pct = (p.totalQty ?? 0) > 0 ? Math.round(((p.receivedQty ?? 0) / (p.totalQty ?? 1)) * 100) : 0
              return (
                <tr key={p.id} className="rowlink" onClick={() => p.id && onOpen(p.id)}>
                  <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{p.code}</td>
                  {!isVendor && <td>{p.vendorName}</td>}
                  <td style={{ color: 'var(--teal)' }}>{p.rfqCode ?? '—'}</td>
                  <td className="amt">RM {fmt(p.total)}</td>
                  <td style={{ minWidth: 130 }}>
                    <div className="pbar"><i style={{ width: `${pct}%` }} /></div>
                    <div className="hint" style={{ marginTop: 3 }}>{p.receivedQty}/{p.totalQty} received</div>
                  </td>
                  <td><span className={`badge ${STATUS_TONE[p.status ?? ''] ?? 'b-grey'}`}>{label(p.status)}</span></td>
                  <td className="amt"><div className="rowactions"><span className="btn btn-ghost btn-sm">Open <Icon name="chev" size={13} /></span></div></td>
                </tr>
              )
            })}
            {shown.length === 0 && <tr><td colSpan={isVendor ? 6 : 7}><EmptyState>No POs in this view.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PoDetailView({ id, onBack, onNavigate }: { id: string; onBack: () => void; onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const { isVendor } = useIdentity()
  const { data: po, isPending } = useQuery({ queryKey: ['po', id], queryFn: () => getPo(id) })
  const { data: asns = [] } = useQuery({ queryKey: ['asns'], queryFn: getAsns })
  const { data: invoices = [] } = useQuery({ queryKey: ['invoices'], queryFn: getInvoices })
  const { data: audit = [] } = useQuery({ queryKey: ['po-audit', id], queryFn: () => getPoAudit(id) })
  const [notice, setNotice] = useState('')
  const [showIssue, setShowIssue] = useState(false)
  const inval = () => { void qc.invalidateQueries({ queryKey: ['po', id] }); void qc.invalidateQueries({ queryKey: ['pos'] }) }
  const issue = useMutation({ mutationFn: () => issuePo(id), onSuccess: () => { inval(); setShowIssue(false) }, onError: (e: Error) => { setShowIssue(false); setNotice(e.message) } })
  const ack = useMutation({ mutationFn: () => acknowledgePo(id), onSuccess: inval, onError: (e: Error) => setNotice(e.message) })

  if (isPending || !po) return <Spinner label="Loading PO…" />

  const poAsns = asns.filter((a) => a.poCode === po.code)
  const poInvoices = invoices.filter((i) => i.poCode === po.code)
  const inTransit = poAsns.find((a) => a.status === 'InTransit')

  // 3-way match readout
  const lines = po.lines ?? []
  const totQty = lines.reduce((s, l) => s + (l.qty ?? 0), 0)
  const recvQty = lines.reduce((s, l) => s + (l.receivedQty ?? 0), 0)
  const invQty = lines.reduce((s, l) => s + (l.invoicedQty ?? 0), 0)
  // Compare PO value against the invoice subtotal (pre-tax), matching the prototype's poInvAmt.
  const invAmt = poInvoices.reduce((s, i) => s + (i.subtotal ?? 0), 0)
  const hasInv = poInvoices.length > 0
  const valueOk = hasInv && poInvoices.every((i) => i.matchStatus === 'Matched')

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Purchase Orders</a> <Icon name="chev" size={13} /> <span>{po.code}</span></div>
      <div className="pagehead">
        <div>
          <h1>{po.code}</h1>
          <p>{po.vendorName}{po.rfqCode ? ` · from ${po.rfqCode}` : ''} · {po.incoterm}</p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          {!isVendor && po.status === 'Draft' && (
            <button type="button" className="btn btn-pri" onClick={() => setShowIssue(true)}><Icon name="send" size={15} /> Issue PO</button>
          )}
          {!isVendor && inTransit && (
            <button type="button" className="btn btn-pri" onClick={() => onNavigate(`deliveries/receive/${inTransit.id}`)}><Icon name="box" size={15} /> Receive shipment</button>
          )}
          {isVendor && po.status === 'Issued' && (
            <button type="button" className="btn btn-pri" onClick={() => ack.mutate()}><Icon name="check" size={15} /> Acknowledge PO</button>
          )}
          {isVendor && (po.status === 'Acknowledged' || po.status === 'PartiallyReceived') && (
            <button type="button" className="btn btn-out" onClick={() => onNavigate(`deliveries/new/${id}`)}><Icon name="send" size={15} /> Create shipping notice</button>
          )}
          {isVendor && (po.lines ?? []).some((l) => (l.receivedQty ?? 0) > (l.invoicedQty ?? 0)) && (
            <button type="button" className="btn btn-out" onClick={() => onNavigate(`invoices/new/${id}`)}><Icon name="doc" size={15} /> Submit invoice</button>
          )}
        </div>
      </div>

      <div className="ribbon">
        <span className={`badge ${STATUS_TONE[po.status ?? ''] ?? 'b-grey'}`}>{label(po.status)}</span>
        {po.acknowledged && <span className="badge b-green" style={{ marginLeft: 6 }}><Icon name="check" size={11} /> Vendor acknowledged</span>}
      </div>
      {notice && <Notice tone="error">{notice}</Notice>}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>PO Lines</h3><div className="spacer" /><span className="hint">Total RM {fmt(po.total)}</span></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Qty</th><th>UoM</th><th className="amt">Unit price</th><th className="amt">Line total</th><th className="amt">Recv</th><th className="amt">Inv</th></tr></thead>
          <tbody>
            {lines.map((l, i) => (
              <tr key={i}>
                <td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.qty}</td><td>{l.uom}</td>
                <td className="amt">RM {fmt(l.unitPrice)}</td><td className="amt">RM {fmt(l.lineTotal)}</td>
                <td className="amt">{l.receivedQty}</td><td className="amt">{l.invoicedQty}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Deliveries against this PO */}
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>Deliveries</h3><div className="spacer" /><span className="hint">{poAsns.length} shipping notice(s)</span></div>
        <table>
          <thead><tr><th>ASN</th><th>Carrier</th><th>Expected</th><th>Status</th><th>Goods receipt</th></tr></thead>
          <tbody>
            {poAsns.map((a) => (
              <tr key={a.id} className="rowlink" onClick={() => a.id && onNavigate(`deliveries/asn/${a.id}`)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{a.code}</td><td>{a.carrier || '—'}</td><td>{fmtDay(a.expectedDate)}</td>
                <td><span className={`badge ${a.status === 'Received' ? 'b-green' : a.status === 'InTransit' ? 'b-blue' : 'b-grey'}`}>{a.status === 'InTransit' ? 'In transit' : a.status}</span></td>
                <td>{a.grnCode ? <span className="badge b-green">{a.grnCode}</span> : <span className="hint">—</span>}</td>
              </tr>
            ))}
            {poAsns.length === 0 && <tr><td colSpan={5}><EmptyState>No deliveries yet.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>

      {/* Invoices against this PO */}
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>Invoices</h3><div className="spacer" /><span className="hint">{poInvoices.length} invoice(s)</span></div>
        <table>
          <thead><tr><th>Invoice</th><th>Supplier ref</th><th className="amt">Total</th><th>Match</th><th>Status</th></tr></thead>
          <tbody>
            {poInvoices.map((i) => (
              <tr key={i.id} className="rowlink" onClick={() => i.id && onNavigate(`invoices/${i.id}`)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{i.code}</td><td>{i.invoiceNo}</td>
                <td className="amt">RM {fmt(i.total)}</td>
                <td><span className={`badge ${i.matchStatus === 'Matched' ? 'b-green' : 'b-red'}`}>{i.matchStatus}</span></td>
                <td><span className={`badge ${i.status === 'Approved' || i.status === 'Paid' ? 'b-green' : i.status === 'Exception' ? 'b-red' : 'b-blue'}`}>{i.status}</span></td>
              </tr>
            ))}
            {poInvoices.length === 0 && <tr><td colSpan={5}><EmptyState>No invoices yet.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>

      {/* 3-way match */}
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>3-way match</h3><div className="spacer" /><span className="hint">PO vs goods receipt vs invoice</span></div>
        <div className="grid g3" style={{ padding: '14px 16px 4px' }}>
          <div className="card stat"><div className="lbl">Purchase order</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(po.total)}</div><div className="sub">{totQty} {lines.length > 1 ? 'items' : lines[0]?.uom ?? 'units'}</div></div>
          <div className="card stat"><div className="lbl">Goods receipt</div><div className="num" style={{ fontSize: 18 }}>{recvQty}/{totQty}</div><div className="sub">{recvQty >= totQty ? 'fully received' : recvQty > 0 ? 'partial' : 'awaiting'}</div></div>
          <div className="card stat"><div className="lbl">Supplier invoice</div><div className="num" style={{ fontSize: 18 }}>{hasInv ? `RM ${fmt(invAmt)}` : '—'}</div><div className="sub">{hasInv ? 'received' : 'not received'}</div></div>
        </div>
        <div className="cbody">
          <MatchRow label="Quantity · PO vs goods receipt" value={`${totQty} vs ${recvQty}`} state={recvQty === totQty} />
          <MatchRow label="Quantity · goods receipt vs invoice" value={hasInv ? `${recvQty} vs ${invQty}` : '—'} state={hasInv ? recvQty === invQty : null} />
          <MatchRow label="Value · PO vs invoice" value={hasInv ? `RM ${fmt(po.total)} vs RM ${fmt(invAmt)}` : '—'} state={hasInv ? valueOk : null} />
          {hasInv && !valueOk && (
            <div style={{ background: '#fbeded', color: '#9c3a30', borderRadius: 10, padding: '10px 12px', marginTop: 10, fontSize: 12.5 }}>
              <Icon name="flag" size={15} /> Invoice variance — blocked from payment, routed for exception approval.
            </div>
          )}
        </div>
      </div>

      {audit.length > 0 && (
        <div className="card" style={{ marginBottom: 16 }}>
          <div className="chead"><h3>Activity</h3><div className="spacer" /><span className="hint">audit trail</span></div>
          <div className="cbody">
            {[...audit].reverse().map((a, i) => (
              <div className="file" key={i} style={{ justifyContent: 'flex-start', gap: 10 }}>
                <span className="cav" style={{ width: 26, height: 26, fontSize: 10 }}>{(a.actorName ?? '?').split(/\s+/).slice(0, 2).map((w) => w[0]?.toUpperCase()).join('')}</span>
                <div>
                  <div style={{ fontWeight: 600, fontSize: 12.5 }}>{a.action}</div>
                  <div className="hint">{a.actorName} · {(a.utcTimestamp ?? '').slice(0, 16).replace('T', ' ')}{a.after ? ` · ${a.after}` : ''}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <CustomFieldsSection recordType="PurchaseOrder" recordId={id} />
      {showIssue && (
        <ConfirmModal
          icon="send" title={`Issue ${po.code}?`}
          body={<p className="hint" style={{ marginTop: 0 }}>This issues the PO and notifies <strong>{po.vendorName}</strong>. The vendor will be asked to acknowledge.</p>}
          cancelLabel="Not yet" confirmLabel="Issue now" confirmIcon="send" busy={issue.isPending}
          onCancel={() => setShowIssue(false)} onConfirm={() => issue.mutate()}
        />
      )}
    </>
  )
}

function MatchRow({ label, value, state }: { label: string; value: string; state: boolean | null }) {
  const badge = state == null
    ? <span className="badge b-grey">pending</span>
    : state ? <span className="badge b-green">match</span> : <span className="badge b-red">variance</span>
  return (
    <div className="mrow">
      <span>{label}</span>
      <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}><span style={{ fontWeight: 600 }}>{value}</span>{badge}</span>
    </div>
  )
}

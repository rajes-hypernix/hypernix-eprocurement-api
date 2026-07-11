import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getAsns, getAsn, getGrnForAsn, receiveAsn, getShipPlan, createAsn, getPos,
} from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, EmptyState, Notice, Spinner } from '../ui'
import { useIdentity } from '../../identity'
import { fmtDay, todayIso } from '../../lib/format'

const ASN_TONE: Record<string, string> = { Draft: 'b-grey', InTransit: 'b-blue', Received: 'b-green' }
const asnLabel = (s: string | null | undefined) => (s === 'InTransit' ? 'In transit' : s ?? '')

export function DeliveryPage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  const parts = route.split('/')
  if (parts[1] === 'receive') return <ReceiveForm asnId={parts[2]} onBack={() => onNavigate('deliveries')} />
  if (parts[1] === 'new') return <AsnForm poId={parts[2]} onBack={() => onNavigate('deliveries')} />
  if (parts[1] === 'asn') return <AsnDetail asnId={parts[2]} onBack={() => onNavigate('deliveries')} />
  return <DeliveryList onNavigate={onNavigate} />
}

function DeliveryList({ onNavigate }: { onNavigate: (key: string) => void }) {
  const { isVendor } = useIdentity()
  const { data: asns = [] } = useQuery({ queryKey: ['asns'], queryFn: getAsns })
  const { data: pos = [] } = useQuery({ queryKey: ['pos'], queryFn: getPos, enabled: isVendor })
  const shippable = pos.filter((p) => p.status === 'Acknowledged' || p.status === 'PartiallyReceived')

  return (
    <>
      <div className="pagehead"><div><h1>Deliveries</h1><p>Advance shipping notices and goods receipts. The buyer receives against each ASN on delivery.</p></div></div>

      {isVendor && (
        <div className="card" style={{ marginBottom: 16 }}>
          <div className="chead"><h3>Ready to ship</h3><div className="spacer" /><span className="hint">acknowledged POs</span></div>
          <table>
            <thead><tr><th>PO</th><th className="amt">Value</th><th>Status</th><th /></tr></thead>
            <tbody>
              {shippable.map((p) => (
                <tr key={p.id}>
                  <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{p.code}</td>
                  <td className="amt">RM {Number(p.total ?? 0).toLocaleString()}</td>
                  <td><span className="badge b-blue">{p.status === 'PartiallyReceived' ? 'Partially received' : p.status}</span></td>
                  <td className="amt"><div className="rowactions"><button type="button" className="btn btn-pri btn-sm" onClick={() => p.id && onNavigate(`deliveries/new/${p.id}`)}><Icon name="send" size={13} /> New shipping notice</button></div></td>
                </tr>
              ))}
              {shippable.length === 0 && <tr><td colSpan={4}><EmptyState>No acknowledged POs ready to ship.</EmptyState></td></tr>}
            </tbody>
          </table>
        </div>
      )}

      <div className="card">
        <div className="chead"><h3>Shipping notices</h3></div>
        <table>
          <thead><tr><th>ASN</th><th>PO</th><th>Carrier</th><th>Expected</th><th>Status</th><th /></tr></thead>
          <tbody>
            {asns.map((a) => (
              <tr key={a.id} className="rowlink" onClick={() => a.id && onNavigate(`deliveries/asn/${a.id}`)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{a.code}</td>
                <td>{a.poCode}</td>
                <td>{a.carrier || '—'}</td>
                <td>{fmtDay(a.expectedDate)}</td>
                <td><span className={`badge ${ASN_TONE[a.status ?? ''] ?? 'b-grey'}`}>{asnLabel(a.status)}</span></td>
                <td className="amt">
                  <div className="rowactions">
                    {!isVendor && a.status === 'InTransit' ? (
                      <button type="button" className="btn btn-pri btn-sm" onClick={(e) => { e.stopPropagation(); if (a.id) onNavigate(`deliveries/receive/${a.id}`) }}><Icon name="box" size={13} /> Receive</button>
                    ) : (
                      <span className="btn btn-ghost btn-sm">View <Icon name="chev" size={13} /></span>
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {asns.length === 0 && <tr><td colSpan={6}><EmptyState>No shipping notices yet.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>
    </>
  )
}

function AsnForm({ poId, onBack }: { poId: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { data: plan, isPending } = useQuery({ queryKey: ['ship-plan', poId], queryFn: () => getShipPlan(poId) })
  const [qty, setQty] = useState<Record<string, number>>({})
  const [lots, setLots] = useState<Record<string, string>>({})
  const [carrier, setCarrier] = useState('')
  const [notice, setNotice] = useState('')
  const [confirm, setConfirm] = useState(false)
  const [done, setDone] = useState<string | null>(null)   // post-submit confirmation (ASN code)

  const submit = useMutation({
    mutationFn: () => createAsn(poId, {
      carrier, trackingNo: '', shippedDate: todayIso(), expectedDate: null,
      lines: (plan?.lines ?? []).map((l) => ({ itemCode: l.itemCode!, shippedQty: qty[l.itemCode!] ?? l.remaining ?? 0, lotNo: lots[l.itemCode!] ?? '' })),
    }),
    onSuccess: (asn) => { void qc.invalidateQueries({ queryKey: ['asns'] }); void qc.invalidateQueries({ queryKey: ['pos'] }); setConfirm(false); setDone(asn.code ?? '') },
    onError: (e: Error) => { setConfirm(false); setNotice(e.message) },
  })

  if (done !== null)
    return (
      <div className="card" style={{ maxWidth: 560, margin: '40px auto' }}>
        <div className="cbody" style={{ textAlign: 'center' }}>
          <div className="health health-ok" style={{ marginBottom: 12 }}><span className="dot" /> Shipping notice submitted</div>
          <p>ASN <strong>{done}</strong> is now in transit against {plan?.poCode}. The buyer will receive against it on delivery.</p>
          <button type="button" className="btn btn-pri" onClick={onBack}>Back to deliveries</button>
        </div>
      </div>
    )

  if (isPending || !plan) return <Spinner />
  const anyQty = (plan.lines ?? []).some((l) => (qty[l.itemCode!] ?? l.remaining ?? 0) > 0)

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Deliveries</a> <Icon name="chev" size={13} /> <span>New ASN · {plan.poCode}</span></div>
      <div className="pagehead">
        <div><h1>Advance shipping notice</h1><p>Notify the buyer of an inbound shipment against {plan.poCode}.</p></div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" disabled={!plan.anyRemaining} onClick={() => (anyQty ? setConfirm(true) : setNotice('Enter a shipment quantity on at least one line'))}><Icon name="send" size={15} /> Submit ASN</button>
      </div>
      {!plan.anyRemaining && <Notice tone="warn" icon="lock">Nothing left to ship — all lines are already in transit or received.</Notice>}
      {notice && <Notice tone="error">{notice}</Notice>}
      {confirm && (
        <ConfirmModal
          icon="send" title="Submit shipping notice?"
          body={<p className="hint" style={{ marginTop: 0 }}>This notifies the buyer that goods are in transit against <strong>{plan.poCode}</strong>. The buyer will receive against this ASN on delivery.</p>}
          cancelLabel="Keep editing" confirmLabel="Submit ASN" confirmIcon="send" busy={submit.isPending}
          onCancel={() => setConfirm(false)} onConfirm={() => submit.mutate()}
        />
      )}
      <div className="card" style={{ marginBottom: 16 }}><div className="cbody"><div className="field" style={{ maxWidth: 360 }}><label>Carrier</label><input type="text" value={carrier} placeholder="e.g. Tiong Nam Logistics" onChange={(e) => setCarrier(e.target.value)} /></div></div></div>
      <div className="card">
        <div className="chead"><h3>Shipment lines</h3><div className="spacer" /><span className="hint">quantity is capped to remaining</span></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Ordered</th><th className="amt">Recv</th><th className="amt">In transit</th><th className="amt">Remaining</th><th className="amt">This shipment</th><th>Lot</th></tr></thead>
          <tbody>
            {plan.lines!.map((l) => (
              <tr key={l.itemCode}>
                <td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.ordered}</td><td className="amt">{l.received}</td><td className="amt">{l.inTransit}</td><td className="amt">{l.remaining}</td>
                <td className="amt" style={{ width: 110 }}><input type="number" min={0} max={l.remaining ?? undefined} value={qty[l.itemCode!] ?? l.remaining ?? 0} onChange={(e) => setQty({ ...qty, [l.itemCode!]: Number(e.target.value) })} aria-label={`Ship ${l.itemCode}`} /></td>
                <td style={{ width: 130 }}><input type="text" value={lots[l.itemCode!] ?? ''} placeholder="Lot" onChange={(e) => setLots({ ...lots, [l.itemCode!]: e.target.value })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function ReceiveForm({ asnId, onBack }: { asnId: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { data: asn, isPending } = useQuery({ queryKey: ['asn', asnId], queryFn: () => getAsn(asnId) })
  const [qty, setQty] = useState<Record<string, number>>({})
  const [notice, setNotice] = useState('')
  const [confirm, setConfirm] = useState(false)

  const post = useMutation({
    mutationFn: () => receiveAsn(asnId, { lines: (asn?.lines ?? []).map((l) => ({ itemCode: l.itemCode!, receivedQty: qty[l.itemCode!] ?? l.shippedQty ?? 0, condition: 'Good' })) }),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['asns'] }); void qc.invalidateQueries({ queryKey: ['pos'] }); setConfirm(false); onBack() },
    onError: (e: Error) => { setConfirm(false); setNotice(e.message) },
  })

  if (isPending || !asn) return <Spinner />

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Deliveries</a> <Icon name="chev" size={13} /> <span>Receive {asn.code}</span></div>
      <div className="pagehead">
        <div><h1>Goods receipt — {asn.code}</h1><p>{asn.poCode} · {asn.vendorName} · carrier {asn.carrier || '—'}</p></div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" onClick={() => setConfirm(true)}><Icon name="box" size={15} /> Post receipt</button>
      </div>
      {notice && <Notice tone="error">{notice}</Notice>}
      {confirm && (
        <ConfirmModal
          icon="box" title={`Post goods receipt for ${asn.code}?`}
          body={<p className="hint" style={{ marginTop: 0 }}>This records the received quantities as a goods receipt and updates <strong>{asn.poCode}</strong> and its 3-way match.</p>}
          cancelLabel="Not yet" confirmLabel="Post receipt" confirmIcon="box" busy={post.isPending}
          onCancel={() => setConfirm(false)} onConfirm={() => post.mutate()}
        />
      )}
      <div className="card">
        <div className="chead"><h3>Receive lines</h3><div className="spacer" /><span className="hint">received is capped to shipped &amp; outstanding; under-receipt = Short</span></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Shipped</th><th className="amt">Receive now</th></tr></thead>
          <tbody>
            {asn.lines!.map((l) => (
              <tr key={l.itemCode}>
                <td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.shippedQty}</td>
                <td className="amt" style={{ width: 110 }}><input type="number" min={0} max={l.shippedQty ?? undefined} value={qty[l.itemCode!] ?? l.shippedQty ?? 0} onChange={(e) => setQty({ ...qty, [l.itemCode!]: Number(e.target.value) })} aria-label={`Receive ${l.itemCode}`} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function AsnDetail({ asnId, onBack }: { asnId: string; onBack: () => void }) {
  const { data: asn, isPending } = useQuery({ queryKey: ['asn', asnId], queryFn: () => getAsn(asnId) })
  const { data: grn } = useQuery({ queryKey: ['grn', asnId], queryFn: () => getGrnForAsn(asnId) })
  if (isPending || !asn) return <Spinner />
  return (
    <>
      <div className="crumb"><a onClick={onBack}>Deliveries</a> <Icon name="chev" size={13} /> <span>{asn.code}</span></div>
      <div className="pagehead"><div><h1>{asn.code}</h1><p>{asn.poCode} · {asn.vendorName} · expected {fmtDay(asn.expectedDate)}</p></div></div>
      <div className="ribbon">
        <span className={`badge ${ASN_TONE[asn.status ?? ''] ?? 'b-grey'}`}>{asnLabel(asn.status)}</span>
        {asn.grnCode && <span className="badge b-green" style={{ marginLeft: 6 }}><Icon name="check" size={11} /> Received · {asn.grnCode}</span>}
      </div>
      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>Shipment lines</h3><div className="spacer" /><span className="hint">tracking {asn.trackingNo || '—'}</span></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Shipped</th><th>Lot</th></tr></thead>
          <tbody>{asn.lines!.map((l) => <tr key={l.itemCode}><td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.shippedQty} {l.uom}</td><td>{l.lotNo || '—'}</td></tr>)}</tbody>
        </table>
      </div>
      {grn && (
        <div className="card">
          <div className="chead"><h3>Goods receipt · {grn.code}</h3><div className="spacer" /><span className="hint">{grn.nsId}</span></div>
          <table>
            <thead><tr><th>Code</th><th className="amt">Expected</th><th className="amt">Received</th><th>Condition</th></tr></thead>
            <tbody>{grn.lines!.map((l) => <tr key={l.itemCode}><td>{l.itemCode}</td><td className="amt">{l.expectedQty}</td><td className="amt">{l.receivedQty}</td><td><span className={`badge ${l.condition === 'Short' ? 'b-amber' : 'b-green'}`}>{l.condition}</span></td></tr>)}</tbody>
          </table>
        </div>
      )}
    </>
  )
}

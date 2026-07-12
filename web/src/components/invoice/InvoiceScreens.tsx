import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getInvoice, getBillablePlan, submitInvoice, approveInvoice, resolveInvoice,
} from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, EmptyState, Notice, Spinner } from '../ui'
import { fmt, fmtDay, todayIso } from '../../lib/format'
import { useIdentity } from '../../identity'
import { useViewRows } from '../views/useViewRows'
import { CustomFieldsSection } from '../customfields/CustomFieldsSection'
import { SegmentsSection } from '../segments/SegmentsSection'

const INV_TONE: Record<string, string> = { Draft: 'b-grey', Submitted: 'b-blue', Approved: 'b-green', Exception: 'b-red', Paid: 'b-grey' }

export function InvoicePage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  const parts = route.split('/')
  if (parts[1] === 'new') return <InvoiceForm poId={parts[2]} onBack={() => onNavigate('invoices')} />
  // 'invoices/view/{id}' (D7.5): open the list WITH that saved view picked.
  if (parts[1] === 'view') return <InvoiceList onOpen={(id) => onNavigate(`invoices/${id}`)} initialViewId={parts[2]} />
  if (parts.length >= 2) return <InvoiceDetail id={parts[1]} onBack={() => onNavigate('invoices')} />
  return <InvoiceList onOpen={(id) => onNavigate(`invoices/${id}`)} />
}

// D7.5 rollout mount (the RfqList recipe): rows from the picked view's paged run — the
// seeded "All Invoices" system view by default (reproduces getInvoices exactly).
const rowToInvoice = (row: Record<string, unknown>) => ({
  id: (row.Id as string | undefined) ?? undefined,
  code: (row.Code as string | undefined) ?? null,
  invoiceNo: (row.InvoiceNo as string | undefined) ?? null,
  poCode: (row.PoCode as string | undefined) ?? null,
  total: (row.Total as number | undefined) ?? 0,
  matchStatus: (row.MatchStatus as string | undefined) ?? null,
  status: (row.Status as string | undefined) ?? null,
})

function InvoiceList({ onOpen, initialViewId }: { onOpen: (id: string) => void; initialViewId?: string }) {
  const { isVendor } = useIdentity()
  const { run, picker, builderModal, pager } = useViewRows('Invoice', initialViewId)
  const invoices = (run?.rows ?? []).map(rowToInvoice)
  return (
    <>
      <div className="pagehead">
        <div><h1>Invoices</h1><p>Supplier billing and line-level 3-way matching — PO vs goods receipt vs invoice.</p></div>
        <div className="spacer" />
        {picker}
      </div>
      <div className="card">
        <table>
          <thead><tr><th>Invoice</th><th>Supplier ref</th>{!isVendor && <th>PO</th>}<th className="amt">Total</th><th>Match</th><th>Status</th><th /></tr></thead>
          <tbody>
            {invoices.map((i) => (
              <tr key={i.id} className="rowlink" onClick={() => i.id && onOpen(i.id)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{i.code}</td>
                <td>{i.invoiceNo}</td>
                {!isVendor && <td>{i.poCode}</td>}
                <td className="amt">RM {fmt(i.total)}</td>
                <td><span className={`badge ${i.matchStatus === 'Matched' ? 'b-green' : 'b-red'}`}>{i.matchStatus}</span></td>
                <td><span className={`badge ${INV_TONE[i.status ?? ''] ?? 'b-grey'}`}>{i.status}</span></td>
                <td className="amt"><div className="rowactions"><span className="btn btn-ghost btn-sm">Open <Icon name="chev" size={13} /></span></div></td>
              </tr>
            ))}
            {invoices.length === 0 && <tr><td colSpan={isVendor ? 6 : 7}><EmptyState>No invoices.</EmptyState></td></tr>}
          </tbody>
        </table>
        {pager}
        {builderModal}
      </div>
    </>
  )
}

function InvoiceDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { isVendor } = useIdentity()
  const { data: inv, isPending } = useQuery({ queryKey: ['invoice', id], queryFn: () => getInvoice(id) })
  const [notice, setNotice] = useState('')
  const [modal, setModal] = useState<'approve' | 'resolve' | null>(null)
  const inval = () => { void qc.invalidateQueries({ queryKey: ['invoice', id] }); void qc.invalidateQueries({ queryKey: ['invoices'] }); void qc.invalidateQueries({ queryKey: ['pos'] }) }
  const approve = useMutation({ mutationFn: () => approveInvoice(id), onSuccess: () => { inval(); setModal(null) }, onError: (e: Error) => { setModal(null); setNotice(e.message) } })
  const resolve = useMutation({ mutationFn: () => resolveInvoice(id), onSuccess: () => { inval(); setModal(null) }, onError: (e: Error) => { setModal(null); setNotice(e.message) } })

  if (isPending || !inv) return <Spinner label="Loading invoice…" />

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Invoices</a> <Icon name="chev" size={13} /> <span>{inv.code}</span></div>
      <div className="pagehead">
        <div><h1>{inv.code}</h1><p>{inv.poCode} · supplier ref {inv.invoiceNo} · {fmtDay(inv.date)}</p></div>
        <div className="spacer" />
        {!isVendor && (
          <div className="actbar">
            {inv.status === 'Exception' && <button type="button" className="btn btn-pri" onClick={() => setModal('resolve')}><Icon name="check" size={15} /> Resolve exception</button>}
            {inv.status === 'Submitted' && <button type="button" className="btn btn-pri" onClick={() => setModal('approve')}><Icon name="check" size={15} /> Approve for payment</button>}
          </div>
        )}
      </div>

      {modal === 'approve' && (
        <ConfirmModal
          title={`Approve ${inv.code} for payment?`}
          body={<p className="hint" style={{ marginTop: 0 }}>This approves the vendor bill (RM {fmt(inv.total)}) and releases the invoice into the payment run.</p>}
          cancelLabel="Not yet" confirmLabel="Approve" confirmIcon="check" busy={approve.isPending}
          onCancel={() => setModal(null)} onConfirm={() => approve.mutate()}
        />
      )}
      {modal === 'resolve' && (
        <ConfirmModal
          title={`Resolve & approve ${inv.code}?`}
          body="Confirm the variance is accepted (e.g. approved price change or credit agreed). This approves the invoice and posts a Vendor Bill."
          confirmLabel="Resolve & approve" confirmIcon="check" busy={resolve.isPending}
          onCancel={() => setModal(null)} onConfirm={() => resolve.mutate()}
        />
      )}
      <div className="ribbon">
        <span className={`badge ${INV_TONE[inv.status ?? ''] ?? 'b-grey'}`}>{inv.status}</span>
        <span className="hint" style={{ marginLeft: 8 }}>
          {inv.status === 'Exception' ? (inv.exceptionReason ?? 'Variance under review.') :
            inv.status === 'Approved' || inv.status === 'Paid' ? `Approved for payment${inv.nsId ? ' · ' + inv.nsId : ''}.` : 'Submitted for 3-way matching.'}
        </span>
      </div>
      {notice && <Notice tone="error">{notice}</Notice>}

      <div className="card">
        <div className="chead"><h3>Invoice lines · 3-way match</h3></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Inv qty</th><th className="amt">Recv qty</th><th className="amt">Inv price</th><th className="amt">PO price</th><th className="amt">Line amt</th><th>Match</th></tr></thead>
          <tbody>
            {(inv.lines ?? []).map((l, i) => (
              <tr key={i}>
                <td>{l.itemCode}</td><td>{l.description}</td>
                <td className="amt">{l.qty}</td><td className="amt">{l.receivedQty}</td>
                <td className="amt">RM {fmt(l.unitPrice)}</td><td className="amt">RM {fmt(l.poUnitPrice)}</td>
                <td className="amt">RM {fmt(l.lineTotal)}</td>
                <td><span className={`badge ${l.qtyOk && l.priceOk ? 'b-green' : 'b-red'}`}>{l.qtyOk && l.priceOk ? 'Match' : 'Variance'}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="cbody">
          <div className="mrow"><span>Subtotal</span><span style={{ fontWeight: 600 }}>RM {fmt(inv.subtotal)}</span></div>
          <div className="mrow"><span>SST (8%)</span><span style={{ fontWeight: 600 }}>RM {fmt(inv.sst)}</span></div>
          {(inv.wht ?? 0) > 0 && <div className="mrow"><span>Withholding tax ({inv.whtRate}%)</span><span style={{ fontWeight: 600 }}>− RM {fmt(inv.wht)}</span></div>}
      <CustomFieldsSection recordType="Invoice" recordId={id} />
      <SegmentsSection recordType="Invoice" recordId={id} />
          <div className="mrow" style={{ borderBottom: 'none' }}><span style={{ fontWeight: 700 }}>Total payable</span><span style={{ fontWeight: 800, color: 'var(--teal)' }}>RM {fmt(inv.total)}</span></div>
        </div>
      </div>
    </>
  )
}

function InvoiceForm({ poId, onBack }: { poId: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { data: plan, isPending } = useQuery({ queryKey: ['billable', poId], queryFn: () => getBillablePlan(poId) })
  const [qty, setQty] = useState<Record<string, number>>({})
  const [price, setPrice] = useState<Record<string, number>>({})
  const [invoiceNo, setInvoiceNo] = useState('')
  const [wht, setWht] = useState(0)
  const [notice, setNotice] = useState('')
  const [confirm, setConfirm] = useState(false)
  const [done, setDone] = useState<string | null>(null)   // post-submit confirmation (invoice code)

  const submit = useMutation({
    mutationFn: () => submitInvoice(poId, {
      invoiceNo, date: todayIso(), whtRate: wht,
      lines: (plan?.lines ?? []).filter((l) => (qty[l.itemCode!] ?? l.billable ?? 0) > 0)
        .map((l) => ({ itemCode: l.itemCode!, qty: qty[l.itemCode!] ?? l.billable ?? 0, unitPrice: price[l.itemCode!] ?? l.poUnitPrice ?? 0 })),
    }),
    onSuccess: (inv) => { void qc.invalidateQueries({ queryKey: ['invoices'] }); setConfirm(false); setDone(inv.code ?? '') },
    onError: (e: Error) => { setConfirm(false); setNotice(e.message) },
  })

  if (done !== null)
    return (
      <div className="card" style={{ maxWidth: 560, margin: '40px auto' }}>
        <div className="cbody" style={{ textAlign: 'center' }}>
          <div className="health health-ok" style={{ marginBottom: 12 }}><span className="dot" /> Invoice submitted</div>
          <p>Invoice <strong>{done}</strong> against {plan?.poCode} is queued for automatic 3-way matching against the PO and goods receipt.</p>
          <button type="button" className="btn btn-pri" onClick={onBack}>Back to invoices</button>
        </div>
      </div>
    )

  if (isPending || !plan) return <Spinner />
  const anyBillable = (plan.lines ?? []).some((l) => (l.billable ?? 0) > 0)
  const anyQty = (plan.lines ?? []).some((l) => (qty[l.itemCode!] ?? l.billable ?? 0) > 0)

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Invoices</a> <Icon name="chev" size={13} /> <span>New invoice · {plan.poCode}</span></div>
      <div className="pagehead">
        <div><h1>Submit invoice</h1><p>Bill against {plan.poCode}. Quantity is capped to received − already invoiced.</p></div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" disabled={!anyBillable} onClick={() => (anyQty ? setConfirm(true) : setNotice('Enter an invoice quantity on at least one line'))}><Icon name="send" size={15} /> Submit invoice</button>
      </div>
      {!anyBillable && <Notice tone="warn" icon="lock">Nothing billable yet — goods must be received first.</Notice>}
      {notice && <Notice tone="error">{notice}</Notice>}
      {confirm && (
        <ConfirmModal
          icon="send" title="Submit invoice?"
          body={<p className="hint" style={{ marginTop: 0 }}>This submits your invoice against <strong>{plan.poCode}</strong> for automatic 3-way matching against the PO and goods receipt.</p>}
          cancelLabel="Keep editing" confirmLabel="Submit" confirmIcon="send" busy={submit.isPending}
          onCancel={() => setConfirm(false)} onConfirm={() => submit.mutate()}
        />
      )}
      <div className="card" style={{ marginBottom: 16 }}><div className="cbody"><div className="grid g2">
        <div className="field"><label>Your invoice no.</label><input type="text" value={invoiceNo} onChange={(e) => setInvoiceNo(e.target.value)} /></div>
        <div className="field"><label>Withholding tax (%)</label><input type="number" min={0} value={wht || ''} onChange={(e) => setWht(Number(e.target.value))} /></div>
      </div></div></div>
      <div className="card">
        <div className="chead"><h3>Invoice lines</h3><div className="spacer" /><span className="hint">price variance &gt; 2% will flag an exception</span></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Recv</th><th className="amt">Already inv</th><th className="amt">Billable</th><th className="amt">Inv qty</th><th className="amt">Unit price</th></tr></thead>
          <tbody>
            {plan.lines!.map((l) => (
              <tr key={l.itemCode}>
                <td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.receivedQty}</td><td className="amt">{l.alreadyInvoiced}</td><td className="amt">{l.billable}</td>
                <td className="amt" style={{ width: 100 }}><input type="number" min={0} max={l.billable ?? undefined} value={qty[l.itemCode!] ?? l.billable ?? 0} onChange={(e) => setQty({ ...qty, [l.itemCode!]: Number(e.target.value) })} aria-label={`Qty ${l.itemCode}`} /></td>
                <td className="amt" style={{ width: 120 }}><input type="number" min={0} value={price[l.itemCode!] ?? l.poUnitPrice ?? 0} onChange={(e) => setPrice({ ...price, [l.itemCode!]: Number(e.target.value) })} aria-label={`Price ${l.itemCode}`} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

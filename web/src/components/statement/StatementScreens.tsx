import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getStatements, getStatement, getMyStatement, getInvoices, type StatementDetailDto } from '../../api/client'
import { Icon } from '../Icon'
import { EmptyState, Spinner } from '../ui'
import { fmt } from '../../lib/format'
import { useIdentity } from '../../identity'

export function StatementPage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  const { isVendor } = useIdentity()
  if (isVendor) return <VendorStatement />
  if (route.startsWith('statements/')) return <StatementDetail vendorId={route.slice('statements/'.length)} onBack={() => onNavigate('statements')} />
  return <StatementList onOpen={(id) => onNavigate(`statements/${id}`)} />
}

function StatementList({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: rows = [] } = useQuery({ queryKey: ['statements'], queryFn: getStatements })
  const totBal = rows.reduce((a, r) => a + (r.balance ?? 0), 0)
  const totGrni = rows.reduce((a, r) => a + (r.grni ?? 0), 0)
  const withBal = rows.filter((r) => (r.balance ?? 0) > 0).length

  return (
    <>
      <div className="pagehead"><div><h1>Statements of Account</h1><p>Per-vendor ledger across POs, goods receipts, invoices and payments — with open balance and GRNI accruals.</p></div></div>
      <div className="grid g3" style={{ marginBottom: 16 }}>
        <div className="card stat"><div className="lbl">Total payable</div><div className="num" style={{ fontSize: 20 }}>RM {fmt(totBal)}</div><div className="sub">open balance</div></div>
        <div className="card stat"><div className="lbl">GRNI accrual</div><div className="num" style={{ fontSize: 20 }}>RM {fmt(totGrni)}</div><div className="sub">received, not invoiced</div></div>
        <div className="card stat"><div className="lbl">Vendors with balance</div><div className="num">{withBal}</div><div className="sub">outstanding</div></div>
      </div>
      <div className="card">
        <table>
          <thead><tr><th>Vendor</th><th className="amt">Invoiced</th><th className="amt">Paid</th><th className="amt">Open balance</th><th className="amt">GRNI</th><th /></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.vendorId} className="rowlink" onClick={() => r.vendorId && onOpen(r.vendorId)}>
                <td style={{ fontWeight: 700 }}>{r.vendorName}</td>
                <td className="amt">RM {fmt(r.invoiced)}</td>
                <td className="amt">RM {fmt(r.paid)}</td>
                <td className="amt" style={{ fontWeight: 700, color: (r.balance ?? 0) > 0 ? 'var(--teal)' : 'inherit' }}>RM {fmt(r.balance)}</td>
                <td className="amt">{(r.grni ?? 0) > 0 ? `RM ${fmt(r.grni)}` : '—'}</td>
                <td className="amt"><div className="rowactions"><span className="btn btn-ghost btn-sm">Statement <Icon name="chev" size={13} /></span></div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function Aging({ s }: { s: StatementDetailDto }) {
  const a = s.aging
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <div className="chead"><h3>Aged payables</h3><div className="spacer" /><span className="hint">unpaid approved invoices</span></div>
      <div className="grid g4" style={{ padding: '14px 16px' }}>
        {([['Current (≤30d)', a?.current], ['31–60 days', a?.d30], ['61–90 days', a?.d60], ['90+ days', a?.d90]] as const).map(([label, v]) => (
          <div className="card stat" key={label}><div className="lbl">{label}</div><div className="num" style={{ fontSize: 17 }}>RM {fmt(v)}</div></div>
        ))}
      </div>
    </div>
  )
}

function LedgerCard({ s }: { s: StatementDetailDto }) {
  return (
    <div className="card">
      <div className="chead"><h3>Ledger</h3><div className="spacer" /><span className="hint">running balance</span></div>
      <table>
        <thead><tr><th>Date</th><th>Reference</th><th>Type</th><th className="amt">Debit (paid)</th><th className="amt">Credit (billed)</th><th className="amt">Balance</th></tr></thead>
        <tbody>
          {(s.ledger ?? []).map((e, i) => (
            <tr key={i}>
              <td>{e.date || '—'}</td>
              <td style={{ fontWeight: 600, color: 'var(--teal)' }}>{e.reference}</td>
              <td>{e.type}{e.memo != null ? <span className="hint"> (RM {fmt(e.memo)})</span> : ''}</td>
              <td className="amt">{(e.debit ?? 0) > 0 ? `RM ${fmt(e.debit)}` : ''}</td>
              <td className="amt">{(e.credit ?? 0) > 0 ? `RM ${fmt(e.credit)}` : ''}</td>
              <td className="amt" style={{ fontWeight: 600 }}>RM {fmt(e.balance)}</td>
            </tr>
          ))}
          {(s.ledger ?? []).length === 0 && <tr><td colSpan={6}><EmptyState>No ledger activity.</EmptyState></td></tr>}
        </tbody>
      </table>
    </div>
  )
}

function Summary({ s }: { s: StatementDetailDto }) {
  return (
    <div className="grid g4" style={{ marginBottom: 16 }}>
      <div className="card stat"><div className="lbl">Invoiced</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(s.invoiced)}</div></div>
      <div className="card stat"><div className="lbl">Paid</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(s.paid)}</div></div>
      <div className="card stat"><div className="lbl">Open balance</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(s.balance)}</div></div>
      <div className="card stat"><div className="lbl">GRNI accrual</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(s.grni)}</div></div>
    </div>
  )
}

function StatementDetail({ vendorId, onBack }: { vendorId: string; onBack: () => void }) {
  const { data: s, isPending } = useQuery({ queryKey: ['statement', vendorId], queryFn: () => getStatement(vendorId) })
  const [notice, setNotice] = useState('')
  if (isPending || !s) return <Spinner label="Loading statement…" />
  return (
    <>
      <div className="crumb"><a onClick={onBack}>Statements</a> <Icon name="chev" size={13} /> <span>{s.vendorName}</span></div>
      <div className="pagehead">
        <div><h1>{s.vendorName}</h1><p>Statement of account · derived from POs, goods receipts and invoices.</p></div>
        <div className="spacer" />
        <div className="actbar">
          <button type="button" className="btn btn-out" onClick={onBack}><Icon name="back" size={15} /> Back</button>
          <button type="button" className="btn btn-out" onClick={() => setNotice('Statement export queued (PDF / Excel).')}><Icon name="download" size={15} /> Export</button>
        </div>
      </div>
      {notice && <div className="ribbon" style={{ marginBottom: 14 }}>{notice}</div>}
      <Summary s={s} /><Aging s={s} /><LedgerCard s={s} />
    </>
  )
}

function VendorStatement() {
  const { data: s, isPending } = useQuery({ queryKey: ['my-statement'], queryFn: getMyStatement })
  const { data: invoices = [] } = useQuery({ queryKey: ['invoices'], queryFn: getInvoices })
  const [uploaded, setUploaded] = useState(false)
  if (isPending) return <Spinner />
  if (!s) return <><div className="pagehead"><div><h1>Statement</h1><p>Your account with Hypernix / SPSB.</p></div></div><div className="card"><div className="cbody"><p className="muted">No activity yet.</p></div></div></>
  return (
    <>
      <div className="pagehead"><div><h1>Statement of Account</h1><p>Your running ledger, open balance and goods-received-not-invoiced accrual.</p></div></div>
      <Summary s={s} /><Aging s={s} /><LedgerCard s={s} />
      <div style={{ marginTop: 16 }}>
        {uploaded
          ? <SoaReconciliation invoices={invoices} onReupload={() => setUploaded(false)} />
          : (
            <div className="card"><div className="cbody" style={{ textAlign: 'center', padding: 38 }}>
              <div style={{ marginBottom: 10 }}><Icon name="upload" size={30} /></div>
              <div style={{ fontWeight: 700, marginBottom: 4 }}>Reconcile your statement</div>
              <p className="hint" style={{ maxWidth: 430, margin: '0 auto 16px' }}>Upload your own statement of account and we'll reconcile it against our records — invoices and payments — and flag any differences.</p>
              <button type="button" className="btn btn-pri" onClick={() => setUploaded(true)}><Icon name="upload" size={15} /> Upload SOA (PDF / Excel)</button>
            </div></div>
          )}
      </div>
    </>
  )
}

// Mirrors venSOARecon: compares the vendor's posted invoices ("our records") against the
// uploaded SOA ("your SOA"), demonstrating a flagged variance + an unmatched item.
function SoaReconciliation({ invoices, onReupload }: { invoices: { code?: string | null; invoiceNo?: string | null; total?: number }[]; onReupload: () => void }) {
  const posted = invoices.filter((i) => (i.total ?? 0) > 0)
  const rows = posted.map((inv, ix) => {
    const sys = inv.total ?? 0
    const soa = ix === 0 ? sys + 1080 : sys      // demo: one disputed line
    return { ref: inv.code ?? inv.invoiceNo ?? '—', soa, sys, variance: soa - sys, status: soa === sys ? 'Matched' : 'Variance' }
  })
  const notInSystem = { ref: 'SOA-INV-9907', soa: 3200, sys: null as number | null, variance: 3200, status: 'Not in system' }
  const soaTotal = rows.reduce((a, r) => a + r.soa, 0) + notInSystem.soa
  const sysTotal = rows.reduce((a, r) => a + r.sys, 0)
  const diff = soaTotal - sysTotal
  return (
    <>
      <div className="grid g3" style={{ marginBottom: 16 }}>
        <div className="card stat"><div className="lbl">Per your SOA</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(soaTotal)}</div></div>
        <div className="card stat"><div className="lbl">Per our records</div><div className="num" style={{ fontSize: 18 }}>RM {fmt(sysTotal)}</div></div>
        <div className="card stat"><div className="lbl">Difference</div><div className="num" style={{ fontSize: 18, color: diff ? 'var(--red)' : 'var(--green)' }}>RM {fmt(diff)}</div></div>
      </div>
      <div className="card">
        <div className="chead"><h3>SOA reconciliation</h3><div className="spacer" /><span className="badge b-green"><Icon name="check" size={11} /> Auto-matched</span> <button type="button" className="btn btn-out btn-sm" style={{ marginLeft: 8 }} onClick={onReupload}>Re-upload</button></div>
        <table>
          <thead><tr><th>Reference</th><th className="amt">Per SOA</th><th className="amt">Per records</th><th className="amt">Variance</th><th>Status</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.ref}>
                <td style={{ fontWeight: 600 }}>{r.ref}</td>
                <td className="amt">RM {fmt(r.soa)}</td>
                <td className="amt">RM {fmt(r.sys)}</td>
                <td className="amt">{r.variance !== 0 ? <span style={{ color: 'var(--red)' }}>RM {fmt(r.variance)}</span> : '—'}</td>
                <td><span className={`badge ${r.status === 'Matched' ? 'b-green' : 'b-amber'}`}>{r.status}</span></td>
              </tr>
            ))}
            <tr>
              <td style={{ fontWeight: 600 }}>{notInSystem.ref}</td>
              <td className="amt">RM {fmt(notInSystem.soa)}</td>
              <td className="amt">—</td>
              <td className="amt"><span style={{ color: 'var(--red)' }}>RM {fmt(notInSystem.variance)}</span></td>
              <td><span className="badge b-red">Not in system</span></td>
            </tr>
          </tbody>
        </table>
        <div className="cbody"><div style={{ background: '#fbeded', color: '#9c3a30', padding: '10px 12px', fontSize: 12.5 }}><Icon name="flag" size={14} /> Variances and unmatched items are flagged to the buyer's AP team for resolution before payment.</div></div>
      </div>
    </>
  )
}

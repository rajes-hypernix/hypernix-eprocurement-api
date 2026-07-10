import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRfqs, getAwards, getAwardEligibility, getAwardForRfq, submitAward, approveAward,
  type AwardDto, type AwardLineDto,
} from '../../api/client'
import { Icon } from '../Icon'
import { EmptyState, Modal, Notice, Spinner } from '../ui'
import { fmt } from '../../lib/format'
import { AnswerCell } from '../../lib/answers'
import { useIdentity } from '../../identity'

export function AwardsPage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  if (route.startsWith('awards/'))
    return <AwardWorkspace rfqId={route.slice('awards/'.length)} onBack={() => onNavigate('awards')} />
  return <AwardsList onOpen={(id) => onNavigate(`awards/${id}`)} />
}

function AwardsList({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: rfqs = [] } = useQuery({ queryKey: ['rfqs'], queryFn: getRfqs })
  const { data: awards = [] } = useQuery({ queryKey: ['awards'], queryFn: getAwards })
  const ready = rfqs.filter((r) => r.status === 'Evaluation')

  return (
    <>
      <div className="pagehead"><div><h1>Awards &amp; Purchase Orders</h1><p>Evaluate, allocate and award RFQs. Approval (DoA) generates one PO per vendor.</p></div></div>

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>Ready to evaluate / award</h3></div>
        <table>
          <thead><tr><th>RFQ</th><th>Title</th><th>Envelope</th><th>Status</th><th /></tr></thead>
          <tbody>
            {ready.map((r) => (
              <tr key={r.id} className="rowlink" onClick={() => r.id && onOpen(r.id)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{r.code}</td>
                <td>{r.title}</td>
                <td>{r.envelope}</td>
                <td><span className="badge b-blue">{r.status}</span></td>
                <td className="amt"><div className="rowactions"><span className="btn btn-ghost btn-sm">Award <Icon name="chev" size={13} /></span></div></td>
              </tr>
            ))}
            {ready.length === 0 && <tr><td colSpan={5}><EmptyState>No RFQs under evaluation.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card">
        <div className="chead"><h3>Award decisions</h3></div>
        <table>
          <thead><tr><th>Award</th><th>RFQ</th><th>Status</th><th>Approver</th><th className="amt">Value</th><th>PO(s)</th></tr></thead>
          <tbody>
            {awards.map((a) => (
              <tr key={a.id} className="rowlink" onClick={() => a.rfqId && onOpen(a.rfqId)}>
                <td style={{ fontWeight: 700 }}>{a.code}</td>
                <td style={{ color: 'var(--teal)' }}>{a.rfqCode}</td>
                <td><span className={`badge ${a.status === 'Approved' ? 'b-green' : 'b-amber'}`}>{a.status}</span></td>
                <td>{a.approverUserId ?? '—'}</td>
                <td className="amt">RM {fmt(a.totalValue)}</td>
                <td>{(a.poCodes ?? []).map((p) => <span key={p} className="swchip">{p}</span>)}</td>
              </tr>
            ))}
            {awards.length === 0 && <tr><td colSpan={6}><EmptyState>No awards yet.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>
    </>
  )
}

function AwardWorkspace({ rfqId, onBack }: { rfqId: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { code: actingCode, roles } = useIdentity()
  const { data: elig, isPending } = useQuery({ queryKey: ['award-eligibility', rfqId], queryFn: () => getAwardEligibility(rfqId) })
  const { data: award } = useQuery({ queryKey: ['award', rfqId], queryFn: () => getAwardForRfq(rfqId) })
  // Allocation is keyed per cell ("<lineCode>::<vendorId>" → qty) so one line's quantity
  // can be split across vendors (partial to A, balance to B).
  const [alloc, setAlloc] = useState<Record<string, number> | null>(null)
  const [notice, setNotice] = useState('')
  const [approved, setApproved] = useState<AwardDto | null>(null)
  const cellKey = (lineCode: string, vendorId: string) => `${lineCode}::${vendorId}`

  const init = useMemo(() => {
    const m: Record<string, number> = {}
    for (const l of elig?.lines ?? []) {
      if (l.recommendedVendorId) m[`${l.lineCode}::${l.recommendedVendorId}`] = l.requiredQty ?? 0
    }
    return m
  }, [elig])

  const inval = () => {
    void qc.invalidateQueries({ queryKey: ['award', rfqId] })
    void qc.invalidateQueries({ queryKey: ['awards'] })
    void qc.invalidateQueries({ queryKey: ['rfqs'] })
  }
  const submit = useMutation({
    mutationFn: () => {
      const a = alloc ?? init
      const allocations = Object.entries(a)
        .filter(([, qty]) => qty > 0)
        .map(([key, qty]) => { const [lineCode, vendorId] = key.split('::'); return { lineCode, vendorId, qty } })
      return submitAward(rfqId, { allocations })
    },
    onSuccess: () => { inval(); setNotice('Submitted for approval — a different authorised user (Approver) must approve.') },
    onError: (e: Error) => setNotice(e.message),
  })
  const approve = useMutation({
    mutationFn: (id: string) => approveAward(id),
    onSuccess: (result) => { inval(); setApproved(result) },
    onError: (e: Error) => setNotice(e.message),
  })

  if (isPending || !elig) return <Spinner />
  const A = alloc ?? init

  const header = (
    <>
      <div className="crumb"><a onClick={onBack}>Awards &amp; POs</a> <Icon name="chev" size={13} /> <span>{elig.code}</span></div>
      <div className="pagehead"><div><h1>Award — {elig.code}</h1><p>{elig.title} · {elig.envelope} envelope</p></div></div>
    </>
  )

  // Approved → outcome
  if (award && award.status === 'Approved') {
    return (
      <>
        {header}
        <Notice tone="success" icon="check">{award.code} approved by {award.approverUserId} (≠ creator {award.createdByUserId}). {(award.poCodes ?? []).length} PO(s) generated.</Notice>
        <div className="card">
          <div className="chead"><h3>Award outcome</h3><div className="spacer" /><span className="badge b-green">Approved</span></div>
          <table>
            <thead><tr><th>Line</th><th>Vendor</th><th className="amt">Qty</th><th className="amt">Unit price</th><th className="amt">Value</th></tr></thead>
            <tbody>
              {(award.allocations ?? []).map((al, i) => (
                <tr key={i}><td>{al.lineCode}</td><td>{al.vendorName}</td><td className="amt">{al.qty}</td><td className="amt">RM {fmt(al.unitPrice)}</td><td className="amt">RM {fmt((al.qty ?? 0) * (al.unitPrice ?? 0))}</td></tr>
              ))}
            </tbody>
          </table>
          <div className="cbody" style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700, borderTop: '2px solid #333' }}>
            <span>Total award value · PO {(award.poCodes ?? []).join(', ')}</span><span>RM {fmt(award.totalValue)}</span>
          </div>
        </div>
      </>
    )
  }

  if (!elig.commercialRevealed)
    return (
      <>
        {header}
        <Notice tone="warn" icon="lock">Commercial envelope is sealed. Finalize technical scoring and open the commercial envelope (Bid Openings) before awarding.</Notice>
      </>
    )

  const pending = award && award.status === 'PendingApproval'
  const canApprove = !!pending && roles.includes('Approver') && actingCode !== award?.createdByUserId

  return (
    <>
      {header}
      {notice && <Notice>{notice}</Notice>}

      {pending && (
        <Notice tone="warn" icon="lock">
          {award!.code} is pending approval (created by {award!.createdByUserId}). A different user with the Approver role must approve.
          <div style={{ flex: 1 }} />
          <button type="button" className="btn btn-pri btn-sm" disabled={!canApprove} onClick={() => approve.mutate(award!.id!)}>
            <Icon name="check" size={14} /> Approve &amp; generate PO
          </button>
        </Notice>
      )}

      {(() => {
        const cols = (elig.ranking ?? [])   // eligible vendors as columns (masked aliases)
        const optFor = (lineCode: string, vendorId: string) => (elig.lines ?? []).find((l) => l.lineCode === lineCode)?.options?.find((o) => o.vendorId === vendorId)
        const lineAllocated = (lineCode: string) => cols.reduce((s, c) => s + (A[cellKey(lineCode!, c.vendorId!)] ?? 0), 0)
        const setQty = (key: string, qty: number) => setAlloc({ ...A, [key]: Math.max(0, qty) })
        const toggleCell = (l: AwardLineDto, vendorId: string, on: boolean) => {
          const key = cellKey(l.lineCode!, vendorId)
          if (!on) { const next = { ...A }; delete next[key]; setAlloc(next); return }
          const offered = optFor(l.lineCode!, vendorId)?.offeredQty ?? l.requiredQty ?? 0
          const remaining = (l.requiredQty ?? 0) - lineAllocated(l.lineCode!)
          setAlloc({ ...A, [key]: Math.max(1, Math.min(offered, remaining > 0 ? remaining : (l.requiredQty ?? 0))) })
        }
        return (
          <div className="card" style={{ marginBottom: 16 }}>
            <div className="chead"><h3>Award comparison</h3><div className="spacer" /><span className="badge b-teal">Eligible vendors only · split allowed</span></div>
            <div className="cbody" style={{ paddingBottom: 0 }}><p className="hint" style={{ marginTop: 0 }}>Recommended (lowest) per line is highlighted. Tick the vendor(s) to award each line — split a line's quantity across vendors by ticking more than one.</p></div>
            <div className="comp-wrap">
              <table className="comp">
                <thead>
                  <tr>
                    <th style={{ minWidth: 220 }}>Line item</th>
                    {cols.map((c) => <th key={c.vendorId} className="vcol" style={{ textAlign: 'center' }}>{c.vendorName}{c.recommended && <div className="hint" style={{ fontWeight: 500 }}>recommended</div>}</th>)}
                    <th className="amt">Allocated</th>
                  </tr>
                </thead>
                <tbody>
                  {(elig.lines ?? []).map((l) => {
                    const alloced = lineAllocated(l.lineCode!)
                    const over = alloced > (l.requiredQty ?? 0)
                    return (
                      <tr key={l.lineCode}>
                        <td><div style={{ fontWeight: 600 }}>{l.description}</div><div className="hint">{l.lineCode} · need {l.requiredQty} {l.uom}</div></td>
                        {cols.map((c) => {
                          const opt = optFor(l.lineCode!, c.vendorId!)
                          const key = cellKey(l.lineCode!, c.vendorId!)
                          const qty = A[key] ?? 0
                          const isRec = l.recommendedVendorId === c.vendorId
                          if (!opt) return <td key={c.vendorId} className="vcol" style={{ textAlign: 'center' }}><span className="hint">No bid</span></td>
                          return (
                            <td key={c.vendorId} className="vcol" style={{ textAlign: 'center', background: isRec ? 'var(--green-bg)' : undefined }}>
                              <label className="ck" style={{ justifyContent: 'center', gap: 6 }}>
                                <input type="checkbox" style={{ width: 'auto' }} checked={qty > 0} disabled={!!pending} onChange={(e) => toggleCell(l, c.vendorId!, e.target.checked)} aria-label={`Award ${l.lineCode} to ${c.vendorName}`} />
                                <span style={{ fontWeight: 700 }}>RM {fmt(opt.unitPrice)}</span>
                              </label>
                              {qty > 0 && <input type="number" min={0} max={Math.min(opt.offeredQty ?? 0, l.requiredQty ?? 0)} value={qty} disabled={!!pending} style={{ width: 76, textAlign: 'right', marginTop: 4 }} onChange={(e) => setQty(key, Number(e.target.value))} aria-label={`Qty ${l.lineCode} ${c.vendorName}`} />}
                            </td>
                          )
                        })}
                        <td className="amt" style={{ fontWeight: 700, color: over ? 'var(--red)' : alloced === (l.requiredQty ?? 0) ? '#2f9e6e' : 'var(--muted)' }}>{alloced}/{l.requiredQty}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )
      })()}

      {(() => {
        // Per-vendor totals from the cell allocation (Award Summary).
        const byV = new Map<string, { name: string; total: number }>()
        for (const [key, qty] of Object.entries(A)) {
          if (!qty) continue
          const [lineCode, vendorId] = key.split('::')
          const line = (elig.lines ?? []).find((x) => x.lineCode === lineCode)
          const o = (line?.options ?? []).find((x) => x.vendorId === vendorId)
          if (!o) continue
          const cur = byV.get(vendorId) ?? { name: o.vendorName ?? vendorId, total: 0 }
          cur.total += (o.unitPrice ?? 0) * qty
          byV.set(vendorId, cur)
        }
        const rows = [...byV.values()]
        const grand = rows.reduce((s, r) => s + r.total, 0)
        return (
          <div className="card" style={{ marginBottom: 16 }}>
            <div className="chead"><h3>Award Summary</h3></div>
            <div className="cbody">
              {rows.map((r) => (
                <div className="file" key={r.name} style={{ justifyContent: 'space-between' }}>
                  <div style={{ fontWeight: 600 }}>{r.name}</div>
                  <div style={{ fontWeight: 700, color: 'var(--teal)' }}>RM {fmt(r.total)}</div>
                </div>
              ))}
              {rows.length === 0 && <p className="hint" style={{ margin: 0 }}>No lines allocated yet.</p>}
              <div style={{ display: 'flex', justifyContent: 'space-between', paddingTop: 12, marginTop: 6, borderTop: '2px solid #333', fontWeight: 700, fontSize: 15 }}>
                <span>Total award value</span><span>RM {fmt(grand)}</span>
              </div>
              <p className="hint" style={{ marginBottom: 0 }}>
                {rows.length} purchase order{rows.length !== 1 ? 's' : ''} will be created (one per awarded vendor) once the award is approved.
              </p>
            </div>
          </div>
        )
      })()}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>Combined evaluation</h3><div className="spacer" /><span className="hint">{elig.envelope === 'Dual' ? 'technical 70% · commercial 30%' : 'lowest price'}</span></div>
        <table>
          <thead><tr><th style={{ width: 50 }}>Rank</th><th>Vendor</th>{elig.envelope === 'Dual' && <th className="amt">Technical</th>}<th className="amt">Price score</th><th className="amt">Combined</th></tr></thead>
          <tbody>
            {(elig.ranking ?? []).map((r, i) => (
              <tr key={r.vendorId}>
                <td style={{ textAlign: 'center', fontWeight: 700 }}>{i + 1}</td>
                <td style={{ fontWeight: 600 }}>{r.vendorName}{r.recommended && <span className="badge b-green" style={{ marginLeft: 8 }}>Recommended</span>}</td>
                {elig.envelope === 'Dual' && <td className="amt">{r.technicalScore ?? '—'}</td>}
                <td className="amt">{r.priceScore}</td>
                <td className="amt" style={{ fontWeight: 800, color: 'var(--teal)' }}>{r.combined}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ResponsesCard title="Technical Responses" questions={elig.technicalQuestions ?? []} responses={elig.responses ?? []} />
      <ResponsesCard title="Commercial Responses" questions={elig.commercialQuestions ?? []} responses={elig.responses ?? []} />

      {!pending && (
        <div className="actbar">
          <div style={{ flex: 1 }} />
          <button type="button" className="btn btn-pri" onClick={() => submit.mutate()}>
            <Icon name="award" size={15} /> Submit for approval
          </button>
        </div>
      )}

      {approved && (
        <Modal
          title="Award Confirmed"
          footer={<button type="button" className="btn btn-pri" onClick={() => { setApproved(null); onBack() }}>View awards</button>}
        >
          <div className="locked-note" style={{ background: 'var(--green-bg)', color: '#256b41', marginBottom: 14 }}>
            <Icon name="check" size={16} /> {(approved.poCodes ?? []).length} purchase order(s) generated.
          </div>
          {(approved.poCodes ?? []).map((po) => (
            <div className="file" key={po} style={{ justifyContent: 'space-between' }}>
              <div style={{ fontWeight: 700 }}>{po}</div>
              <span className="badge b-green"><Icon name="check" size={11} /> Issued</span>
            </div>
          ))}
        </Modal>
      )}
    </>
  )
}

function ResponsesCard({ title, questions, responses }: {
  title: string
  questions: { order?: number; label?: string | null; type?: string | null; config?: string | null }[]
  responses: { vendorId?: string; vendorName?: string | null; answers?: { questionOrder?: number; value?: string | null }[] | null }[]
}) {
  if (questions.length === 0 || responses.length === 0) return null
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <div className="chead"><h3>{title}</h3><div className="spacer" /><span className="hint">progressed vendors</span></div>
      <div style={{ overflowX: 'auto' }}>
        <table>
          <thead>
            <tr>
              <th style={{ minWidth: 230 }}>Question</th>
              {responses.map((r) => <th key={r.vendorId} style={{ whiteSpace: 'normal' }}>{r.vendorName}</th>)}
            </tr>
          </thead>
          <tbody>
            {questions.map((q) => (
              <tr key={q.order}>
                <td style={{ fontWeight: 600, whiteSpace: 'normal', maxWidth: 260 }}>{q.label || 'Untitled'}</td>
                {responses.map((r) => {
                  const a = (r.answers ?? []).find((x) => x.questionOrder === q.order)?.value
                  return <td key={r.vendorId} style={{ whiteSpace: 'normal' }}><AnswerCell type={q.type} value={a} config={q.config} /></td>
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

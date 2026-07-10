import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRfqs, getOpening, openTechnical, openCommercial,
  getTechnicalEval, setScore, finalizeTechnical, getUsers,
} from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, EmptyState, Notice, Spinner } from '../ui'
import { AnswerCell } from '../../lib/answers'

export function EvalPage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  const parts = route.split('/')
  if (parts.length >= 3 && parts[2] === 'score')
    return <TechnicalEval rfqId={parts[1]} onBack={() => onNavigate(`openings/${parts[1]}`)} onNavigate={onNavigate} />
  if (parts.length >= 2)
    return <BidOpening rfqId={parts[1]} onBack={() => onNavigate('openings')} onScore={(id) => onNavigate(`openings/${id}/score`)} onNavigate={onNavigate} />
  return <BidOpenings onOpen={(id) => onNavigate(`openings/${id}`)} />
}

function BidOpenings({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: rfqs = [] } = useQuery({ queryKey: ['rfqs'], queryFn: getRfqs })
  const rows = rfqs.filter((r) => ['Closed', 'Evaluation', 'Awarded'].includes(r.status ?? ''))
  return (
    <>
      <div className="pagehead">
        <div><h1>Bid Openings</h1><p>Closed RFQs ready to open and evaluate. Technical opens and scores before commercial unseals.</p></div>
      </div>
      <div className="card">
        <table>
          <thead><tr><th>RFQ</th><th>Title</th><th>Envelope</th><th className="amt">Bids</th><th>Status</th><th /></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id} className="rowlink" onClick={() => r.id && onOpen(r.id)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{r.code}</td>
                <td>{r.title}</td>
                <td>{r.envelope === 'Dual' ? <span className="badge b-blue"><Icon name="lock" size={12} /> Dual</span> : <span className="badge b-grey">Single</span>}</td>
                <td className="amt">{r.invitedCount}</td>
                <td><span className="badge b-blue">{r.status}</span></td>
                <td className="amt"><div className="rowactions"><span className="btn btn-ghost btn-sm">Open <Icon name="chev" size={13} /></span></div></td>
              </tr>
            ))}
            {rows.length === 0 && <tr><td colSpan={6}><EmptyState>No RFQs ready to open.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>
    </>
  )
}

function BidOpening({ rfqId, onBack, onScore, onNavigate }: { rfqId: string; onBack: () => void; onScore: (id: string) => void; onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const { data: o, isPending } = useQuery({ queryKey: ['opening', rfqId], queryFn: () => getOpening(rfqId) })
  const { data: users = [] } = useQuery({ queryKey: ['users'], queryFn: getUsers })
  const [notice, setNotice] = useState('')
  const [authorise, setAuthorise] = useState(false)
  const evNames = (codes: string[]) => codes.map((c) => users.find((u) => u.code === c)?.name ?? c).join(', ')
  const inval = () => { void qc.invalidateQueries({ queryKey: ['opening', rfqId] }); void qc.invalidateQueries({ queryKey: ['rfqs'] }) }
  // Opening technical unseals then drops straight into scoring (prototype doOpenTech).
  const openT = useMutation({ mutationFn: () => openTechnical(rfqId), onSuccess: () => { inval(); setAuthorise(false); onScore(rfqId) }, onError: (e: Error) => { setAuthorise(false); setNotice(e.message) } })
  const openC = useMutation({ mutationFn: () => openCommercial(rfqId), onSuccess: inval, onError: (e: Error) => setNotice(e.message) })

  if (isPending || !o) return <Spinner />
  const single = o.envelope === 'Single'

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Bid Openings</a> <Icon name="chev" size={13} /> <span>{o.code}</span></div>
      <div className="pagehead"><div><h1>Open Sealed Bids — {o.code}</h1><p>{single ? 'Single envelope — technical and commercial open together.' : 'Dual envelope. Technical must be opened and scored before commercial unseals.'}</p></div></div>
      {notice && <Notice tone="error">{notice}</Notice>}
      <div className="grid g2">
        <div className="env tech">
          <div className="etop">
            <span className="seal" style={{ color: '#5b56b0' }}><Icon name="lock" size={22} /></span>
            <div><div style={{ fontWeight: 700, fontSize: 15 }}>Technical envelope</div><div className="hint">{o.submittedCount} sealed submission{o.submittedCount !== 1 ? 's' : ''}</div></div>
          </div>
          <div className="ebody">
            <div className="locked-note" style={{ marginBottom: 12 }}><Icon name="users" size={16} /> Restricted to assigned evaluators: <strong style={{ color: 'var(--ink)' }}>{evNames(o.evaluators ?? []) || '—'}</strong></div>
            {o.technicalOpened ? (
              <button type="button" className="btn btn-pri" onClick={() => onScore(rfqId)}>
                <Icon name="edit" size={15} /> {o.techFinalized ? 'View technical scoring' : 'Continue technical scoring'}
              </button>
            ) : (
              <button type="button" className="btn btn-pri" disabled={!o.canOpenTechnical} onClick={() => setAuthorise(true)}>
                <Icon name="unlock" size={15} /> Open technical envelope
              </button>
            )}
          </div>
        </div>
        <div className="env comm">
          <div className="etop">
            <span className="seal" style={{ color: '#2f7d56' }}><Icon name="lock" size={22} /></span>
            <div><div style={{ fontWeight: 700, fontSize: 15 }}>Commercial envelope</div><div className="hint">Pricing &amp; commercial terms</div></div>
          </div>
          <div className="ebody">
            <div className="locked-note" style={{ marginBottom: 12 }}>
              <Icon name="lock" size={16} /> {o.techFinalized || single ? 'Technical finalized — commercial can be opened.' : 'Sealed until technical scoring is finalized.'}
            </div>
            {o.commercialOpened ? (
              <button type="button" className="btn btn-pri" onClick={() => onNavigate(`awards/${rfqId}`)}>
                <Icon name="award" size={15} /> View commercial evaluation
              </button>
            ) : (
              <button type="button" className={`btn ${o.canOpenCommercial ? 'btn-pri' : 'btn-out'}`} disabled={!o.canOpenCommercial} onClick={() => openC.mutate()}>
                <Icon name="unlock" size={15} /> Open commercial envelope
              </button>
            )}
            {!o.commercialOpened && !o.canOpenCommercial && (o.techFinalized || single) && (
              <p className="hint" style={{ marginTop: 8 }}>A commercial evaluator must open this envelope.</p>
            )}
          </div>
        </div>
      </div>

      {authorise && (
        <ConfirmModal
          icon="lock" title="Authorise — Open Technical Envelope"
          body={
            <>
              <p className="hint" style={{ marginTop: 0 }}>This envelope is sealed. Only an assigned evaluator can open it. Confirm to unseal the technical envelope for scoring.</p>
              <div className="field"><label>Assigned evaluators</label><div style={{ fontWeight: 600 }}>{evNames(o.evaluators ?? []) || '—'}</div></div>
              <div className="locked-note"><Icon name="flag" size={15} /> Action is logged to the audit trail with your name &amp; timestamp.</div>
            </>
          }
          confirmLabel="Unseal technical" confirmIcon="unlock" busy={openT.isPending}
          onCancel={() => setAuthorise(false)} onConfirm={() => openT.mutate()}
        />
      )}
    </>
  )
}

function TechnicalEval({ rfqId, onBack, onNavigate }: { rfqId: string; onBack: () => void; onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const { data: ev, isPending } = useQuery({ queryKey: ['technical-eval', rfqId], queryFn: () => getTechnicalEval(rfqId) })
  const [evaluator, setEvaluator] = useState<string>('')
  const [notice, setNotice] = useState('')

  const score = useMutation({
    mutationFn: (v: { vendorId: string; criterion: string; value: number }) =>
      setScore(rfqId, { vendorId: v.vendorId, evaluatorId: evaluator || (ev?.evaluators?.[0]?.code ?? ''), criterion: v.criterion, score: v.value }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['technical-eval', rfqId] }),
    onError: (e: Error) => setNotice(e.message),
  })
  const finalize = useMutation({
    mutationFn: () => finalizeTechnical(rfqId),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['technical-eval', rfqId] }),
    onError: (e: Error) => setNotice(e.message),
  })

  if (isPending || !ev) return <Spinner />
  const pic = evaluator || ev.evaluators?.[0]?.code || ''
  const cellScore = (vendorId: string, criterion: string) =>
    ev.vendors?.find((v) => v.vendorId === vendorId)?.scores?.find((s) => s.evaluatorId === pic && s.criterion === criterion)?.score ?? ''

  // Weighted score for the currently-picked evaluator: Σ(score × weight)/100, only when complete.
  const weighted = (vendorId: string) => {
    let sum = 0
    for (const c of ev.criteria ?? []) {
      const s = cellScore(vendorId, c.key!)
      if (s === '' || s == null) return null
      sum += Number(s) * (c.weight ?? 0)
    }
    return Math.round((sum / 100) * 10) / 10
  }
  // An evaluator is "done" when every vendor is fully scored by them.
  const evaluatorDone = (code: string) =>
    (ev.vendors ?? []).length > 0 && (ev.vendors ?? []).every((v) =>
      (ev.criteria ?? []).every((c) => {
        const cell = v.scores?.find((s) => s.evaluatorId === code && s.criterion === c.key)?.score
        return cell != null
      }))
  const failedCount = (ev.vendors ?? []).filter((v) => v.pass === false).length

  return (
    <>
      <div className="crumb"><a onClick={onBack}>Open bids</a> <Icon name="chev" size={13} /> <span>{ev.code}</span></div>
      <div className="pagehead">
        <div><h1>Technical Evaluation — {ev.code}</h1><p>{ev.title} · sealed technical envelope</p></div>
        <div className="spacer" />
        {!ev.finalized && (
          <button type="button" className="btn btn-pri" onClick={() => finalize.mutate()}>
            <Icon name="check" size={15} /> Finalize technical
          </button>
        )}
        {ev.finalized && <span className="badge b-green">Technical finalized</span>}
      </div>

      {ev.masked && (
        <div className="ribbon" style={{ marginBottom: 14, background: '#ecebf6', borderColor: '#d9d6ef', color: '#4a45a0' }}>
          <Icon name="eye" size={14} /> Evaluator view — vendor identities are masked (Bidder A, B, C…) until award.
        </div>
      )}
      {notice && <Notice tone="error">{notice}</Notice>}

      {(ev.technicalQuestions ?? []).length > 0 && (ev.vendors ?? []).length > 0 && (
        <div className="card" style={{ marginBottom: 16 }}>
          <div className="chead"><h3>Technical Responses</h3><div className="spacer" /><span className="hint">read-only · for reference</span></div>
          <div style={{ overflowX: 'auto' }}>
            <table>
              <thead>
                <tr>
                  <th style={{ minWidth: 230 }}>Question</th>
                  {(ev.vendors ?? []).map((v) => <th key={v.vendorId} style={{ whiteSpace: 'normal' }}>{v.displayName}</th>)}
                </tr>
              </thead>
              <tbody>
                {(ev.technicalQuestions ?? []).map((q) => (
                  <tr key={q.order}>
                    <td style={{ fontWeight: 600, whiteSpace: 'normal', maxWidth: 260 }}>{q.label || 'Untitled'}</td>
                    {(ev.vendors ?? []).map((v) => {
                      const a = v.answers?.find((x) => x.questionOrder === q.order)?.value
                      return <td key={v.vendorId} style={{ whiteSpace: 'normal' }}><AnswerCell type={q.type} value={a} config={q.config} /></td>
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="card">
        <div className="chead">
          <h3>Scoring matrix</h3>
          <div className="spacer" />
          {!ev.finalized && (
            <>
              <span className="hint" style={{ marginRight: 8 }}>Scoring as</span>
              <select value={pic} onChange={(e) => setEvaluator(e.target.value)} style={{ width: 'auto' }}>
                {(ev.evaluators ?? []).map((x) => <option key={x.code} value={x.code!}>{x.name}{evaluatorDone(x.code!) ? ' ✓' : ''}</option>)}
              </select>
            </>
          )}
        </div>
        <table>
          <thead>
            <tr>
              <th style={{ minWidth: 220 }}>Evaluation criterion</th>
              {(ev.vendors ?? []).map((v) => <th key={v.vendorId} className="amt">{v.displayName}</th>)}
            </tr>
          </thead>
          <tbody>
            {(ev.criteria ?? []).map((c) => (
              <tr key={c.key}>
                <td><div style={{ fontWeight: 600 }}>{c.label}</div><div className="hint">Weight {c.weight}%</div></td>
                {(ev.vendors ?? []).map((v) => (
                  <td key={v.vendorId} className="amt">
                    {ev.finalized ? (
                      <strong>{cellScore(v.vendorId!, c.key!) || '—'}</strong>
                    ) : (
                      <input
                        type="number" min={0} max={100}
                        style={{ width: 70, textAlign: 'right' }}
                        defaultValue={cellScore(v.vendorId!, c.key!)}
                        aria-label={`${c.key} ${v.displayName}`}
                        onBlur={(e) => e.target.value !== '' && score.mutate({ vendorId: v.vendorId!, criterion: c.key!, value: Number(e.target.value) })}
                      />
                    )}
                  </td>
                ))}
              </tr>
            ))}
            {!ev.finalized && (
              <tr style={{ background: 'var(--soft)' }}>
                <td style={{ fontWeight: 700 }}>Your weighted score</td>
                {(ev.vendors ?? []).map((v) => {
                  const w = weighted(v.vendorId!)
                  return <td key={v.vendorId} className="amt" style={{ fontWeight: 700 }}>{w == null ? <span className="hint">incomplete</span> : w.toFixed(1)}</td>
                })}
              </tr>
            )}
            <tr style={{ background: 'var(--soft)' }}>
              <td style={{ fontWeight: 700 }}>Committee score <span className="hint">avg of {(ev.evaluators ?? []).length} evaluator(s)</span></td>
              {(ev.vendors ?? []).map((v) => (
                <td key={v.vendorId} className="amt" style={{ fontWeight: 800 }}>
                  <span style={{ color: v.committee == null ? 'var(--muted)' : (v.pass === false ? 'var(--red)' : '#2f9e6e') }}>
                    {v.committee ?? '—'}
                  </span>
                  {ev.finalized && v.pass != null && (
                    <div><span className={`badge ${v.pass ? 'b-green' : 'b-red'}`}>{v.pass ? 'Pass' : 'Fail'}</span></div>
                  )}
                </td>
              ))}
            </tr>
          </tbody>
        </table>
      </div>
      {ev.finalized ? (
        <div className="card" style={{ marginTop: 16 }}>
          <div className="cbody" style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <div style={{ flex: 1 }}>
              <strong>Technical evaluation finalized.</strong>{' '}
              <span className="hint">{failedCount} vendor(s) did not pass — excluded from commercial. A commercial evaluator opens the commercial envelope next.</span>
            </div>
            <button type="button" className="btn btn-pri" onClick={() => onNavigate(`openings/${rfqId}`)}>
              <Icon name="unlock" size={15} /> Commercial opening
            </button>
          </div>
        </div>
      ) : (
        <p className="hint" style={{ marginTop: 10 }}>
          Commercial (price) envelopes release only for vendors scoring ≥ {ev.threshold}. Pricing stays sealed until technical is finalized.
        </p>
      )}
    </>
  )
}

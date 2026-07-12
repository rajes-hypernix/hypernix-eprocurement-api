import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getOnboardingApplication, startOnboardingReview, clarifyOnboarding, approveOnboarding, rejectOnboarding,
  type ClarificationItemInput,
} from '../../api/client'
import { useSwec } from '../../api/swec'
import { Icon } from '../Icon'
import { Modal, Notice, Spinner } from '../ui'
import { CustomFieldsSection } from '../customfields/CustomFieldsSection'
import { SegmentsSection } from '../segments/SegmentsSection'

/**
 * Buyer review of one onboarding application (SPEC §4/§7/§8): profile, banking, financial band with a
 * "view calculation" drawer, questionnaire answers, documents, clarification history, and an action bar
 * (start review · request clarification · reject · approve → promote).
 */
export function OnboardingReview({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { data: app, isPending } = useQuery({ queryKey: ['onboarding-app', id], queryFn: () => getOnboardingApplication(id) })
  const { data: swec } = useSwec()

  const [calc, setCalc] = useState(false)
  const [clarMode, setClarMode] = useState(false)
  const [items, setItems] = useState<ClarificationItemInput[]>([])
  const [topic, setTopic] = useState('')
  const [request, setRequest] = useState('')
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [approved, setApproved] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)

  const refresh = () => qc.invalidateQueries({ queryKey: ['onboarding-app', id] })
  const onErr = (e: Error) => setErr(e.message)

  const start = useMutation({ mutationFn: () => startOnboardingReview(id), onSuccess: refresh, onError: onErr })
  const clarify = useMutation({
    mutationFn: () => clarifyOnboarding(id, 'Please address the items below.', items),
    onSuccess: () => { setClarMode(false); setItems([]); void refresh() }, onError: onErr,
  })
  const approve = useMutation({ mutationFn: () => approveOnboarding(id), onSuccess: (r) => setApproved(r.vendorCode), onError: onErr })
  const reject = useMutation({ mutationFn: () => rejectOnboarding(id, reason), onSuccess: () => { setRejecting(false); void refresh() }, onError: onErr })

  if (isPending || !app) return <Spinner />

  const addItem = () => { if (!topic.trim()) return; setItems((xs) => [...xs, { topic: topic.trim(), request: request.trim() || 'Please review and resubmit.' }]); setTopic(''); setRequest('') }
  const fin = app.financial
  const finColor = fin ? (fin.band === 'D' ? 'var(--red)' : fin.band === 'C' ? 'var(--amber)' : 'var(--teal)') : undefined
  const openRound = app.rounds.find((r) => r.status === 'Open')
  const canDecide = app.status === 'UnderReview'
  const canStart = app.status === 'Submitted' || app.status === 'Resubmitted'

  // One action row, rendered at the top AND bottom so the reviewer never has to scroll to act.
  const actions = (
    <div className="actionbar" style={{ marginTop: 14 }}>
      {canStart ? (
        <><span className="hint">Pick this application up to review it.</span><div className="spacer" style={{ flex: 1 }} />
          <button type="button" className="btn btn-pri" disabled={start.isPending} onClick={() => start.mutate()}>Start review</button></>
      ) : clarMode ? (
        <><span className="hint">{items.length} item{items.length === 1 ? '' : 's'} flagged</span><div className="spacer" style={{ flex: 1 }} />
          <button type="button" className="btn btn-out" onClick={() => { setClarMode(false); setItems([]) }}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={items.length === 0 || clarify.isPending} onClick={() => clarify.mutate()}><Icon name="flag" size={15} /> Send clarification ({items.length})</button></>
      ) : canDecide ? (
        <><span className="hint">Decide once all items check out.</span><div className="spacer" style={{ flex: 1 }} />
          <button type="button" className="btn btn-out" style={{ color: 'var(--red)' }} onClick={() => setRejecting(true)}><Icon name="x" size={15} /> Reject</button>
          <button type="button" className="btn btn-out" onClick={() => { setClarMode(true); setItems([]) }}><Icon name="flag" size={15} /> Request clarification</button>
          <button type="button" className="btn btn-pri" disabled={approve.isPending} onClick={() => approve.mutate()}><Icon name="check" size={15} /> Approve &amp; onboard</button></>
      ) : (
        <span className="hint">This application is {app.status} — no action available.</span>
      )}
    </div>
  )

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>Onboarding</button> <Icon name="chev" size={12} /> {app.name || app.code}
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            {app.name || app.code} <span className={`badge ${app.type === 'SWEC' ? 'b-teal' : 'b-amber'}`}>{app.type}</span>
          </h1>
          <p>{app.code} · {app.registrationNo || 'reg. pending'} · {app.location || '—'} · {app.source === 'SelfService' ? 'Vendor self-service' : 'Manual'}</p>
        </div>
      </div>

      {err && <Notice tone="error" icon="x">{err}</Notice>}
      {app.duplicateWarning && <Notice tone="warn" icon="flag">{app.duplicateWarning}</Notice>}
      {openRound && <Notice tone="warn" icon="flag">Clarification round {openRound.roundNo} is open ({openRound.items.length} item{openRound.items.length === 1 ? '' : 's'}) — awaiting the vendor’s resubmission.</Notice>}

      {actions}

      <div className="grid g2">
        <Card title="Company & contact">
          <KV k="Registered name" v={app.name || '—'} />
          <KV k="Reg. no. (SSM)" v={app.registrationNo || '—'} mono />
          <KV k="Location" v={app.location || '—'} />
          <KV k="Email" v={app.email || '—'} />
        </Card>
        <Card title="Banking">
          <KV k="Bank" v={app.bank.bank || '—'} />
          <KV k="Account no." v={app.bank.accountNo || '—'} mono />
          <KV k="SWIFT" v={app.bank.swift || '—'} mono />
          <KV k="SWEC categories" v={app.categories.map((c) => swec?.label(c) ?? c).join(', ') || '—'} />
        </Card>
      </div>

      {fin ? (
        <div className="card" style={{ marginTop: 14 }}>
          <div className="chead"><h3>Financial pre-qualification</h3><div className="spacer" style={{ flex: 1 }} />
            <button type="button" className="btn btn-out btn-sm" onClick={() => setCalc(true)}><Icon name="eye" size={13} /> View calculation</button>
          </div>
          <div className="cbody">
            <div className="grid g3">
              <Stat label="Altman Z · weighted" value={fin.weightedZ.toFixed(2)} sub={fin.zone} color={finColor} />
              <Stat label="Score" value={`${fin.score} / 100`} />
              <Stat label={`Band → Risk`} value={`Band ${fin.band}`} sub={`${fin.risk} risk`} color={finColor} />
            </div>
            <p style={{ margin: '14px 0 0', lineHeight: 1.55 }}>{fin.statement}</p>
          </div>
        </div>
      ) : (
        <Notice tone="success" icon="check" style={{ marginTop: 14 }}>SWEC-registered — financial pre-qualification waived (verified against the PETRONAS LLRC).</Notice>
      )}

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead"><h3>Questionnaire</h3><span className="sub">· {app.answers.length} answered</span></div>
        <div className="cbody">
          {app.answers.length === 0 ? <p className="hint" style={{ margin: 0 }}>No answers submitted.</p>
            : app.answers.map((a, i) => <KV key={i} k={a.label} v={a.value || '—'} />)}
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead"><h3>Documents</h3></div>
        <div className="cbody">
          {app.documents.length === 0 ? <p className="hint" style={{ margin: 0 }}>No documents uploaded.</p>
            : app.documents.map((d) => <div className="doc" key={d.key}><div className="di"><Icon name="doc" size={15} /></div><div className="dn">{d.fileName}</div><span className="filepill"><Icon name="check" size={12} /> attached</span></div>)}
        </div>
      </div>

      {app.rounds.length > 0 && (
        <div className="card" style={{ marginTop: 14 }}>
          <div className="chead"><h3>Clarification history</h3><span className="sub">· {app.rounds.length} round{app.rounds.length === 1 ? '' : 's'}</span></div>
          <div className="cbody">
            {app.rounds.map((r) => (
              <div key={r.roundNo} style={{ marginBottom: 12 }}>
                <div style={{ fontWeight: 700, fontSize: 12.5 }}>Round {r.roundNo} · {r.direction === 'BuyerToVendor' ? 'to vendor' : 'from vendor'} · <span className={`badge ${r.status === 'Responded' ? 'b-green' : 'b-amber'}`}>{r.status}</span></div>
                {r.items.map((it, i) => <KV key={i} k={it.topic} v={it.response || <span className="hint">awaiting response</span>} />)}
              </div>
            ))}
          </div>
        </div>
      )}

      {clarMode && (
        <div className="card" style={{ marginTop: 14, borderColor: 'var(--amber)' }}>
          <div className="chead"><h3><Icon name="flag" size={15} /> New clarification round</h3><span className="sub">· batched — sent together as one request</span></div>
          <div className="cbody">
            {items.length === 0 ? <p className="hint" style={{ margin: 0 }}>Add each item the vendor must fix. They’ll receive them all in one message.</p>
              : items.map((it, i) => (
                <div className="pickrow" key={i}>
                  <div style={{ flex: 1 }}><div style={{ fontWeight: 600, fontSize: 13 }}>{it.topic}</div><div className="hint">{it.request}</div></div>
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => setItems((xs) => xs.filter((_, x) => x !== i))}><Icon name="x" size={14} /></button>
                </div>
              ))}
            <div className="grid g2" style={{ marginTop: 12 }}>
              <div className="field" style={{ margin: 0 }}><label>Item / topic</label><input value={topic} aria-label="Clarification topic" placeholder="e.g. ISO 9001 certificate" onChange={(e) => setTopic(e.target.value)} /></div>
              <div className="field" style={{ margin: 0 }}><label>What the vendor must do</label><input value={request} aria-label="Clarification request" placeholder="e.g. Upload a valid certificate" onChange={(e) => setRequest(e.target.value)} /></div>
            </div>
            <button type="button" className="btn btn-out btn-sm" style={{ marginTop: 10 }} onClick={addItem}><Icon name="plus" size={14} /> Add item</button>
          </div>
        </div>
      )}

      {actions}

      {calc && fin && (
        <Modal title="How the assessment is calculated" icon="eye" footer={<button type="button" className="btn btn-pri" onClick={() => setCalc(false)}>Close</button>}>
          <p className="hint" style={{ marginTop: 0 }}>Altman Z′ (private-firm), weighted over 3 years.</p>
          <table className="comp">
            <thead><tr><th>Component</th><th className="amt">FY-2</th><th className="amt">FY-1</th><th className="amt">Current</th><th className="amt">Coef.</th></tr></thead>
            <tbody>
              {([['X1 · Working capital / TA', 'x1', '0.717'], ['X2 · Retained earnings / TA', 'x2', '0.847'], ['X3 · EBIT / TA', 'x3', '3.107'], ['X4 · Equity / TL', 'x4', '0.420'], ['X5 · Revenue / TA', 'x5', '0.998']] as const).map(([lbl, key, coef]) => (
                <tr key={key}><td>{lbl}</td>{fin.years.map((y) => <td className="amt" key={y.yearIndex}>{(y[key] as number).toFixed(3)}</td>)}<td className="amt hint">× {coef}</td></tr>
              ))}
              <tr style={{ background: 'var(--soft)' }}><td style={{ fontWeight: 700 }}>Z′</td>{fin.years.map((y) => <td className="amt" key={y.yearIndex} style={{ fontWeight: 800 }}>{y.z.toFixed(2)}</td>)}<td /></tr>
            </tbody>
          </table>
          <div style={{ marginTop: 12 }}>
            <KV k="Year weighting" v="FY-2 ×0.2 · FY-1 ×0.3 · current ×0.5" />
            <KV k="Weighted Z′" v={fin.weightedZ.toFixed(2)} />
            <KV k="Score" v={`${fin.score} / 100`} />
            <KV k="Band" v={`Band ${fin.band} · ${fin.zone} · ${fin.risk} risk`} />
          </div>
        </Modal>
      )}

      {rejecting && (
        <Modal title="Reject application" icon="x"
          footer={<><button type="button" className="btn btn-out" onClick={() => setRejecting(false)}>Cancel</button>
            <button type="button" className="btn btn-pri btn-danger" disabled={!reason.trim() || reject.isPending} onClick={() => reject.mutate()}>Reject</button></>}>
          <div className="field" style={{ marginBottom: 0 }}><label>Reason (emailed to the vendor)</label>
            <textarea rows={3} value={reason} aria-label="Rejection reason" placeholder="e.g. Incomplete audited accounts" onChange={(e) => setReason(e.target.value)} /></div>
        </Modal>
      )}

      {approved && (
        <Modal title="Approved & onboarded" icon="check" footer={<button type="button" className="btn btn-pri" onClick={onBack}>Back to queue</button>}>
          <p style={{ marginTop: 0 }}>Promoted into the Vendor Master as <b>{approved}</b>. A supplier-portal login has been provisioned and a set-password email sent.</p>
        </Modal>
      )}
      <CustomFieldsSection recordType="Onboarding" recordId={id} />
      <SegmentsSection recordType="Onboarding" recordId={id} />
    </>
  )
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return <div className="card"><div className="chead"><h3>{title}</h3></div><div className="cbody">{children}</div></div>
}
function KV({ k, v, mono }: { k: string; v: React.ReactNode; mono?: boolean }) {
  return <div className="kv"><span className="k">{k}</span><span className={`v ${mono ? 'mono' : ''}`}>{v}</span></div>
}
function Stat({ label, value, sub, color }: { label: string; value: string; sub?: string; color?: string }) {
  return <div className="card stat" style={{ margin: 0 }}><div className="lbl">{label}</div><div className="num" style={{ color }}>{value}</div>{sub && <div className="sub">{sub}</div>}</div>
}

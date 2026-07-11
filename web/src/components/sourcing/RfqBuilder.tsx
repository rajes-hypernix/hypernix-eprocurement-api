import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRfq,
  getForms,
  getVendors,
  getUsers,
  getRequisitions,
  updateRfqDraft,
  releaseRfq,
  createForm,
  updateForm,
  type RfqDetail,
} from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, Modal, Notice, Spinner } from '../ui'
import { roleLabel, fmtDay } from '../../lib/format'
import { fromDto, toDto, type EditItem, type FormSections } from '../../lib/formTypes'
import { QuestionEditor } from './QuestionEditor'
import { useSwec } from '../../api/swec'
import { SwecPicker } from '../vendors/SwecPicker'
import { Requisitions } from './Requisitions'
import { RfqDetailHub } from './RfqDetailHub'

interface Draft {
  title: string
  envelope: string
  currency: string
  opens: string
  closes: string
  lines: NonNullable<RfqDetail['lines']>
  items: EditItem[]
  sections: FormSections
  invited: string[]
  techEvals: string[]
  commEvals: string[]
}

const isoToDate = (iso: string | null | undefined) => (iso ? iso.slice(0, 10) : '')
const dateToIso = (d: string) => (d ? new Date(`${d}T00:00:00Z`).toISOString() : null)

function fromRfq(r: RfqDetail): Draft {
  return {
    title: r.title ?? '',
    envelope: r.envelope ?? 'Dual',
    currency: r.currency ?? 'MYR',
    opens: isoToDate(r.opensUtc),
    closes: isoToDate(r.closesUtc),
    lines: r.lines ?? [],
    items: (r.formItems ?? []).map(fromDto),
    sections: { technical: [...(r.technicalSections ?? [])], commercial: [...(r.commercialSections ?? [])] },
    invited: [...(r.invitedVendorIds ?? [])],
    techEvals: [...(r.technicalEvaluatorIds ?? [])],
    commEvals: [...(r.commercialEvaluatorIds ?? [])],
  }
}

export function RfqBuilder({ id, onBack, onNavigate }: { id: string; onBack: () => void; onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const { data: rfq, isPending } = useQuery({ queryKey: ['rfq', id], queryFn: () => getRfq(id) })
  const { data: forms = [] } = useQuery({ queryKey: ['forms'], queryFn: getForms })
  const { data: vendors = [] } = useQuery({ queryKey: ['vendors', '', 'all', 'all'], queryFn: () => getVendors() })
  const { data: users = [] } = useQuery({ queryKey: ['users'], queryFn: getUsers })
  const { data: prs = [] } = useQuery({ queryKey: ['requisitions'], queryFn: getRequisitions })

  const [draft, setDraft] = useState<Draft | null>(null)
  const [step, setStep] = useState(0)
  const [notice, setNotice] = useState('')
  const [showRelease, setShowRelease] = useState(false)
  const [srcOpen, setSrcOpen] = useState<Set<string>>(new Set())
  const [vSearch, setVSearch] = useState('')
  const [vRegion, setVRegion] = useState('all')
  const [vState, setVState] = useState('all')
  const [vType, setVType] = useState('all')
  const [catGroups, setCatGroups] = useState<string[][]>([[]])
  const [activeGroup, setActiveGroup] = useState(0)
  const [showCat, setShowCat] = useState(false)
  const [showAddPrs, setShowAddPrs] = useState(false)
  const [libForm, setLibForm] = useState<{ id: string; name: string } | null>(null)
  const [saveName, setSaveName] = useState<string | null>(null)
  const { data: swec } = useSwec()

  const d = draft ?? (rfq ? fromRfq(rfq) : null)
  const patch = (p: Partial<Draft>) => setDraft({ ...(d as Draft), ...p })

  const toRequest = (cur: Draft) => ({
    title: cur.title,
    envelope: cur.envelope,
    currency: cur.currency,
    opensUtc: dateToIso(cur.opens),
    closesUtc: dateToIso(cur.closes),
    lines: cur.lines,
    formItems: cur.items.map((it, i) => toDto(it, i)),
    technicalSections: cur.sections.technical,
    commercialSections: cur.sections.commercial,
    invitedVendorIds: cur.invited,
    technicalEvaluatorIds: cur.techEvals,
    commercialEvaluatorIds: cur.commEvals,
  })

  const save = useMutation({
    mutationFn: (cur: Draft) => updateRfqDraft(id, toRequest(cur)),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['rfq', id] })
      void qc.invalidateQueries({ queryKey: ['rfqs'] })
      setNotice('Draft saved')
    },
  })
  const release = useMutation({
    mutationFn: async (cur: Draft) => {
      await updateRfqDraft(id, toRequest(cur))
      return releaseRfq(id)
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['rfqs'] })
      onBack()
    },
    onError: (e: Error) => setNotice(e.message),
  })

  const formPayload = (cur: Draft, name: string) => ({
    name,
    items: cur.items.map((it, i) => toDto(it, i)),
    technicalSections: cur.sections.technical,
    commercialSections: cur.sections.commercial,
  })
  const saveAsForm = useMutation({
    mutationFn: (name: string) => createForm(formPayload(d as Draft, name.trim() || 'New questionnaire')),
    onSuccess: (f) => { void qc.invalidateQueries({ queryKey: ['forms'] }); if (f.id) setLibForm({ id: f.id, name: f.name ?? 'form' }); setSaveName(null); setNotice(`Saved as new form: ${f.name}`) },
    onError: (e: Error) => { setSaveName(null); setNotice(`Could not save form: ${e.message}`) },
  })
  const updateLibForm = useMutation({
    mutationFn: (cur: Draft) => updateForm(libForm!.id, formPayload(cur, libForm!.name)),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['forms'] }); setNotice(`Updated “${libForm!.name}”`) },
    onError: (e: Error) => setNotice(`Could not update form: ${e.message}`),
  })

  const techEvaluators = useMemo(() => users.filter((u) => (u.roles ?? []).includes('TechEvaluator')), [users])
  const commEvaluators = useMemo(() => users.filter((u) => (u.roles ?? []).includes('CommEvaluator')), [users])

  if (isPending || !rfq || !d) return <Spinner label="Loading RFQ…" />

  // Released RFQs render the adaptive detail hub (procRFQDetail).
  if (rfq.status !== 'Draft') return <RfqDetailHub rfq={rfq} onBack={onBack} onNavigate={onNavigate} />

  const steps =
    d.envelope === 'Dual'
      ? ['Items', 'Settings', 'Questions', 'Vendors', 'Evaluators', 'Review']
      : ['Items', 'Settings', 'Questions', 'Vendors', 'Review']
  const last = steps.length - 1
  const stepName = steps[step]

  const toggle = (arr: string[], v: string) => (arr.includes(v) ? arr.filter((x) => x !== v) : [...arr, v])

  // Settings step requires a title + both bid dates before you can move past it —
  // enforced both on Continue and on any forward jump via the step rail.
  const settingsComplete = !!(d.title && d.opens && d.closes)
  const settingsIdx = steps.indexOf('Settings')
  const validateStep = () => {
    if (stepName === 'Settings' && !settingsComplete) {
      setNotice('RFQ title, bid open date and bid close date are required.')
      return false
    }
    setNotice('')
    return true
  }
  const goToStep = (i: number) => {
    if (i > settingsIdx && !settingsComplete) {
      setNotice('Complete the RFQ title and bid dates in Settings first.')
      setStep(settingsIdx)
      return
    }
    setNotice('')
    setStep(i)
  }

  return (
    <>
      <div className="crumb">
        <a onClick={onBack}>RFQs</a> <Icon name="chev" size={13} /> <span>{rfq.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Create RFQ</h1>
          <p>
            {rfq.code} · from {(rfq.prRefs ?? []).join(', ') || '—'}
          </p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          {step > 0 && (
            <button type="button" className="btn btn-out" onClick={() => setStep((s) => Math.max(s - 1, 0))}>
              <Icon name="back" size={15} /> Back
            </button>
          )}
          <button type="button" className="btn btn-out" onClick={() => save.mutate(d)}>
            <Icon name="doc" size={15} /> Save as draft
          </button>
          {step < last ? (
            <button type="button" className="btn btn-pri" onClick={() => { if (validateStep()) setStep((s) => Math.min(s + 1, last)) }}>
              Continue <Icon name="chev" size={15} />
            </button>
          ) : (
            <button type="button" className="btn btn-pri" onClick={() => (d.invited.length ? setShowRelease(true) : setNotice('Invite at least one vendor before releasing'))}>
              <Icon name="send" size={15} /> Release to {d.invited.length} vendor{d.invited.length !== 1 ? 's' : ''}
            </button>
          )}
        </div>
      </div>

      <div className="steps">
        {steps.map((s, i) => (
          <span key={s} style={{ display: 'contents' }}>
            <button type="button" className={`step${i === step ? ' on' : i < step ? ' done' : ''}`} onClick={() => goToStep(i)}>
              <span className="n">{i < step ? '✓' : i + 1}</span>{s}
            </button>
            {i < steps.length - 1 && <span className="sep" />}
          </span>
        ))}
      </div>

      {notice && <Notice>{notice}</Notice>}

      {stepName === 'Items' && (() => {
        const srcRefs = [...new Set(d.lines.map((l) => l.prRef).filter(Boolean) as string[])]
        const inDraft = (prRef: string, itemCode: string) => d.lines.some((x) => x.prRef === prRef && x.itemCode === itemCode)
        return (
          <>
            <div className="card">
              <div className="chead">
                <h3>RFQ Line Items</h3>
                <div className="spacer" />
                <span className="hint" style={{ marginRight: 12 }}>{d.lines.length} {d.lines.length === 1 ? 'line' : 'lines'} from {srcRefs.length} PR{srcRefs.length !== 1 ? 's' : ''}</span>
                <button type="button" className="btn btn-out btn-sm" onClick={() => setShowAddPrs(true)}><Icon name="plus" size={15} /> Add PRs</button>
              </div>
              <table className="rfqlt">
                <thead>
                  <tr><th>PR #</th><th>Item</th><th>Description</th><th className="amt">Qty</th><th>UoM</th><th /></tr>
                </thead>
                <tbody>
                  {d.lines.map((l, i) => {
                    const first = i > 0 && l.prRef !== d.lines[i - 1].prRef
                    return (
                      <tr key={i} className={first ? 'firstof' : ''}>
                        <td><span className="prtag">{l.prRef ?? '—'}</span></td>
                        <td>{l.itemCode}</td>
                        <td>{l.description}</td>
                        <td className="amt">{l.qty}</td>
                        <td>{l.uom}</td>
                        <td className="amt">
                          <button type="button" className="btn btn-ghost btn-sm" title="Remove line" onClick={() => patch({ lines: d.lines.filter((_, x) => x !== i) })}><Icon name="x" size={15} /></button>
                        </td>
                      </tr>
                    )
                  })}
                  {d.lines.length === 0 && (
                    <tr><td colSpan={6} style={{ padding: 22, textAlign: 'center', color: 'var(--muted)' }}>No lines yet. Add lines from Requisitions.</td></tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="srcsec">Source requisitions</div>
            {srcRefs.length === 0 && <p className="hint">No source requisitions.</p>}
            {srcRefs.map((ref) => {
              const pr = prs.find((p) => p.code === ref)
              if (!pr) return null
              const open = srcOpen.has(ref)
              const prLines = pr.lines ?? []
              const inCount = prLines.filter((l) => inDraft(ref, l.itemCode!)).length
              return (
                <div className={`prc ${open ? 'open' : ''}`} key={ref}>
                  <div className="prh" onClick={() => setSrcOpen((p) => { const n = new Set(p); if (n.has(ref)) n.delete(ref); else n.add(ref); return n })}>
                    <span className="chev"><Icon name="chev" size={13} /></span>
                    <span className="pid">{pr.code}</span>
                    <span className="who">{pr.requestor} · {pr.department} · {pr.location ?? ''}</span>
                    <span className="sp" />
                    <span className="inb">{inCount} of {prLines.length} lines in this RFQ</span>
                  </div>
                  {open && (
                    <div className="prb">
                      <div className="meta">
                        <div><div className="k">Memo</div><div className="v">{pr.memo || '—'}</div></div>
                        <div><div className="k">Job</div><div className="v">{pr.job || '—'}</div></div>
                        <div><div className="k">Category</div><div className="v">{pr.category || '—'}</div></div>
                        <div><div className="k">Raised</div><div className="v">{fmtDay(pr.raisedDate)}</div></div>
                        <div><div className="k">Required by</div><div className="v">{fmtDay(pr.requiredDate)}</div></div>
                        <div><div className="k">Requestor</div><div className="v">{pr.requestor}</div></div>
                      </div>
                      <table>
                        <thead><tr><th>Item</th><th>Description</th><th className="amt">Qty</th><th>UoM</th><th>In RFQ</th></tr></thead>
                        <tbody>
                          {prLines.map((l) => {
                            const isIn = inDraft(ref, l.itemCode!)
                            return (
                              <tr key={l.itemCode} className={isIn ? '' : 'out'}>
                                <td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{l.qty}</td><td>{l.uom}</td>
                                <td>{isIn
                                  ? <span className="inmark"><Icon name="check" size={13} /> In this RFQ</span>
                                  : (l.status ?? 'available') === 'available'
                                    ? <span className="outmark"><span className="dd" />Available, not added</span>
                                    : <span className="hint">{l.ref ?? 'Sourced'}</span>}</td>
                              </tr>
                            )
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              )
            })}
          </>
        )
      })()}

      {stepName === 'Settings' && (
        <div className="card">
          <div className="cbody">
            <div className="field">
              <label>RFQ title *</label>
              <input type="text" required value={d.title} placeholder="e.g. Pump & VFD Package — Facilities Upgrade" onChange={(e) => patch({ title: e.target.value })} style={!d.title ? { borderColor: 'var(--amber)' } : undefined} />
            </div>
            <div className="grid g2">
              <div className="field">
                <label>Bid open date *</label>
                <input type="date" required value={d.opens} onChange={(e) => patch({ opens: e.target.value })} style={!d.opens ? { borderColor: 'var(--amber)' } : undefined} />
              </div>
              <div className="field">
                <label>Bid close date *</label>
                <input type="date" required value={d.closes} onChange={(e) => patch({ closes: e.target.value })} style={!d.closes ? { borderColor: 'var(--amber)' } : undefined} />
              </div>
            </div>
            <div className="field" style={{ maxWidth: 240 }}>
              <label>Currency</label>
              <select value={d.currency} onChange={(e) => patch({ currency: e.target.value })}>
                {['MYR', 'USD', 'SGD', 'EUR', 'GBP'].map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Envelope type</label>
              <div className="grid g2" style={{ gap: 12 }}>
                {[
                  ['Single', 'Single envelope', 'Commercial & technical opened together when bids close.'],
                  ['Dual', 'Dual envelope (sealed)', 'Technical opened & scored first; commercial stays sealed until technical pass.'],
                ].map(([val, title, desc]) => (
                  <div
                    key={val}
                    className="addr"
                    style={{ cursor: 'pointer', borderColor: d.envelope === val ? 'var(--teal)' : 'var(--line)' }}
                    onClick={() => patch({ envelope: val })}
                  >
                    <label className="ck">
                      <input type="radio" checked={d.envelope === val} onChange={() => patch({ envelope: val })} /> <strong>{title}</strong>
                    </label>
                    <p className="hint" style={{ margin: '8px 0 0' }}>{desc}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {stepName === 'Questions' && (
        <>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="cbody" style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
              <label className="hint" style={{ fontWeight: 700 }}>Load from library</label>
              <select
                value={libForm?.id ?? ''}
                onChange={(e) => {
                  const f = forms.find((x) => x.id === e.target.value)
                  if (f) { patch({ items: (f.items ?? []).map(fromDto), sections: { technical: [...(f.technicalSections ?? [])], commercial: [...(f.commercialSections ?? [])] } }); setLibForm({ id: f.id!, name: f.name ?? 'form' }) }
                  else setLibForm(null)
                }}
                style={{ maxWidth: 320 }}
              >
                <option value="">Choose a form…</option>
                {forms.map((f) => (
                  <option key={f.id} value={f.id!}>{f.name}</option>
                ))}
              </select>
              <span className="hint">Two sealed groups — Commercial & Technical. Add sub-groups and any of the 12 field types.</span>
            </div>
          </div>
          <QuestionEditor items={d.items} sections={d.sections} onChange={(items, sections) => patch({ items, sections })} />
          <div className="actbar" style={{ marginTop: 14 }}>
            <span className="hint" style={{ flex: 1, minWidth: 160 }}>Editing for this RFQ. Save to the library to reuse this form on future RFQs.</span>
            <button type="button" className="btn btn-out btn-sm" disabled={saveAsForm.isPending} onClick={() => setSaveName(d.title ? `${d.title} questionnaire` : 'New questionnaire')}><Icon name="plus" size={13} /> Save as new form</button>
            {libForm && <button type="button" className="btn btn-out btn-sm" disabled={updateLibForm.isPending} onClick={() => updateLibForm.mutate(d)}><Icon name="check" size={13} /> Update “{libForm.name}”</button>}
          </div>
        </>
      )}

      {stepName === 'Vendors' && (() => {
        const regions = [...new Set(vendors.map((v) => v.region).filter(Boolean) as string[])].sort()
        const states = [...new Set(vendors.map((v) => v.state).filter(Boolean) as string[])].sort()
        const matchedDesc = new Set(catGroups.flat().flatMap((c) => swec?.desc(c) ?? [c]))
        const activeCats = catGroups.filter((g) => g.length)
        const matchesCats = (v: typeof vendors[number]) => {
          if (activeCats.length === 0) return true
          const cats = v.categories ?? []
          return activeCats.some((g) => g.every((c) => (swec?.desc(c) ?? [c]).some((x) => cats.includes(x))))
        }
        const shown = vendors.filter((v) =>
          (!vSearch || (v.name ?? '').toLowerCase().includes(vSearch.toLowerCase())) &&
          (vRegion === 'all' || v.region === vRegion) &&
          (vState === 'all' || v.state === vState) &&
          (vType === 'all' || v.type === vType) &&
          matchesCats(v))
        const selectAllShown = () => patch({ invited: [...new Set([...d.invited, ...shown.map((v) => v.id!)])] })
        const setGroup = (gi: number, codes: string[]) => setCatGroups((gs) => gs.map((g, i) => (i === gi ? codes : g)))
        const nGroups = activeCats.length
        return (
          <>
            <div className="card" style={{ marginBottom: 12 }}>
              <div className="cbody">
                <div className="grid" style={{ gridTemplateColumns: '1.6fr 1fr 1fr 1fr', gap: 10, alignItems: 'end', marginBottom: 11 }}>
                  <div className="field" style={{ margin: 0 }}><label>Search vendor</label><input type="text" value={vSearch} placeholder="Vendor name" onChange={(e) => setVSearch(e.target.value)} /></div>
                  <div className="field" style={{ margin: 0 }}><label>Region</label><select value={vRegion} onChange={(e) => setVRegion(e.target.value)}><option value="all">All regions</option>{regions.map((r) => <option key={r} value={r}>{r}</option>)}</select></div>
                  <div className="field" style={{ margin: 0 }}><label>State</label><select value={vState} onChange={(e) => setVState(e.target.value)}><option value="all">All states</option>{states.map((s) => <option key={s} value={s}>{s}</option>)}</select></div>
                  <div className="field" style={{ margin: 0 }}><label>Type</label><select value={vType} onChange={(e) => setVType(e.target.value)}><option value="all">All</option><option value="SWEC">SWEC</option><option value="Non-SWEC">Non-SWEC</option></select></div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap', marginBottom: 8 }}>
                  <button type="button" className="btn btn-out btn-sm" onClick={() => setShowCat(true)}><Icon name="split" size={14} /> SWEC categories{nGroups ? ` · ${catGroups.reduce((n, g) => n + g.length, 0)} tag(s)` : ''}</button>
                  <span className="hint" style={{ fontSize: 12 }}>Vendors match <b>all</b> categories in a group, and <b>any</b> group overall.</span>
                  <div style={{ flex: 1 }} />
                  {nGroups > 0 && <button type="button" className="freset" onClick={() => { setCatGroups([[]]); setActiveGroup(0) }}>Clear</button>}
                </div>
                <div className="grpwrap">
                  {catGroups.map((g, gi) => (
                    <span key={gi}>
                      <span className={`grpbox${gi === activeGroup ? ' active' : ''}`} onClick={() => setActiveGroup(gi)}>
                        {g.length ? g.map((c, ci) => (
                          <span key={c}>
                            <span className="swchip" style={{ cursor: 'pointer' }} title={swec?.path(c)} onClick={(e) => { e.stopPropagation(); setGroup(gi, g.filter((x) => x !== c)) }}>{swec?.label(c) ?? c} <Icon name="x" size={11} /></span>
                            {ci < g.length - 1 && <span className="andsep">and</span>}
                          </span>
                        )) : <span className="hint" style={{ fontSize: 12 }}>empty — pick categories</span>}
                        {catGroups.length > 1 && <span className="grpdel" onClick={(e) => { e.stopPropagation(); setCatGroups((gs) => { const n = gs.filter((_, i) => i !== gi); return n.length ? n : [[]] }); setActiveGroup(0) }}><Icon name="x" size={12} /></span>}
                      </span>
                      {gi < catGroups.length - 1 && <span className="orsep">or</span>}
                    </span>
                  ))}
                  <button type="button" className="btn btn-out btn-sm grpadd" onClick={() => { if (catGroups[activeGroup]?.length) { setCatGroups((gs) => [...gs, []]); setActiveGroup(catGroups.length) } }}><Icon name="plus" size={13} /> OR group</button>
                </div>
              </div>
            </div>
            <div className="card">
              <div className="chead">
                <h3>Vendors</h3>
                <div className="spacer" />
                <span className="hint" style={{ marginRight: 8 }}>{shown.length} shown · {d.invited.length} selected{nGroups ? ` · ${nGroups} category group(s)` : ''}</span>
                <button type="button" className="btn btn-ghost btn-sm" onClick={selectAllShown}>Select all shown</button>
                {d.invited.length > 0 && <button type="button" className="btn btn-ghost btn-sm" onClick={() => patch({ invited: [] })}>Unselect all</button>}
              </div>
              <div className="cbody" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {shown.map((v) => {
                  const on = d.invited.includes(v.id!)
                  return (
                    <div className={`vpick ${on ? 'on' : ''}`} key={v.id} onClick={() => patch({ invited: toggle(d.invited, v.id!) })}>
                      <span className="vck">{on && <Icon name="check" size={12} />}</span>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontWeight: 700 }}>{v.name} {v.type === 'SWEC' && <span className="badge b-teal" style={{ marginLeft: 4 }}>SWEC</span>}</div>
                        <div className="hint">{v.code} · {v.state} · {v.region} · ★ {v.rating}</div>
                        <div style={{ marginTop: 5 }}>{(v.categories ?? []).map((c) => <span key={c} className={`swchip${matchedDesc.has(c) ? ' hit' : ''}`}>{swec?.label(c) ?? c}</span>)}</div>
                      </div>
                    </div>
                  )
                })}
                {shown.length === 0 && <div className="empty" style={{ padding: 26 }}>No vendors match these filters.</div>}
              </div>
            </div>
            {d.invited.length > 0 && (
              <div className="card" style={{ marginTop: 12 }}>
                <div className="cbody">
                  <div className="hint" style={{ marginBottom: 6, fontWeight: 700 }}>Invited ({d.invited.length})</div>
                  <div>
                    {d.invited.map((vid) => {
                      const v = vendors.find((x) => x.id === vid)
                      return (
                        <span className="chip" key={vid} style={{ cursor: 'pointer' }} onClick={() => patch({ invited: toggle(d.invited, vid) })}>
                          {v?.name ?? vid} <Icon name="x" size={11} />
                        </span>
                      )
                    })}
                  </div>
                </div>
              </div>
            )}
            {showCat && (
              <SwecPicker
                initial={catGroups[activeGroup] ?? []}
                vendorName={`OR group ${activeGroup + 1}`}
                onCancel={() => setShowCat(false)}
                onSave={(codes) => { setCatGroups((gs) => gs.map((g, i) => (i === activeGroup ? codes : g))); setShowCat(false) }}
              />
            )}
          </>
        )
      })()}

      {stepName === 'Evaluators' && (
        <>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="chead"><h3>Technical envelope — evaluators</h3></div>
            <div className="cbody">
              <p className="hint" style={{ marginTop: 0 }}>Only assigned evaluators can open and score the sealed technical envelope.</p>
              {techEvaluators.map((u) => (
                <label className="ck" key={u.id} style={{ padding: '6px 0' }}>
                  <input type="checkbox" checked={d.techEvals.includes(u.code!)} onChange={() => patch({ techEvals: toggle(d.techEvals, u.code!) })} />
                  <span style={{ fontWeight: 600 }}>{u.name}</span>
                  <span className="hint">· {roleLabel('TechEvaluator')}</span>
                </label>
              ))}
            </div>
          </div>
          <div className="card">
            <div className="chead"><h3>Commercial envelope — evaluators</h3></div>
            <div className="cbody">
              {commEvaluators.map((u) => (
                <label className="ck" key={u.id} style={{ padding: '6px 0' }}>
                  <input type="checkbox" checked={d.commEvals.includes(u.code!)} onChange={() => patch({ commEvals: toggle(d.commEvals, u.code!) })} />
                  <span style={{ fontWeight: 600 }}>{u.name}</span>
                  <span className="hint">· {roleLabel('CommEvaluator')}</span>
                </label>
              ))}
            </div>
          </div>
        </>
      )}

      {stepName === 'Review' && (
        <div className="card">
          <div className="chead"><h3>Review &amp; send</h3></div>
          <div className="cbody">
            <div className="grid g2">
              <div><div className="hint">Title</div><div style={{ fontWeight: 700 }}>{d.title || '—'}</div></div>
              <div><div className="hint">Bid window</div><div style={{ fontWeight: 700 }}>{d.opens || '—'} → {d.closes || '—'}</div></div>
              <div><div className="hint">Currency</div><div style={{ fontWeight: 700 }}>{d.currency}</div></div>
              <div><div className="hint">Envelope</div><div style={{ fontWeight: 700 }}>{d.envelope}</div></div>
              <div><div className="hint">Lines / Questions</div><div style={{ fontWeight: 700 }}>{d.lines.length} lines · {d.items.filter((i) => i.kind === 'question').length} questions</div></div>
              <div><div className="hint">Vendors</div><div style={{ fontWeight: 700 }}>{d.invited.length} invited</div></div>
            </div>
            <p className="hint" style={{ marginTop: 14 }}>
              Releasing captures the closing date as the server-side bid deadline and opens the RFQ to invited vendors.
            </p>
          </div>
        </div>
      )}

      {showRelease && (
        <ConfirmModal
          icon="send" title="Release RFQ to Vendors"
          body={
            <>
              <p className="hint" style={{ marginTop: 0 }}>
                Once released, the invited vendors can see this RFQ and start bidding. Line items and questions are locked after release — you can still send clarifications.
              </p>
              <div className="locked-note">
                <b>{d.title || 'Untitled RFQ'}</b><br />
                {d.lines.length} line(s) · {d.items.filter((i) => i.kind === 'question').length} question(s) · {d.invited.length} vendor(s) · closes {d.closes || '—'}
              </div>
            </>
          }
          cancelLabel="Not yet" confirmLabel="Release now" confirmIcon="send" busy={release.isPending}
          onCancel={() => setShowRelease(false)} onConfirm={() => { setShowRelease(false); release.mutate(d) }}
        />
      )}

      {saveName !== null && (
        <Modal
          icon="doc" title="Save as new form"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setSaveName(null)}>Cancel</button>
              <button type="button" className="btn btn-pri" disabled={!saveName.trim() || saveAsForm.isPending} onClick={() => saveAsForm.mutate(saveName)}><Icon name="check" size={15} /> Save form</button>
            </>
          }
        >
          <p className="hint" style={{ marginTop: 0 }}>Save this questionnaire to the form library so you can reuse it on future RFQs.</p>
          <div className="field"><label>Form name</label>
            <input type="text" autoFocus value={saveName} onChange={(e) => setSaveName(e.target.value)} placeholder="e.g. Standard Technical Questionnaire" />
          </div>
        </Modal>
      )}

      {showAddPrs && (
        <div className="addpr-overlay">
          <div className="addpr-head">
            <Icon name="rfq" size={18} /><h3>Add PRs to This RFQ</h3>
            <div style={{ flex: 1 }} />
            <button type="button" className="x" onClick={() => setShowAddPrs(false)}>×</button>
          </div>
          <div className="addpr-body">
            <Requisitions
              onOpenRfq={() => {}}
              addTo={{
                existing: new Set(d.lines.map((l) => `${l.prRef}:${l.itemCode}`)),
                onAdd: (newLines) => { patch({ lines: [...d.lines, ...newLines] }); setShowAddPrs(false) },
              }}
            />
          </div>
        </div>
      )}
    </>
  )
}


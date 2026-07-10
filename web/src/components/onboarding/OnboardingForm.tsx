import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import {
  getOnboardingDraft, saveOnboardingDraft, submitOnboardingDraft,
  uploadOnboardingDocument, deleteOnboardingDocument,
  type OnboardingFinancialYear, type OnboardingDocumentDto,
} from '../../api/client'
import { AnswerInput } from '../vendor/BidForm'
import { FIN_ITEMS, blankFin, computeFin, type FinData, type FinKey } from '../../lib/altmanZ'
import { useLookups } from '../../lib/lookups'
import { useSwec } from '../../api/swec'
import { SwecPicker } from '../vendors/SwecPicker'
import { Icon } from '../Icon'
import { Notice, Spinner } from '../ui'

// Document checklist (SPEC §5) — filtered by registration type.
const DOCS: { id: string; name: string; req: 'all' | 'opt' | 'swec' | 'nonswec' }[] = [
  { id: 'ssm', name: 'SSM / CCM Registration', req: 'all' },
  { id: 'iso', name: 'ISO 9001:2015 Certificate', req: 'opt' },
  { id: 'cidb', name: 'CIDB Grade Certificate', req: 'opt' },
  { id: 'bank', name: 'Bank Confirmation Letter', req: 'all' },
  { id: 'acct', name: 'Audited Accounts (3 years)', req: 'nonswec' },
  { id: 'swec', name: 'PETRONAS SWEC Certificate', req: 'swec' },
]
const FIN_FIELDS: Record<FinKey, keyof OnboardingFinancialYear> = {
  revenue: 'revenue', netProfit: 'netProfit', ebit: 'ebit', totalAssets: 'totalAssets',
  currentAssets: 'currentAssets', inventory: 'inventory', currentLiab: 'currentLiabilities',
  totalLiab: 'totalLiabilities', equity: 'equity', retainedEarnings: 'retainedEarnings', fixedAssets: 'fixedAssets',
}

const answerKey = (packId: string, order: number) => `${packId}:${order}`

function finFromDraft(years: OnboardingFinancialYear[]): FinData {
  const fin = blankFin()
  for (const y of years) for (const [, k] of FIN_ITEMS) fin[k][y.yearIndex] = Number(y[FIN_FIELDS[k]]) || 0
  return fin
}
function finToDraft(fin: FinData): OnboardingFinancialYear[] {
  return [0, 1, 2].map((i) => {
    const y = { yearIndex: i } as OnboardingFinancialYear
    for (const [, k] of FIN_ITEMS) (y[FIN_FIELDS[k]] as number) = fin[k][i]
    return y
  })
}

export function OnboardingForm({ token, onSubmitted }: { token: string; onSubmitted: () => void }) {
  const { data: draft, isPending } = useQuery({
    queryKey: ['onboarding-draft', token],
    queryFn: () => getOnboardingDraft(token),
    retry: false,
  })

  const [h, setH] = useState({ name: '', reg: '', location: '', email: '', contactName: '', contactPhone: '' })
  const [bank, setBank] = useState({ bank: '', accountNo: '', swift: '' })
  const [country, setCountry] = useState('MY')
  const [geoState, setGeoState] = useState('')
  const [stepErr, setStepErr] = useState<string[] | null>(null)
  const [visited, setVisited] = useState<Set<number>>(new Set([0]))
  const { of, hasCities } = useLookups()               // same conformed-dimension lists as the manual form
  const { data: swec } = useSwec()                     // the real SWEC index — for resolving codes to labels
  const [cats, setCats] = useState<string[]>([])
  const [picking, setPicking] = useState(false)
  const [fin, setFin] = useState<FinData>(blankFin())
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [docs, setDocs] = useState<OnboardingDocumentDto[]>([])
  const [step, setStep] = useState(0)
  const [err, setErr] = useState<string | null>(null)
  const hydrated = useRef(false)

  useEffect(() => {
    if (!draft || hydrated.current) return
    hydrated.current = true
    setH({
      name: draft.name, reg: draft.registrationNo, location: draft.location, email: draft.email,
      contactName: draft.contactName, contactPhone: draft.contactPhone,
    })
    setBank(draft.bank)
    setCountry(draft.country || 'MY')
    setGeoState(draft.state || '')
    setCats(draft.categories)
    setFin(finFromDraft(draft.financials))
    setDocs(draft.documents)
    const a: Record<string, string> = {}
    for (const p of draft.packs) for (const ans of draft.answers) if (ans.formTemplateId === p.id) a[answerKey(p.id, ans.questionOrder)] = ans.value
    setAnswers(a)
  }, [draft])

  const isSwec = draft?.type === 'SWEC'
  const steps = useMemo(() => {
    const s = [{ k: 'company', n: 'Company & contact' }, { k: 'banking', n: 'Banking' }, { k: 'docs', n: 'Documents' }, { k: 'cats', n: 'SWEC categories' }]
    if (!isSwec) s.push({ k: 'fin', n: 'Financials' })
    for (const p of draft?.packs ?? []) s.push({ k: `pack:${p.id}`, n: p.name })
    s.push({ k: 'review', n: 'Review & submit' })
    return s
  }, [draft, isSwec])

  const body = () => ({
    token,
    name: h.name, registrationNo: h.reg, location: h.location, email: h.email,
    contactName: h.contactName, contactPhone: h.contactPhone,
    country, state: geoState,
    bank, categories: cats,
    financials: isSwec ? null : finToDraft(fin),
    answers: (draft?.packs ?? []).flatMap((p) =>
      p.items.filter((i) => i.kind === 'question').map((i) => ({ formTemplateId: p.id, questionOrder: i.order, value: answers[answerKey(p.id, i.order)] ?? '' }))),
  })

  // Single-flight autosave: chain draft saves so rapid section changes can't land out of order and
  // overwrite newer edits with a slower, older request. Each save captures the current state via body().
  const saveChain = useRef<Promise<unknown>>(Promise.resolve())
  const saveDraft = () => {
    saveChain.current = saveChain.current
      .catch(() => {})
      .then(() => saveOnboardingDraft(body()))
      .catch((e: unknown) => setErr(e instanceof Error ? e.message : 'Could not save your progress.'))
    return saveChain.current
  }
  const submit = useMutation({
    mutationFn: async () => { await saveDraft(); return submitOnboardingDraft(token) },
    onSuccess: onSubmitted,
    onError: (e: Error) => setErr(e.message),
  })

  // Save-and-resume: all navigation is free (progress autosaves). Completeness is shown per step (amber)
  // and enforced on submit.
  const goto = (i: number) => {
    setErr(null); setStepErr(null); void saveDraft()
    const t = Math.max(0, Math.min(steps.length - 1, i))
    setVisited((v) => new Set(v).add(t)); setStep(t)
  }

  if (isPending) return <div className="onboard-land"><div className="onboard-card"><Spinner label="Opening your application…" /></div></div>
  if (!draft) return <div className="onboard-land"><div className="onboard-card"><Notice tone="error" icon="x">This onboarding link is not valid or has expired.</Notice></div></div>

  const cur = steps[step]
  const fc = computeFin(fin)
  const missing = [!h.name && 'Registered name', !h.reg && 'Reg. no.'].filter(Boolean) as string[]

  // Required fields for a section — used to WARN (amber step), not to block. Enforced on submit.
  const stepMissing = (k: string): string[] => {
    if (k === 'company') return [!h.name.trim() && 'Registered name', !h.reg.trim() && 'Reg. no. (SSM)'].filter(Boolean) as string[]
    if (k === 'docs') return DOCS.filter((d) => d.req === 'all' && !docs.some((x) => x.key === d.id)).map((d) => d.name)   // required attachments
    if (k === 'fin' && !isSwec)
      return [0, 1, 2].some((i) => fin.totalAssets[i] <= 0 || fin.totalLiab[i] <= 0)
        ? ['Positive total assets & total liabilities for all three years'] : []
    if (k.startsWith('pack:')) {
      const pack = draft.packs.find((p) => `pack:${p.id}` === k)
      return (pack?.items ?? []).filter((it) => it.kind === 'question' && it.required && !(answers[answerKey(pack!.id, it.order)] ?? '').trim()).map((it) => it.label)
    }
    return []
  }
  const stepComplete = (k: string) => stepMissing(k).length === 0
  // Progress saves automatically, so the vendor may move on and finish later — Next never blocks.
  const next = () => { setStepErr(null); goto(step + 1) }
  // Enforce completeness only on submit: list the sections still needing attention.
  const incompleteSections = steps.filter((s) => !stepComplete(s.k)).map((s) => s.n)
  const trySubmit = () => {
    if (incompleteSections.length > 0) { setStepErr(incompleteSections); return }
    setStepErr(null); submit.mutate()
  }

  return (
    <div className="ob-shell">
      <div className="pagehead">
        <div>
          <h1>{draft.name || 'Supplier'} — onboarding</h1>
          <p>{draft.type} · {draft.code} · progress saves automatically.</p>
        </div>
      </div>

      {err && <Notice tone="error" icon="x">{err}</Notice>}

      <div className="obwrap">
        <div className="obside">
          {steps.map((st, i) => {
            const done = visited.has(i) && stepComplete(st.k)
            const warn = visited.has(i) && !stepComplete(st.k) && i !== step   // visited but incomplete → amber
            return (
              <button type="button" key={st.k} className={`obstep ${i === step ? 'on' : ''} ${done ? 'done' : ''} ${warn ? 'warn' : ''}`} onClick={() => goto(i)}>
                <span className="n">{done ? '✓' : warn ? '!' : i + 1}</span> {st.n}
              </button>
            )
          })}
        </div>

        <div className="obmain">
          {cur.k === 'company' && (
            <Section title="Company & contact">
              <div className="grid g2">
                <Field label="Registered name" required value={h.name} onChange={(v) => setH({ ...h, name: v })} />
                <Field label="Reg. no. (SSM)" required value={h.reg} placeholder="1234567-A" onChange={(v) => setH({ ...h, reg: v })} />
              </div>
              <div className="grid g3">
                <Select label="Country" value={country} onChange={(v) => { setCountry(v); setGeoState(''); setH((p) => ({ ...p, location: '' })) }} options={of('COUNTRY')} />
                {country === 'MY'
                  ? <Select label="State / Region" value={geoState} placeholder="Select state" onChange={(v) => { setGeoState(v); setH((p) => ({ ...p, location: '' })) }} options={of('STATE', 'MY')} />
                  : <Field label="State / Region" value={geoState} onChange={setGeoState} />}
                {country === 'MY' && geoState && hasCities(geoState)
                  ? <Select label="City" value={h.location} placeholder="Select city" onChange={(v) => setH({ ...h, location: v })} options={of('CITY', geoState)} />
                  : <Field label="City" value={h.location} placeholder="City" onChange={(v) => setH({ ...h, location: v })} />}
              </div>
              <div className="grid g3">
                <Field label="Primary contact email" value={h.email} onChange={(v) => setH({ ...h, email: v })} />
                <Field label="Contact name" value={h.contactName} onChange={(v) => setH({ ...h, contactName: v })} />
                <Field label="Contact phone" value={h.contactPhone} onChange={(v) => setH({ ...h, contactPhone: v })} />
              </div>
            </Section>
          )}

          {cur.k === 'banking' && (
            <Section title="Banking">
              <div className="grid g3">
                <Select label="Bank" value={bank.bank} placeholder="Select bank" onChange={(v) => setBank({ ...bank, bank: v })} options={of('BANK')} />
                <Field label="Account no." value={bank.accountNo} onChange={(v) => setBank({ ...bank, accountNo: v })} />
                <Field label="SWIFT" value={bank.swift} onChange={(v) => setBank({ ...bank, swift: v })} />
              </div>
            </Section>
          )}

          {cur.k === 'docs' && (
            <Section title="Documents">
              {DOCS.filter((d) => (d.req !== 'swec' || isSwec) && (d.req !== 'nonswec' || !isSwec)).map((d) => {
                const up = docs.find((x) => x.key === d.id)
                return (
                  <div className="doc" key={d.id}>
                    <div className="di"><Icon name="doc" size={15} /></div>
                    <div className="dn">{d.name} {d.req === 'all' && <span className="req">*</span>}</div>
                    {up ? (
                      <>
                        <span className="filepill"><Icon name="check" size={12} /> {up.fileName}</span>
                        <button type="button" className="btn btn-ghost btn-sm" onClick={() => { void deleteOnboardingDocument(token, d.id).then(() => setDocs((xs) => xs.filter((x) => x.key !== d.id))) }}><Icon name="x" size={13} /></button>
                      </>
                    ) : (
                      <label className="btn btn-out btn-sm" style={{ cursor: 'pointer' }}>
                        <Icon name="upload" size={13} /> Upload
                        <input type="file" style={{ display: 'none' }} aria-label={`Upload ${d.name}`}
                          onChange={(e) => {
                            const f = e.target.files?.[0]; e.currentTarget.value = ''
                            if (!f) return
                            void uploadOnboardingDocument(token, d.id, f).then((doc) => setDocs((xs) => [...xs.filter((x) => x.key !== d.id), doc]))
                          }} />
                      </label>
                    )}
                  </div>
                )
              })}
            </Section>
          )}

          {cur.k === 'cats' && (
            <Section title="SWEC categories">
              <p className="hint" style={{ marginTop: 0 }}>Select the PETRONAS SWEC categories you’re registered for. These are matched against buyers’ RFQ categories.</p>
              <div className="catwrap">
                {cats.length === 0
                  ? <span className="hint">None selected yet.</span>
                  : cats.map((c) => (
                    <span key={c} className="swchip" title={swec?.path(c)} style={{ cursor: 'pointer' }}
                      onClick={() => setCats((xs) => xs.filter((x) => x !== c))}>{swec?.label(c) ?? c} ✕</span>
                  ))}
              </div>
              <button type="button" className="btn btn-out btn-sm" style={{ marginTop: 12 }} onClick={() => setPicking(true)}>
                <Icon name="edit" size={13} /> Select categories
              </button>
            </Section>
          )}

          {cur.k === 'fin' && !isSwec && (
            <>
              <Notice tone="warn" icon="flag">Non-SWEC supplier — enter 3 years of figures (RM’000). An Altman Z-score is computed automatically as you type.</Notice>
              <div className="card" style={{ marginBottom: 14 }}>
                <div className="cbody">
                  <div className="grid g3">
                    <Stat label="Altman Z · weighted" value={isFinite(fc.zW) ? fc.zW.toFixed(2) : '—'} sub={fc.bd.zone} cls={fc.bd.cls} />
                    <Stat label="Score" value={isFinite(fc.score) ? `${fc.score} / 100` : '—'} />
                    <Stat label="Band → Risk" value={`Band ${fc.bd.band}`} sub={`${fc.bd.risk} risk`} cls={fc.bd.cls} />
                  </div>
                </div>
              </div>
              <Section title="Financial data (RM’000)">
                <table className="comp">
                  <thead><tr><th>Item</th><th className="amt">FY-2</th><th className="amt">FY-1</th><th className="amt">Current</th></tr></thead>
                  <tbody>
                    {FIN_ITEMS.map(([label, k]) => (
                      <tr key={k}>
                        <td style={{ fontWeight: 600 }}>{label}</td>
                        {[0, 1, 2].map((i) => (
                          <td key={i}>
                            <input type="number" className="amt" aria-label={`${label} year ${i + 1}`} value={fin[k][i] || ''}
                              onChange={(e) => setFin((prev) => {
                                const n = Number(e.target.value) || 0
                                if (Math.abs(n) >= 1e12) return prev            // figures are RM'000 — reject an out-of-range entry
                                const nx = { ...prev, [k]: [...prev[k]] as [number, number, number] }
                                nx[k][i] = n
                                return nx
                              })} />
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </Section>
            </>
          )}

          {cur.k.startsWith('pack:') && (() => {
            const pack = draft.packs.find((p) => `pack:${p.id}` === cur.k)!
            return (
              <Section title={pack.name}>
                {pack.items.filter((i) => i.kind === 'question').map((it) => (
                  <div className="q" key={it.order}>
                    <div className="qt">{it.label}{it.required && <span className="req"> *</span>}</div>
                    <AnswerInput type={it.type} config={it.config}
                      value={answers[answerKey(pack.id, it.order)] ?? ''}
                      onChange={(v) => setAnswers((a) => ({ ...a, [answerKey(pack.id, it.order)]: v }))} />
                  </div>
                ))}
              </Section>
            )
          })()}

          {cur.k === 'review' && (
            <>
              {missing.length
                ? <Notice tone="warn" icon="flag">Still needed: {missing.join(', ')}</Notice>
                : <Notice tone="success" icon="check">Looks complete. Submit to route to SPSB procurement.</Notice>}
              <Section title="Summary">
                <div className="kv"><span className="k">Vendor</span><span className="v">{h.name || '—'}</span></div>
                <div className="kv"><span className="k">Type</span><span className="v">{draft.type}</span></div>
                <div className="kv"><span className="k">Documents attached</span><span className="v">{docs.length}</span></div>
                {!isSwec && <div className="kv"><span className="k">Financial band</span><span className="v">Band {fc.bd.band}</span></div>}
              </Section>
            </>
          )}

          {/* Non-blocking amber warning on the current section (progress still saves). */}
          {cur.k !== 'review' && !stepComplete(cur.k) && (
            <Notice tone="warn" icon="flag" style={{ marginTop: 14 }}>
              This section still needs: {stepMissing(cur.k).join(', ')}. You can continue and finish it later — it’s required to submit.
            </Notice>
          )}
          {/* On submit, enforce: list the sections still incomplete. */}
          {stepErr && cur.k === 'review' && (
            <Notice tone="warn" icon="flag" style={{ marginTop: 14 }}>Complete these sections before submitting: {stepErr.join(', ')}.</Notice>
          )}

          <div className="actionbar">
            <button type="button" className="btn btn-out" disabled={step === 0} onClick={() => goto(step - 1)}>Back</button>
            <div className="spacer" style={{ flex: 1 }} />
            {step < steps.length - 1
              ? <button type="button" className="btn btn-pri" onClick={next}>Next <Icon name="chev" size={14} /></button>
              : <button type="button" className="btn btn-pri" disabled={submit.isPending} onClick={trySubmit}><Icon name="check" size={15} /> Submit application</button>}
          </div>
        </div>
      </div>

      {picking && (
        <SwecPicker initial={cats} vendorName={h.name || 'your company'}
          onCancel={() => setPicking(false)} onSave={(codes) => { setCats(codes); setPicking(false) }} />
      )}
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <div className="card" style={{ marginBottom: 14 }}><div className="chead"><h3>{title}</h3></div><div className="cbody">{children}</div></div>
}
function Field({ label, value, onChange, placeholder, required }: { label: string; value: string; onChange: (v: string) => void; placeholder?: string; required?: boolean }) {
  return (
    <div className="field">
      <label>{label} {required && <span className="req">*</span>}</label>
      <input value={value} placeholder={placeholder} aria-label={label} onChange={(e) => onChange(e.target.value)} />
    </div>
  )
}
function Select({ label, value, onChange, options, placeholder }: { label: string; value: string; onChange: (v: string) => void; options: { code: string; label: string }[]; placeholder?: string }) {
  return (
    <div className="field">
      <label>{label}</label>
      <select value={value} aria-label={label} onChange={(e) => onChange(e.target.value)}>
        {placeholder && <option value="">{placeholder}</option>}
        {options.map((o) => <option key={o.code} value={o.code}>{o.label}</option>)}
      </select>
    </div>
  )
}
function Stat({ label, value, sub, cls }: { label: string; value: string; sub?: string; cls?: string }) {
  return (
    <div className="card stat" style={{ margin: 0 }}>
      <div className="lbl">{label}</div>
      <div className="num">{value}</div>
      {sub && (cls ? <div className="sub"><span className={`badge ${cls}`}>{sub}</span></div> : <div className="sub">{sub}</div>)}
    </div>
  )
}

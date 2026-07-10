import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getRfq, getMyBid, saveBidDraft, submitBid, uploadFile, fileUrl, declineRfqInvitation, intendRfq, withdrawBid } from '../../api/client'
import { Icon } from '../Icon'
import { Notice, Spinner } from '../ui'
import { fmt, fmtDay } from '../../lib/format'
import { parseConfig } from '../../lib/formTypes'
import { useIdentity } from '../../identity'
import { ReasonModal, useReasonLabeler } from '../sourcing/RfqGovernance'

interface LineState { bidding: boolean; price: number; qty: number; altItem: string }

// Exported so the vendor onboarding form (Slice C) renders question packs with the SAME renderer
// as the RFQ questionnaire (D3). Standalone: pass type + config (JSON) + value + onChange.
export function AnswerInput({ type, config, value, onChange }: { type: string; config: string; value: string; onChange: (v: string) => void }) {
  const cfg = parseConfig(config)
  if (type === 'long_text') return <textarea rows={3} value={value} onChange={(e) => onChange(e.target.value)} />
  if (type === 'number' || type === 'money' || type === 'percent')
    return <input type="number" value={value} onChange={(e) => onChange(e.target.value)} placeholder={cfg.unit ?? ''} />
  if (type === 'date') return <input type="date" value={value} onChange={(e) => onChange(e.target.value)} />
  if (type === 'yesno')
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">—</option>
        <option value="Yes">Yes</option>
        <option value="No">No</option>
      </select>
    )
  if (type === 'list')
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">—</option>
        {(cfg.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
      </select>
    )
  if (type === 'multi') {
    const selected = value ? value.split('|') : []
    const toggle = (o: string) =>
      onChange((selected.includes(o) ? selected.filter((x) => x !== o) : [...selected, o]).join('|'))
    return (
      <div className="ckcol">
        {(cfg.options ?? []).map((o) => (
          <label className="ck" key={o}>
            <input type="checkbox" checked={selected.includes(o)} onChange={() => toggle(o)} /> {o}
          </label>
        ))}
      </div>
    )
  }
  if (type === 'attachment') {
    // Each entry is "<fileId>::<name>" so the evaluator can download it later.
    const files = value ? value.split('|').filter(Boolean) : []
    const add = async (list: FileList | null) => {
      const picked = Array.from(list ?? [])
      if (!picked.length) return
      const uploaded: string[] = []
      for (const f of picked) {
        try { const r = await uploadFile(f); uploaded.push(`${r.id}::${r.name}`) } catch { /* skip failed upload */ }
      }
      if (!uploaded.length) return
      onChange((cfg.multiple ? [...files, ...uploaded] : uploaded.slice(0, 1)).join('|'))
    }
    return (
      <div>
        <label className="btn btn-out btn-sm" style={{ cursor: 'pointer', display: 'inline-flex' }}>
          <Icon name="upload" size={13} /> Choose file{cfg.multiple ? 's' : ''}
          <input type="file" multiple={cfg.multiple ?? false} style={{ display: 'none' }} onChange={(e) => { void add(e.target.files); e.currentTarget.value = '' }} />
        </label>
        <div style={{ marginTop: 6 }}>
          {files.length === 0 && <span className="hint">No file chosen.</span>}
          {files.map((entry, i) => {
            const [id, name] = entry.includes('::') ? entry.split('::') : ['', entry]
            return (
              <span className="file" key={i} style={{ display: 'inline-flex', width: 'auto', marginRight: 8 }}>
                <span className="ext">{(name.split('.').pop() ?? 'FILE').toUpperCase()}</span>
                {id ? <a href={fileUrl(id)} target="_blank" rel="noreferrer">{name}</a> : name}
                <button type="button" className="lnk" onClick={() => onChange(files.filter((_, x) => x !== i).join('|'))}><Icon name="x" size={12} /></button>
              </span>
            )
          })}
        </div>
      </div>
    )
  }
  if (type === 'table') return <TableAnswer cfg={cfg} value={value} onChange={onChange} />
  if (type === 'group') return <GroupAnswer cfg={cfg} value={value} onChange={onChange} />
  return <input type="text" value={value} onChange={(e) => onChange(e.target.value)} />
}

const safeParse = (v: string): string[][] => { try { const p = JSON.parse(v || '[]'); return Array.isArray(p) ? p : [] } catch { return [] } }

// Lets a map return two <tr> rows under one key.
function FragmentRow({ children }: { children: React.ReactNode }) { return <>{children}</> }

// Table / matrix: a rows × columns grid; serialised to JSON in the answer value.
function TableAnswer({ cfg, value, onChange }: { cfg: ReturnType<typeof parseConfig>; value: string; onChange: (v: string) => void }) {
  const cols = cfg.columns ?? []
  const rows = cfg.rows ?? []
  const data = safeParse(value)
  const cell = (ri: number, ci: number) => data[ri]?.[ci] ?? ''
  const setCell = (ri: number, ci: number, v: string) =>
    onChange(JSON.stringify(rows.map((_, r) => cols.map((_, c) => (r === ri && c === ci ? v : cell(r, c))))))
  const inputType = cfg.cellType === 'number' || cfg.cellType === 'money' ? 'number' : 'text'
  return (
    <div style={{ overflowX: 'auto' }}>
      <table>
        <thead><tr><th /> {cols.map((c, ci) => <th key={ci} className="amt">{c}</th>)}</tr></thead>
        <tbody>
          {rows.map((r, ri) => (
            <tr key={ri}>
              <td style={{ fontWeight: 600 }}>{r}</td>
              {cols.map((_, ci) => (
                <td key={ci} className="amt"><input type={inputType} value={cell(ri, ci)} onChange={(e) => setCell(ri, ci, e.target.value)} aria-label={`${r} ${cols[ci]}`} /></td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// Repeatable group: up to `max` entries, each with the configured fields; JSON-serialised.
function GroupAnswer({ cfg, value, onChange }: { cfg: ReturnType<typeof parseConfig>; value: string; onChange: (v: string) => void }) {
  const fields = cfg.fields ?? []
  const max = cfg.max ?? 3
  const entries = safeParse(value)
  const commit = (next: string[][]) => onChange(JSON.stringify(next))
  const setField = (ei: number, fi: number, v: string) =>
    commit(entries.map((e, i) => (i === ei ? fields.map((_, f) => (f === fi ? v : (e[f] ?? ''))) : e)))
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {entries.map((entry, ei) => (
        <div key={ei} className="card" style={{ padding: 0 }}>
          <div className="chead" style={{ padding: '8px 12px' }}>
            <span className="hint" style={{ fontWeight: 700 }}>Entry {ei + 1}</span>
            <div className="spacer" />
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => commit(entries.filter((_, i) => i !== ei))} aria-label={`Remove entry ${ei + 1}`}><Icon name="x" size={13} /></button>
          </div>
          <div className="cbody" style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
            {fields.map((f, fi) => (
              <div className="field" key={fi} style={{ margin: 0 }}>
                <label>{f.label}</label>
                <input type={f.type === 'number' || f.type === 'money' ? 'number' : f.type === 'date' ? 'date' : 'text'} value={entry[fi] ?? ''} onChange={(e) => setField(ei, fi, e.target.value)} />
              </div>
            ))}
          </div>
        </div>
      ))}
      {entries.length < max && (
        <button type="button" className="btn btn-out btn-sm" style={{ alignSelf: 'flex-start' }} onClick={() => commit([...entries, fields.map(() => '')])}><Icon name="plus" size={13} /> Add entry ({entries.length}/{max})</button>
      )}
    </div>
  )
}

export function BidForm({ rfqId, onBack }: { rfqId: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { persona } = useIdentity()
  const reasonLabel = useReasonLabeler()
  const { data: rfq, isPending } = useQuery({ queryKey: ['rfq', rfqId], queryFn: () => getRfq(rfqId) })
  const { data: bid } = useQuery({ queryKey: ['my-bid', rfqId], queryFn: () => getMyBid(rfqId) })

  const [lines, setLines] = useState<Record<string, LineState> | null>(null)
  const [answers, setAnswers] = useState<Record<number, string> | null>(null)
  const [lead, setLead] = useState<number | null>(null)
  const [warranty, setWarranty] = useState<number | null>(null)
  const [notice, setNotice] = useState('')
  const [missing, setMissing] = useState<Set<number>>(new Set())

  const init = useMemo(() => {
    if (!rfq) return null
    const ls: Record<string, LineState> = {}
    for (const l of rfq.lines ?? []) {
      const bl = bid?.lines?.find((x) => x.itemCode === l.itemCode)
      ls[l.itemCode!] = { bidding: bl?.bidding ?? true, price: bl?.price ?? 0, qty: bl?.qty ?? (l.qty ?? 0), altItem: bl?.altItem ?? '' }
    }
    const ans: Record<number, string> = {}
    for (const a of bid?.answers ?? []) if (a.questionOrder != null) ans[a.questionOrder] = a.value ?? ''
    return { ls, ans, lead: bid?.lead ?? 0, warranty: bid?.warranty ?? 0 }
  }, [rfq, bid])

  const buildRequest = () => {
    const ls = lines ?? init!.ls
    const ans = answers ?? init!.ans
    const qs = (rfq!.formItems ?? []).filter((i) => i.kind === 'question')
    return {
      lead: lead ?? init!.lead,
      warranty: warranty ?? init!.warranty,
      lines: (rfq!.lines ?? []).map((l) => ({
        itemCode: l.itemCode!,
        bidding: ls[l.itemCode!].bidding,
        price: Number(ls[l.itemCode!].price) || 0,
        qty: Number(ls[l.itemCode!].qty) || 0,
        partial: (Number(ls[l.itemCode!].qty) || 0) < (l.qty ?? 0),
        altItem: ls[l.itemCode!].altItem.trim() || null,
      })),
      answers: qs.map((q) => ({ questionOrder: q.order!, value: ans[q.order!] ?? '' })),
      files: bid?.files ?? [],
    }
  }

  const save = useMutation({
    mutationFn: () => saveBidDraft(rfqId, buildRequest()),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['my-bid', rfqId] }); setNotice('Draft saved — submit before the RFQ closes.') },
    onError: (e: Error) => setNotice(e.message),
  })
  const submit = useMutation({
    mutationFn: () => submitBid(rfqId, buildRequest()),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['my-bid', rfqId] })
      void qc.invalidateQueries({ queryKey: ['my-invitations'] })
      setNotice('SUBMITTED')
    },
    onError: (e: Error) => setNotice(e.message),
  })

  // ---- Invitation lifecycle (Slice J): decline / intend / withdraw. Viewing the page already fired
  // the mark-viewed side effect via getRfq above (server-side, vendor-scoped, fire-and-forget). ----
  const invalRfq = () => qc.invalidateQueries({ queryKey: ['rfq', rfqId] })
  const [showDecline, setShowDecline] = useState(false)
  const decline = useMutation({
    mutationFn: (v: { code: string; note: string | null }) => declineRfqInvitation(rfqId, v.code, v.note),
    onSuccess: () => { void invalRfq(); setShowDecline(false) },
  })
  const intend = useMutation({ mutationFn: () => intendRfq(rfqId), onSuccess: () => void invalRfq() })
  const withdraw = useMutation({
    mutationFn: () => withdrawBid(rfqId),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['my-bid', rfqId] }); void invalRfq(); setNotice('WITHDRAWN') },
    onError: (e: Error) => setNotice(e.message),
  })

  if (isPending || !rfq || !init) return <Spinner label="Loading bid…" />

  const L = lines ?? init.ls
  const A = answers ?? init.ans
  const isOpen = rfq.status === 'Open'
  const myInv = (rfq.invitations ?? []).find((i) => i.vendorId === persona?.vendorId)
  const declined = myInv?.status === 'Declined'
  const intending = myInv?.status === 'IntendToBid'
  const submitted = !!bid?.submitted

  const setLine = (code: string, patch: Partial<LineState>) => setLines({ ...L, [code]: { ...L[code], ...patch } })
  const setAns = (order: number, v: string) => setAnswers({ ...A, [order]: v })

  // Is a required answer actually complete? Per-type, mirroring the prototype's vbValidate
  // — e.g. a table needs every cell filled, a group every field of every entry.
  const isAnswered = (it: NonNullable<typeof rfq.formItems>[number], value: string) => {
    const v = value ?? ''
    const cfg = parseConfig(it.config)
    switch (it.type) {
      case 'multi':
      case 'attachment':
        return v.split('|').filter(Boolean).length > 0
      case 'table': {
        const cols = cfg.columns ?? []; const rows = cfg.rows ?? []
        if (rows.length === 0 || cols.length === 0) return v.trim() !== ''
        const data = safeParse(v)
        return rows.every((_, ri) => cols.every((_, ci) => (data[ri]?.[ci] ?? '').toString().trim() !== ''))
      }
      case 'group': {
        const fields = cfg.fields ?? []
        const entries = safeParse(v)
        return entries.length > 0 && entries.every((e) => fields.every((_, fi) => (e[fi] ?? '').toString().trim() !== ''))
      }
      default:
        return v.trim() !== ''
    }
  }

  // Client-side gate mirroring vbValidate: at least one priced line + all required answers.
  const handleSubmit = () => {
    const anyPriced = (rfq.lines ?? []).some((l) => L[l.itemCode!].bidding && Number(L[l.itemCode!].price) > 0)
    if (!anyPriced) { setNotice('Enter a price on at least one line'); return }
    const miss = new Set<number>()
    for (const it of rfq.formItems ?? []) {
      if (it.kind !== 'question' || !it.required || it.order == null) continue
      if (!isAnswered(it, A[it.order] ?? '')) miss.add(it.order)
    }
    setMissing(miss)
    if (miss.size > 0) { setNotice(`${miss.size} required answer${miss.size > 1 ? 's' : ''} still needed`); return }
    submit.mutate()
  }

  if (notice === 'SUBMITTED')
    return (
      <div className="card" style={{ maxWidth: 560, margin: '40px auto' }}>
        <div className="cbody" style={{ textAlign: 'center' }}>
          <div className="health health-ok" style={{ marginBottom: 12 }}><span className="dot" /> Bid Submitted</div>
          <p>Your bid for <strong>{rfq.code}</strong> has been submitted{rfq.envelope === 'Dual' ? ' — technical & commercial sealed separately' : ''}.</p>
          <button type="button" className="btn btn-pri" onClick={onBack}>Back to my RFQs</button>
        </div>
      </div>
    )

  return (
    <>
      <div className="crumb">
        <a onClick={onBack}>My RFQs</a> <Icon name="chev" size={13} /> <span>{rfq.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>{rfq.title || rfq.code}</h1>
          <p>{rfq.code} · {rfq.envelope} envelope · closes {fmtDay(rfq.closesUtc)}</p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          {/* Pre-bid intent + decline (Open, not yet submitted, not declined) */}
          {isOpen && !submitted && !declined && (intending
            ? <span className="badge b-teal" title="You've told the buyer you intend to bid"><Icon name="check" size={12} /> Intending to bid</span>
            : <button type="button" className="btn btn-ghost" disabled={intend.isPending} onClick={() => intend.mutate()}><Icon name="check" size={15} /> I intend to bid</button>)}
          {isOpen && !submitted && !declined && (
            <button type="button" className="btn btn-ghost" style={{ color: 'var(--red)' }} onClick={() => setShowDecline(true)}>Decline invitation</button>)}
          {/* Withdraw a submitted bid (Open, before close) */}
          {isOpen && submitted && (
            <button type="button" className="btn btn-out" style={{ color: 'var(--red)' }} disabled={withdraw.isPending} onClick={() => withdraw.mutate()}><Icon name="x" size={15} /> Withdraw bid</button>)}
          <button type="button" className="btn btn-out" disabled={!isOpen || declined} onClick={() => save.mutate()}>
            <Icon name="doc" size={15} /> Save draft
          </button>
          <button type="button" className="btn btn-pri" disabled={!isOpen || declined} onClick={handleSubmit}>
            <Icon name="send" size={15} /> {submitted ? 'Resubmit bid' : 'Submit bid'}
          </button>
        </div>
      </div>

      {showDecline && (
        <ReasonModal
          title="Decline invitation" icon="flag" listCode="RFQ_DECLINE_REASON" confirmLabel="Decline" confirmIcon="x"
          busy={decline.isPending} error={decline.error ? (decline.error as Error).message : null}
          onCancel={() => { setShowDecline(false); decline.reset() }}
          onSubmit={(code, note) => decline.mutate({ code, note })}
        />
      )}

      {!isOpen && <Notice tone="error" icon="lock">Bidding is closed for this RFQ — submissions are no longer accepted.</Notice>}
      {declined && isOpen && (
        <Notice tone="warn" icon="flag">
          You declined this RFQ{myInv?.declineReasonCode ? ` — ${reasonLabel(myInv.declineReasonCode)}` : ''}.{' '}
          <button type="button" className="lnk" disabled={intend.isPending} onClick={() => intend.mutate()}>Reconsider — I intend to bid</button>
        </Notice>
      )}
      {notice === 'WITHDRAWN' && <Notice tone="warn" icon="flag">Bid withdrawn — resubmit before {fmtDay(rfq.closesUtc)} if you still want to bid.</Notice>}
      {notice && notice !== 'SUBMITTED' && notice !== 'WITHDRAWN' && <Notice>{notice}</Notice>}

      <div className="card" style={{ marginBottom: 16 }}>
        <div className="chead"><h3>Line pricing</h3><div className="spacer" /><span className="hint">{rfq.currency}</span></div>
        <table>
          <thead>
            <tr><th style={{ width: 36 }}>Bid</th><th>Item</th><th>Description</th><th className="amt">Required</th><th>UoM</th><th className="amt">Offered qty</th><th className="amt">Unit price</th><th className="amt">Line total</th></tr>
          </thead>
          <tbody>
            {(rfq.lines ?? []).map((l) => {
              const s = L[l.itemCode!]
              const required = l.qty ?? 0
              const partial = s.bidding && s.qty < required
              return (
                <FragmentRow key={l.itemCode}>
                  <tr>
                    <td style={{ textAlign: 'center' }}>
                      <input type="checkbox" style={{ width: 'auto' }} checked={s.bidding} disabled={!isOpen} onChange={(e) => setLine(l.itemCode!, { bidding: e.target.checked })} aria-label={`Bid ${l.itemCode}`} />
                    </td>
                    <td>{l.itemCode}</td>
                    <td>{l.description}</td>
                    <td className="amt">{required}</td>
                    <td>{l.uom}</td>
                    <td className="amt" style={{ width: 96 }}>
                      <input type="number" min={0} max={required} value={s.qty || ''} disabled={!isOpen || !s.bidding} onChange={(e) => setLine(l.itemCode!, { qty: Number(e.target.value) })} aria-label={`Qty ${l.itemCode}`} />
                    </td>
                    <td className="amt" style={{ width: 130 }}>
                      <input type="number" value={s.price || ''} disabled={!isOpen || !s.bidding} onChange={(e) => setLine(l.itemCode!, { price: Number(e.target.value) })} aria-label={`Price ${l.itemCode}`} />
                    </td>
                    <td className="amt" style={{ fontWeight: 700 }}>{s.bidding && s.price ? `RM ${fmt(s.price * (s.qty || 0))}` : '—'}</td>
                  </tr>
                  {s.bidding && (
                    <tr>
                      <td />
                      <td colSpan={7} style={{ background: '#fbfbf9' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                          {partial && <span className="badge b-amber">Partial · {required - s.qty} {l.uom} short</span>}
                          <span className="hint">Alternate item (optional):</span>
                          <input type="text" value={s.altItem} disabled={!isOpen} placeholder="propose an equivalent / substitute to fulfil this line" onChange={(e) => setLine(l.itemCode!, { altItem: e.target.value })} aria-label={`Alternate item ${l.itemCode}`} style={{ flex: 1, minWidth: 280, maxWidth: 420 }} />
                        </div>
                      </td>
                    </tr>
                  )}
                </FragmentRow>
              )
            })}
          </tbody>
        </table>
      </div>

      <div className="grid g2" style={{ marginBottom: 16 }}>
        <div className="card"><div className="cbody"><div className="field"><label>Delivery lead time (weeks)</label><input type="number" value={(lead ?? init.lead) || ''} disabled={!isOpen} onChange={(e) => setLead(Number(e.target.value))} /></div></div></div>
        <div className="card"><div className="cbody"><div className="field"><label>Warranty (months)</label><input type="number" value={(warranty ?? init.warranty) || ''} disabled={!isOpen} onChange={(e) => setWarranty(Number(e.target.value))} /></div></div></div>
      </div>

      {(['technical', 'commercial'] as const).map((group) => {
        const items = (rfq.formItems ?? []).filter((i) => i.group === group)
        if (items.length === 0) return null
        return (
          <div className="card" style={{ marginBottom: 16 }} key={group}>
            <div className="chead"><h3>{group === 'technical' ? 'Technical responses' : 'Commercial responses'}</h3></div>
            <div className="cbody">
              {items.map((it) => {
                if (it.kind !== 'question')
                  return <p key={it.order} className="hint" style={{ borderLeft: '3px solid var(--line)', paddingLeft: 10, margin: '10px 0' }}>{it.label}</p>
                const isMissing = missing.has(it.order!)
                return (
                  <div className="field" key={it.order} style={isMissing ? { borderLeft: '3px solid var(--red)', paddingLeft: 10 } : undefined}>
                    <label>{it.label}{it.required ? ' *' : ''}{isMissing && <span style={{ color: 'var(--red)', marginLeft: 6, fontWeight: 600 }}>required</span>}</label>
                    <AnswerInput type={it.type ?? 'short_text'} config={it.config ?? '{}'} value={A[it.order!] ?? ''} onChange={(v) => { setAns(it.order!, v); if (isMissing) setMissing((m) => { const n = new Set(m); n.delete(it.order!); return n }) }} />
                  </div>
                )
              })}
            </div>
          </div>
        )
      })}
    </>
  )
}

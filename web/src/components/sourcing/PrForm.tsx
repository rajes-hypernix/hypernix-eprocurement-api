import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRequisition, createPr, updatePr, cancelPr, submitPr,
  type RequisitionDto, type SavePrRequest,
} from '../../api/client'
import { Icon } from '../Icon'
import { Modal, Notice, Spinner } from '../ui'
import { PrHeaderBadge } from '../../lib/prStatus'

type PrLine = NonNullable<RequisitionDto['lines']>[number]
// A line being edited: carries the server id (existing) or undefined (new).
type EditLine = { id?: string; itemCode: string; description: string; qty: string; uom: string; estUnitPrice: string; lifecycleStatus: string; editable: boolean }

const toEdit = (l: PrLine): EditLine => ({
  id: l.id ?? undefined, itemCode: l.itemCode ?? '', description: l.description ?? '',
  qty: String(l.qty ?? ''), uom: l.uom ?? '', estUnitPrice: String(l.estUnitPrice ?? ''),
  lifecycleStatus: l.lifecycleStatus ?? 'Open', editable: l.editable ?? true,
})
const blankLine = (): EditLine => ({ itemCode: '', description: '', qty: '', uom: '', estUnitPrice: '', lifecycleStatus: 'Open', editable: true })

export function PrForm({ id, onBack }: { id: string | null; onBack: () => void }) {
  const qc = useQueryClient()
  const isNew = id === null
  const { data: pr, isPending } = useQuery({ queryKey: ['requisition', id], queryFn: () => getRequisition(id!), enabled: !isNew })

  const [h, setH] = useState({ requestor: '', department: '', category: '', location: '', job: '', requiredDate: '', memo: '' })
  const [lines, setLines] = useState<EditLine[]>([blankLine()])
  const [err, setErr] = useState<string | null>(null)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [cancelReason, setCancelReason] = useState('')

  // Hydrate from the loaded PR once (guard in-progress edits from a background refetch).
  const [hydrated, setHydrated] = useState(false)
  useEffect(() => {
    if (pr && !hydrated) {
      setH({
        requestor: pr.requestor ?? '', department: pr.department ?? '', category: pr.category ?? '',
        location: pr.location ?? '', job: pr.job ?? '', requiredDate: (pr.requiredDate ?? '').slice(0, 10), memo: pr.memo ?? '',
      })
      setLines((pr.lines ?? []).map(toEdit))
      setHydrated(true)
    }
  }, [pr, hydrated])

  const total = lines.reduce((s, l) => s + (Number(l.qty) || 0) * (Number(l.estUnitPrice) || 0), 0)
  const hasLiveLine = lines.some((l) => l.lifecycleStatus === 'InRfq' || l.lifecycleStatus === 'Awarded')
  const headerStatus = pr?.headerStatus

  const body = (): SavePrRequest => ({
    requestor: h.requestor, department: h.department, location: h.location, category: h.category,
    job: h.job, memo: h.memo, requiredDate: h.requiredDate || null,
    lines: lines.filter((l) => l.itemCode.trim() || l.description.trim()).map((l) => ({
      id: l.id ?? null, itemCode: l.itemCode, description: l.description,
      qty: Number(l.qty) || 0, uom: l.uom || 'Unit', estUnitPrice: Number(l.estUnitPrice) || 0,
    })),
  })

  const inval = () => { void qc.invalidateQueries({ queryKey: ['requisitions'] }); if (id) void qc.invalidateQueries({ queryKey: ['requisition', id] }) }
  const save = useMutation({
    mutationFn: (mode: 'draft' | 'submit' | 'keep') =>
      isNew ? createPr(body(), mode === 'submit') : updatePr(id!, body()),
    onSuccess: () => { inval(); onBack() },
    onError: (e: Error) => setErr(e.message),
  })
  const cancel = useMutation({
    mutationFn: () => cancelPr(id!, cancelReason),
    onSuccess: () => { inval(); setConfirmCancel(false); onBack() },
    onError: (e: Error) => { setErr(e.message); setConfirmCancel(false) },
  })
  // Draft → Submitted (Bug 3). Saves in-progress header edits first so nothing is lost.
  const submit = useMutation({
    mutationFn: async () => { await updatePr(id!, body()); return submitPr(id!) },
    onSuccess: () => { inval(); onBack() },
    onError: (e: Error) => setErr(e.message),
  })

  if (!isNew && isPending) return <Spinner />

  const setLine = (i: number, k: keyof EditLine, v: string) =>
    setLines((ls) => ls.map((l, x) => (x === i ? { ...l, [k]: v } : l)))
  const removeLine = (i: number) => setLines((ls) => ls.filter((_, x) => x !== i))
  const addLine = () => setLines((ls) => [...ls, blankLine()])

  // One action row, rendered at the top AND bottom of the form (kept in one place so they can't
  // drift). Long forms are easier to use when the primary actions are reachable without scrolling.
  const actions = (
    <div className="pr-actions">
      {isNew ? (
        <>
          <button type="button" className="btn btn-ghost" disabled={save.isPending} onClick={() => save.mutate('draft')}>Save draft</button>
          <button type="button" className="btn btn-pri" disabled={save.isPending} onClick={() => save.mutate('submit')}><Icon name="check" size={15} /> Submit PR</button>
        </>
      ) : headerStatus === 'Draft' ? (
        <>
          <button type="button" className="btn btn-out" disabled={save.isPending || submit.isPending} onClick={() => save.mutate('keep')}>Save changes</button>
          <button type="button" className="btn btn-pri" disabled={submit.isPending || save.isPending} onClick={() => submit.mutate()}><Icon name="check" size={15} /> Submit PR</button>
        </>
      ) : (
        <button type="button" className="btn btn-pri" disabled={save.isPending} onClick={() => save.mutate('keep')}><Icon name="check" size={15} /> Save changes</button>
      )}
      <div className="spacer" />
      {!isNew && !hasLiveLine && headerStatus !== 'Cancelled' && (
        <button type="button" className="btn btn-danger" onClick={() => setConfirmCancel(true)}>Cancel PR</button>
      )}
    </div>
  )

  return (
    <>
      <div className="crumb"><button type="button" className="lnk" onClick={onBack}>Requisitions</button> <Icon name="chev" size={13} /> <span>{isNew ? 'New PR' : pr?.code}</span></div>
      <div className="pagehead">
        <div>
          <h1>{isNew ? 'Create Purchase Requisition' : `Edit ${pr?.code}`}</h1>
          <p>{isNew ? 'Raise a purchase requisition directly in the portal.' : 'Header and open lines are editable. Sourced / awarded lines are locked.'}</p>
        </div>
        <div className="spacer" />
        {!isNew && <PrHeaderBadge status={headerStatus} />}
      </div>

      {err && <Notice tone="error" icon="x" style={{ marginBottom: 14 }}>Could not save: {err}</Notice>}

      {actions}

      <div className="card" style={{ padding: 18, marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>Header</h3>
        <div className="frow">
          <Field label="Requestor" value={h.requestor} onChange={(v) => setH({ ...h, requestor: v })} placeholder="Name" />
          <Field label="Department" value={h.department} onChange={(v) => setH({ ...h, department: v })} placeholder="e.g. Maintenance" />
          <Field label="Category" value={h.category} onChange={(v) => setH({ ...h, category: v })} placeholder="e.g. Piping" />
        </div>
        <div className="frow">
          <Field label="Location" value={h.location} onChange={(v) => setH({ ...h, location: v })} placeholder="e.g. Bintulu Plant" />
          <Field label="Job / Cost ref" value={h.job} onChange={(v) => setH({ ...h, job: v })} placeholder="JOB-…" />
          <Field label="Required by" type="date" value={h.requiredDate} onChange={(v) => setH({ ...h, requiredDate: v })} />
        </div>
        <Field label="Memo / Justification" value={h.memo} onChange={(v) => setH({ ...h, memo: v })} placeholder="Short description of the requirement" />
      </div>

      <div className="card" style={{ padding: 18, marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>Lines</h3>
        <table>
          <thead><tr><th style={{ width: '18%' }}>Item code</th><th>Description</th><th className="amt" style={{ width: '10%' }}>Qty</th><th style={{ width: '10%' }}>UoM</th><th className="amt" style={{ width: '12%' }}>Est. rate</th><th style={{ width: '14%' }} /></tr></thead>
          <tbody>
            {lines.map((l, i) => {
              const ro = !isNew && !l.editable
              return (
                <tr key={l.id ?? `new-${i}`}>
                  <td><input className={ro ? 'ro' : ''} readOnly={ro} value={l.itemCode} placeholder="ITEM-CODE" onChange={(e) => setLine(i, 'itemCode', e.target.value)} aria-label={`Line ${i + 1} item code`} /></td>
                  <td><input className={ro ? 'ro' : ''} readOnly={ro} value={l.description} placeholder="Description" onChange={(e) => setLine(i, 'description', e.target.value)} aria-label={`Line ${i + 1} description`} /></td>
                  <td><input className={`amt ${ro ? 'ro' : ''}`} readOnly={ro} value={l.qty} placeholder="0" onChange={(e) => setLine(i, 'qty', e.target.value.replace(/[^0-9.]/g, ''))} aria-label={`Line ${i + 1} qty`} /></td>
                  <td><input className={ro ? 'ro' : ''} readOnly={ro} value={l.uom} placeholder="Unit" onChange={(e) => setLine(i, 'uom', e.target.value)} aria-label={`Line ${i + 1} uom`} /></td>
                  <td><input className={`amt ${ro ? 'ro' : ''}`} readOnly={ro} value={l.estUnitPrice} placeholder="0" onChange={(e) => setLine(i, 'estUnitPrice', e.target.value.replace(/[^0-9.]/g, ''))} aria-label={`Line ${i + 1} rate`} /></td>
                  <td>
                    {ro
                      ? <span className="lockchip"><Icon name="lock" size={12} /> {l.lifecycleStatus === 'InRfq' ? 'In RFQ' : l.lifecycleStatus}</span>
                      : <button type="button" className="btn btn-ghost btn-sm" style={{ color: 'var(--red)' }} onClick={() => removeLine(i)} disabled={lines.length === 1}>Remove</button>}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        <button type="button" className="btn btn-ghost btn-sm" style={{ marginTop: 10 }} onClick={addLine}><Icon name="plus" size={14} /> Add line</button>
        <div className="note" style={{ marginTop: 10 }}><Icon name="eye" size={13} /> Estimated total: <b>{total.toLocaleString()}</b></div>
      </div>

      {actions}

      {confirmCancel && (
        <Modal
          title={`Cancel ${pr?.code}?`} icon="x"
          footer={<>
            <button type="button" className="btn btn-out" onClick={() => setConfirmCancel(false)}>Keep PR</button>
            <button type="button" className="btn btn-pri btn-danger" disabled={cancel.isPending} onClick={() => cancel.mutate()}>Cancel PR</button>
          </>}
        >
          <p style={{ marginTop: 0 }}>This cancels the PR and all its open lines. Sourced/awarded lines block cancellation, so none are present.</p>
          <div className="field"><label>Reason</label>
            <textarea rows={3} value={cancelReason} placeholder="e.g. Duplicate of another PR / no longer required" onChange={(e) => setCancelReason(e.target.value)} />
          </div>
        </Modal>
      )}
    </>
  )
}

function Field({ label, value, onChange, placeholder, type = 'text' }: {
  label: string; value: string; onChange: (v: string) => void; placeholder?: string; type?: string
}) {
  return (
    <div className="field">
      <label>{label}</label>
      <input type={type} value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />
    </div>
  )
}

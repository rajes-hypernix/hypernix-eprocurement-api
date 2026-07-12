import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRequisition, createPr, updatePr, cancelPr, submitPr,
  type RequisitionDto, type SavePrRequest,
} from '../../api/client'
import { Icon } from '../Icon'
import { Modal, Spinner } from '../ui'
import { PrHeaderBadge } from '../../lib/prStatus'
import type { FieldSpec } from '../../ui/fieldSpec'
import { TextField } from '../../ui/TextField'
import { TextAreaField } from '../../ui/TextAreaField'
import { NumberField } from '../../ui/NumberField'
import { MoneyField } from '../../ui/MoneyField'
import { Button } from '../../ui/Button'
import { TransactionPage, type TransactionSection } from '../../ui/archetypes/TransactionPage'
import { CustomFieldsSection } from '../customfields/CustomFieldsSection'

type PrLine = NonNullable<RequisitionDto['lines']>[number]
// A line being edited: carries the server id (existing) or undefined (new).
type EditLine = { id?: string; itemCode: string; description: string; qty: string; uom: string; estUnitPrice: string; lifecycleStatus: string; editable: boolean }

const toEdit = (l: PrLine): EditLine => ({
  id: l.id ?? undefined, itemCode: l.itemCode ?? '', description: l.description ?? '',
  qty: String(l.qty ?? ''), uom: l.uom ?? '', estUnitPrice: String(l.estUnitPrice ?? ''),
  lifecycleStatus: l.lifecycleStatus ?? 'Open', editable: l.editable ?? true,
})
const blankLine = (): EditLine => ({ itemCode: '', description: '', qty: '', uom: '', estUnitPrice: '', lifecycleStatus: 'Open', editable: true })

// The header AS DATA: one TransactionSection consumed by the archetype.
type HeaderKey = 'requestor' | 'department' | 'category' | 'location' | 'job' | 'requiredDate' | 'memo'
const HEADER_SECTION: TransactionSection = {
  title: 'Header',
  rows: [
    [
      { key: 'requestor', label: 'Requestor', dataType: 'text', placeholder: 'Name' },
      { key: 'department', label: 'Department', dataType: 'text', placeholder: 'e.g. Maintenance' },
      { key: 'category', label: 'Category', dataType: 'text', placeholder: 'e.g. Piping' },
    ],
    [
      { key: 'location', label: 'Location', dataType: 'text', placeholder: 'e.g. Bintulu Plant' },
      { key: 'job', label: 'Job / Cost ref', dataType: 'text', placeholder: 'JOB-…' },
      { key: 'requiredDate', label: 'Required by', dataType: 'date' },
    ],
  ],
  fullWidth: [
    { key: 'memo', label: 'Memo / Justification', dataType: 'text', placeholder: 'Short description of the requirement' },
  ],
}

// Line-cell specs: bare chrome, so spec.label becomes the aria-label —
// preserving today's `Line N …` accessible names exactly.
const lineSpec = (i: number, part: string, over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key: `line-${i}-${part}`, label: `Line ${i + 1} ${part}`, dataType: 'text', ...over })

export function PrForm({ id, onBack }: { id: string | null; onBack: () => void }) {
  const qc = useQueryClient()
  const isNew = id === null
  const { data: pr, isPending } = useQuery({ queryKey: ['requisition', id], queryFn: () => getRequisition(id!), enabled: !isNew })

  const [h, setH] = useState<Record<HeaderKey, string>>({ requestor: '', department: '', category: '', location: '', job: '', requiredDate: '', memo: '' })
  const [lines, setLines] = useState<EditLine[]>([blankLine()])
  const [err, setErr] = useState<string | null>(null)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [cancelReason, setCancelReason] = useState('')
  const [dirty, setDirty] = useState(false)
  const markClean = useRef<() => void>(() => {})

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
  // Intentional navigation must not trip the dirty guard.
  const leave = () => { markClean.current(); onBack() }
  const save = useMutation({
    mutationFn: (mode: 'draft' | 'submit' | 'keep') =>
      isNew ? createPr(body(), mode === 'submit') : updatePr(id!, body()),
    onSuccess: () => { inval(); leave() },
    onError: (e: Error) => setErr(e.message),
  })
  const cancel = useMutation({
    mutationFn: () => cancelPr(id!, cancelReason),
    onSuccess: () => { inval(); setConfirmCancel(false); leave() },
    onError: (e: Error) => { setErr(e.message); setConfirmCancel(false) },
  })
  // Draft → Submitted (Bug 3). Saves in-progress header edits first so nothing is lost.
  const submit = useMutation({
    mutationFn: async () => { await updatePr(id!, body()); return submitPr(id!) },
    onSuccess: () => { inval(); leave() },
    onError: (e: Error) => setErr(e.message),
  })

  if (!isNew && isPending) return <Spinner />

  const setHeader = (k: string, v: string) => { setDirty(true); setH((p) => ({ ...p, [k]: v })) }
  const setLine = (i: number, k: keyof EditLine, v: string) => {
    setDirty(true)
    setLines((ls) => ls.map((l, x) => (x === i ? { ...l, [k]: v } : l)))
  }
  const removeLine = (i: number) => { setDirty(true); setLines((ls) => ls.filter((_, x) => x !== i)) }
  const addLine = () => { setDirty(true); setLines((ls) => [...ls, blankLine()]) }

  const actions = (
    <>
      {isNew ? (
        <>
          <Button variant="ghost" busy={save.isPending} onClick={() => save.mutate('draft')}>Save draft</Button>
          <Button variant="primary" icon="check" busy={save.isPending} onClick={() => save.mutate('submit')}>Submit PR</Button>
        </>
      ) : headerStatus === 'Draft' ? (
        <>
          <Button variant="outline" busy={save.isPending || submit.isPending} onClick={() => save.mutate('keep')}>Save changes</Button>
          <Button variant="primary" icon="check" busy={submit.isPending || save.isPending} onClick={() => submit.mutate()}>Submit PR</Button>
        </>
      ) : (
        <Button variant="primary" icon="check" busy={save.isPending} onClick={() => save.mutate('keep')}>Save changes</Button>
      )}
      <div className="spacer" />
      {!isNew && !hasLiveLine && headerStatus !== 'Cancelled' && (
        <Button variant="ghost" red onClick={() => setConfirmCancel(true)}>Cancel PR</Button>
      )}
    </>
  )

  return (
    <TransactionPage
      crumbParent="Requisitions"
      onCrumbParent={onBack}
      crumbCurrent={isNew ? 'New PR' : pr?.code ?? ''}
      title={isNew ? 'Create Purchase Requisition' : `Edit ${pr?.code}`}
      subtitle={isNew ? 'Raise a purchase requisition directly in the portal.' : 'Header and open lines are editable. Sourced / awarded lines are locked.'}
      statusBadge={!isNew ? <PrHeaderBadge status={headerStatus} /> : undefined}
      error={err ? `Could not save: ${err}` : null}
      actions={actions}
      sections={[HEADER_SECTION]}
      values={h}
      onFieldChange={setHeader}
      dirty={dirty}
      onGuardReady={(fn) => { markClean.current = fn }}
    >
      <div className="card" style={{ padding: 18, marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>Lines</h3>
        <table>
          <thead><tr><th style={{ width: '18%' }}>Item code</th><th>Description</th><th className="amt" style={{ width: '10%' }}>Qty</th><th style={{ width: '10%' }}>UoM</th><th className="amt" style={{ width: '12%' }}>Est. rate</th><th style={{ width: '14%' }} /></tr></thead>
          <tbody>
            {lines.map((l, i) => {
              const ro = !isNew && !l.editable
              return (
                <tr key={l.id ?? `new-${i}`}>
                  <td><TextField chrome="bare" spec={lineSpec(i, 'item code', { placeholder: 'ITEM-CODE', readOnly: ro })} value={l.itemCode} onChange={(v) => setLine(i, 'itemCode', v)} /></td>
                  <td><TextField chrome="bare" spec={lineSpec(i, 'description', { placeholder: 'Description', readOnly: ro })} value={l.description} onChange={(v) => setLine(i, 'description', v)} /></td>
                  <td><NumberField chrome="bare" spec={lineSpec(i, 'qty', { dataType: 'number', placeholder: '0', readOnly: ro, validation: { min: 0 } })} value={l.qty} onChange={(v) => setLine(i, 'qty', v)} /></td>
                  <td><TextField chrome="bare" spec={lineSpec(i, 'uom', { placeholder: 'Unit', readOnly: ro })} value={l.uom} onChange={(v) => setLine(i, 'uom', v)} /></td>
                  <td><MoneyField chrome="bare" spec={lineSpec(i, 'rate', { dataType: 'money', placeholder: '0', readOnly: ro })} value={l.estUnitPrice} onChange={(v) => setLine(i, 'estUnitPrice', v)} /></td>
                  <td>
                    {ro
                      ? <span className="lockchip"><Icon name="lock" size={12} /> {l.lifecycleStatus === 'InRfq' ? 'In RFQ' : l.lifecycleStatus}</span>
                      : <Button variant="ghost" size="sm" red disabled={lines.length === 1} onClick={() => removeLine(i)}>Remove</Button>}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        <div style={{ marginTop: 10 }}><Button variant="ghost" size="sm" icon="plus" onClick={addLine}>Add line</Button></div>
        <div className="note" style={{ marginTop: 10 }}><Icon name="eye" size={13} /> Estimated total: <b>{total.toLocaleString()}</b></div>
      </div>

      {confirmCancel && (
        <Modal
          title={`Cancel ${pr?.code}?`} icon="x"
          footer={<>
            <Button variant="outline" onClick={() => setConfirmCancel(false)}>Keep PR</Button>
            <Button variant="danger" busy={cancel.isPending} onClick={() => cancel.mutate()}>Cancel PR</Button>
          </>}
        >
          <p style={{ marginTop: 0 }}>This cancels the PR and all its open lines. Sourced/awarded lines block cancellation, so none are present.</p>
          <TextAreaField
            spec={{ key: 'cancelReason', label: 'Reason', dataType: 'longText', placeholder: 'e.g. Duplicate of another PR / no longer required' }}
            value={cancelReason} onChange={setCancelReason}
          />
        </Modal>
      )}
      {id && <CustomFieldsSection recordType="Requisition" recordId={id} />}
    </TransactionPage>
  )
}

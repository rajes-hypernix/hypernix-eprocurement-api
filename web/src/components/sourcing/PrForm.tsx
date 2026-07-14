import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getRequisition, createPr, updatePr, cancelPr, submitPr, resolveEntryForm,
  getCustomValues, saveCustomValues, getSegmentAssignments, saveSegmentAssignments,
  getLineCustomDefs, getLineCustomValues, saveLineCustomValues,
  type RequisitionDto, type SavePrRequest,
} from '../../api/client'
import { renderField } from '../../ui/renderField'
import { buildSections, splitSubtabs, nativeDefaults, missingRequired, stateKey } from '../entryforms/resolvedForm'
import { Icon } from '../Icon'
import { Modal, Spinner } from '../ui'
import { PrHeaderBadge } from '../../lib/prStatus'
import type { FieldSpec } from '../../ui/fieldSpec'
import { SelectField } from '../../ui/SelectField'
import { TextField } from '../../ui/TextField'
import { TextAreaField } from '../../ui/TextAreaField'
import { NumberField } from '../../ui/NumberField'
import { MoneyField } from '../../ui/MoneyField'
import { Button } from '../../ui/Button'
import { TransactionPage } from '../../ui/archetypes/TransactionPage'
import { CustomFieldsSection } from '../customfields/CustomFieldsSection'
import { SegmentsSection } from '../segments/SegmentsSection'

const LINE_HEADS: Record<string, JSX.Element> = {
  ItemCode: <th key="ItemCode" style={{ width: '18%' }}>Item code</th>,
  Description: <th key="Description">Description</th>,
  Qty: <th key="Qty" className="amt" style={{ width: '10%' }}>Qty</th>,
  Uom: <th key="Uom" style={{ width: '10%' }}>UoM</th>,
  EstUnitPrice: <th key="EstUnitPrice" className="amt" style={{ width: '12%' }}>Est. rate</th>,
}

const LINE_SPEC_TYPE: Record<string, FieldSpec['dataType']> = {
  Text: 'text', LongText: 'text', Int: 'number', Decimal: 'number',
  Money: 'money', Date: 'date', Bool: 'yesNo', ListValue: 'select',
  DateTime: 'dateTime', Percent: 'percent', Email: 'text', Telephone: 'text',
  Hyperlink: 'text', Image: 'attachment', Document: 'attachment',
}

type PrLine = NonNullable<RequisitionDto['lines']>[number]
// A line being edited: carries the server id (existing) or undefined (new).
type EditLine = { id?: string; itemCode: string; description: string; qty: string; uom: string; estUnitPrice: string; lifecycleStatus: string; editable: boolean }

const toEdit = (l: PrLine): EditLine => ({
  id: l.id ?? undefined, itemCode: l.itemCode ?? '', description: l.description ?? '',
  qty: String(l.qty ?? ''), uom: l.uom ?? '', estUnitPrice: String(l.estUnitPrice ?? ''),
  lifecycleStatus: l.lifecycleStatus ?? 'Open', editable: l.editable ?? true,
})
const blankLine = (): EditLine => ({ itemCode: '', description: '', qty: '', uom: '', estUnitPrice: '', lifecycleStatus: 'Open', editable: true })

// D7: the header is no longer hardcoded — it renders from the caller's RESOLVED entry
// form (role-preferred → Standard; the seeded Standard reproduces the old HEADER_SECTION
// byte-identically — parity-pinned). Placed cf_/seg_ fields render inline on EDIT (their
// values ride their own A66/A67 endpoints and need a record id); the residual auto-
// sections keep showing whatever the form did NOT place (D5's zero-deploy promise).

// Line-cell specs: bare chrome, so spec.label becomes the aria-label —
// preserving today's `Line N …` accessible names exactly.
const lineSpec = (i: number, part: string, over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key: `line-${i}-${part}`, label: `Line ${i + 1} ${part}`, dataType: 'text', ...over })

export function PrForm({ id, onBack }: { id: string | null; onBack: () => void }) {
  const qc = useQueryClient()
  const isNew = id === null
  const { data: pr, isPending } = useQuery({ queryKey: ['requisition', id], queryFn: () => getRequisition(id!), enabled: !isNew })
  // CF-FIX4-T5: the form picker — an explicit choice re-resolves the LAYOUT; the server
  // still enforces the ROLE form's required fields at submit (OD-D7-2).
  const [chosenFormId, setChosenFormId] = useState<string | null>(null)
  const { data: form } = useQuery({ queryKey: ['entry-form', 'Requisition', chosenFormId], queryFn: () => resolveEntryForm('Requisition', chosenFormId) })
  // CF-FIX4-T3 (L4): the lines table renders the resolved form's flat sublist order;
  // unknown/missing keys fall back to the standard order (parity when no config exists).
  const NATIVE_LINE_COLS = ['ItemCode', 'Description', 'Qty', 'Uom', 'EstUnitPrice']
  // CF-FIX5-T4: the sublist order drives BOTH native columns and custcol_ line fields.
  const sublistOrder = form?.sublistColumns ?? NATIVE_LINE_COLS

  const [h, setH] = useState<Record<string, string>>({ requestor: '', department: '', category: '', location: '', job: '', requiredDate: '', memo: '' })
  const [lines, setLines] = useState<EditLine[]>([blankLine()])
  // CF6: line-scoped custom fields — extra editable columns keyed by REAL line id.
  const [lineVals, setLineVals] = useState<Record<string, Record<string, string>>>({})
  const [err, setErr] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [cancelReason, setCancelReason] = useState('')
  const [dirty, setDirty] = useState(false)
  const markClean = useRef<() => void>(() => {})

  // Placed cf_/seg_ keys render inline (edit only — their value endpoints need a record id).
  const placedCustom = (form?.fields ?? []).filter((f) => f.kind === 'Custom').map((f) => f.fieldKey)
  const placedSegment = (form?.fields ?? []).filter((f) => f.kind === 'Segment').map((f) => f.fieldKey)
  const { data: customValues } = useQuery({
    queryKey: ['custom-values', 'Requisition', id],
    queryFn: () => getCustomValues('Requisition', id!),
    enabled: !isNew && placedCustom.length > 0,
  })
  const { data: segmentAssignments } = useQuery({
    queryKey: ['segment-assignments', 'Requisition', id, null],
    queryFn: () => getSegmentAssignments('Requisition', id!),
    enabled: !isNew && placedSegment.length > 0,
  })
  const [extras, setExtras] = useState<Record<string, string>>({})
  useEffect(() => {
    const next: Record<string, string> = {}
    for (const v of customValues ?? []) next[v.code] = v.value ?? ''
    for (const a of segmentAssignments ?? []) next[a.segmentCode] = a.valueCode ?? ''
    setExtras((cur) => ({ ...next, ...cur }))   // in-progress edits win over refetches
  }, [customValues, segmentAssignments])

  // Hydrate from the loaded PR once (guard in-progress edits from a background refetch);
  // NEW records take the resolved form's defaults instead (never applied to edits).
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
    if (isNew && form && !hydrated) {
      setH((cur) => ({ ...cur, ...nativeDefaults(form.fields) }))
      setHydrated(true)
    }
  }, [pr, form, isNew, hydrated])

  const total = lines.reduce((s, l) => s + (Number(l.qty) || 0) * (Number(l.estUnitPrice) || 0), 0)
  const hasLiveLine = lines.some((l) => l.lifecycleStatus === 'InRfq' || l.lifecycleStatus === 'Awarded')
  const headerStatus = pr?.headerStatus
  const { data: lineDefs = [] } = useQuery({ queryKey: ['line-defs', 'Requisition'], queryFn: () => getLineCustomDefs('Requisition'), staleTime: 60_000 })
  // CF-FIX5-T4: custcol_ line fields render in the sublist ORDER (interleaved with natives);
  // applied line fields NOT placed in the order append after (CF6 default-shown, non-breaking).
  const lineDefByCode = new Map(lineDefs.map((d) => [d.code, d]))
  const appendedDefs = lineDefs.filter((d) => !sublistOrder.includes(d.code))
  const sublistHeads = sublistOrder.map((k) =>
    NATIVE_LINE_COLS.includes(k) ? LINE_HEADS[k] : (lineDefByCode.get(k) ? <th key={k}>{lineDefByCode.get(k)!.label}</th> : null))
  const { data: lineValues } = useQuery({
    queryKey: ['line-values', 'Requisition', id], queryFn: () => getLineCustomValues('Requisition', id!), enabled: !isNew && !!id,
  })
  // Line values arrive on their OWN query — initialize when THEY land, not when pr does.
  useEffect(() => {
    if (lineValues) setLineVals(Object.fromEntries(Object.entries(lineValues).map(
      ([lid, vs]) => [lid, Object.fromEntries(vs.map((v) => [v.code, v.value ?? '']))])))
  }, [lineValues])

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

  // Form-placed cf_/seg_ values save alongside the header (their own endpoints, only the
  // keys this form placed — the residual sections keep owning everything else). CF-FIX5-T1:
  // on CREATE the record id arrives from createPr's response, so this takes it explicitly
  // and no longer bails on isNew — the chosen form's custom fields now persist at creation.
  const saveExtras = async (recordId: string) => {
    if (!form) return
    const cf = Object.fromEntries(placedCustom.filter((k) => k in extras).map((k) => [k, extras[k] === '' ? null : extras[k]]))
    if (Object.keys(cf).length > 0) await saveCustomValues('Requisition', recordId, cf)
    const seg = Object.fromEntries(placedSegment.filter((k) => k in extras).map((k) => [k, extras[k] === '' ? null : extras[k]]))
    if (Object.keys(seg).length > 0) await saveSegmentAssignments('Requisition', recordId, seg)
    // CF6: per-line custom values ride the same save (server verifies line ownership).
    // Lines only carry ids once the record exists, so on create this stays empty (unchanged).
    if (lineDefs.length > 0 && Object.keys(lineVals).length > 0)
      await saveLineCustomValues('Requisition', recordId,
        Object.fromEntries(Object.entries(lineVals).map(([lid, vs]) =>
          [lid, Object.fromEntries(Object.entries(vs).map(([k, v]) => [k, v === '' ? null : v]))])))
  }

  // The client-side face of the (c) boundary: required-on-form blocks SUBMIT, not drafts.
  // The server re-resolves and enforces the same rule regardless (OD-D7-2).
  const requiredGate = (): boolean => {
    const missing = form ? missingRequired(form.fields, h) : []
    if (missing.length === 0) { setFieldErrors({}); return true }
    setFieldErrors(Object.fromEntries(form!.fields
      .filter((f) => missing.includes(f.label))
      .map((f) => [stateKey(f.fieldKey), 'Required before submit'])))
    setErr(`Required on your form before submit: ${missing.join(', ')}.`)
    return false
  }

  const save = useMutation({
    mutationFn: async (mode: 'draft' | 'submit' | 'keep') => {
      if (mode === 'submit' && !requiredGate()) throw new Error('__handled__')
      const result = isNew ? await createPr(body(), mode === 'submit') : await updatePr(id!, body())
      await saveExtras(result.id)   // CF-FIX5-T1: the new record's id from the create response
      return result
    },
    onSuccess: () => { inval(); leave() },
    onError: (e: Error) => { if (e.message !== '__handled__') setErr(e.message) },
  })
  const cancel = useMutation({
    mutationFn: () => cancelPr(id!, cancelReason),
    onSuccess: () => { inval(); setConfirmCancel(false); leave() },
    onError: (e: Error) => { setErr(e.message); setConfirmCancel(false) },
  })
  // Draft → Submitted (Bug 3). Saves in-progress header edits first so nothing is lost.
  const submit = useMutation({
    mutationFn: async () => {
      if (!requiredGate()) throw new Error('__handled__')
      await updatePr(id!, body())
      await saveExtras(id!)
      return submitPr(id!)
    },
    onSuccess: () => { inval(); leave() },
    onError: (e: Error) => { if (e.message !== '__handled__') setErr(e.message) },
  })

  if ((!isNew && isPending) || !form) return <Spinner />

  // Layout from the RESOLVED definition. CF-FIX5-T1: the chosen form's placed custom/segment
  // fields now render on CREATE too — their values collect into `extras` and persist right
  // after createPr returns the record id. (form.fields only ever holds HEADER-scope custom
  // fields + segments + natives; LINE-scope custom fields are sublist columns, handled
  // separately and still deferred until the record's lines have ids.)
  const renderable = form.fields
  const { main, tabs: subtabFields } = splitSubtabs(renderable)
  const sections = buildSections(main)
  const formTabs = subtabFields.map(([name, fields]) => ({ key: name, label: name, sections: buildSections(fields) }))

  const setHeader = (k: string, v: string) => {
    setDirty(true)
    // CF-FIX5-T1: route by NATIVE-key membership, not a stale cf_/seg_ prefix test. Custom
    // fields have been custbody_/custcol_ and segments seg_/custseg_ since the prefix
    // migrations, so a prefix check silently dropped their edits into the native header
    // bucket (never reaching extras/saveExtras). A native key is one of h's own keys.
    if (k in h) setH((p) => ({ ...p, [k]: v }))
    else setExtras((p) => ({ ...p, [k]: v }))
  }
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
      sections={sections}
      values={{ ...h, ...extras }}
      onFieldChange={setHeader}
      fieldErrors={fieldErrors}
      tabs={formTabs.length > 0 ? formTabs : undefined}
      dirty={dirty}
      onGuardReady={(fn) => { markClean.current = fn }}
      topSlot={(form?.availableForms?.length ?? 0) > 1 && (
        // CF-FIX5-T2: the form picker is the FIRST control — choosing the form drives the
        // whole layout beneath it. Rendered ABOVE the Header via TransactionPage's topSlot.
        <div className="card" style={{ padding: 12, marginBottom: 12, maxWidth: 420 }}>
          <SelectField
            spec={{ key: 'pr-form-picker', label: 'Entry form', dataType: 'select', searchable: true,
              options: { kind: 'static', options: (form?.availableForms ?? []).map((f) => ({ code: f.id, label: f.name })) } }}
            value={form?.formId ?? ''}
            onChange={(v) => setChosenFormId(String(v ?? '') || null)} />
        </div>
      )}
    >
      <div className="card" style={{ padding: 18, marginBottom: 16 }}>
        <h3 style={{ marginTop: 0 }}>Lines</h3>
        <table>
          <thead><tr>{sublistHeads}{appendedDefs.map((d) => <th key={d.code}>{d.label}</th>)}<th style={{ width: '14%' }} /></tr></thead>
          <tbody>
            {lines.map((l, i) => {
              const ro = !isNew && !l.editable
              const nativeCell = (k: string) => {
                switch (k) {
                  case 'ItemCode': return <td key={k}><TextField chrome="bare" spec={lineSpec(i, 'item code', { placeholder: 'ITEM-CODE', readOnly: ro })} value={l.itemCode} onChange={(v) => setLine(i, 'itemCode', v)} /></td>
                  case 'Description': return <td key={k}><TextField chrome="bare" spec={lineSpec(i, 'description', { placeholder: 'Description', readOnly: ro })} value={l.description} onChange={(v) => setLine(i, 'description', v)} /></td>
                  case 'Qty': return <td key={k}><NumberField chrome="bare" spec={lineSpec(i, 'qty', { dataType: 'number', placeholder: '0', readOnly: ro, validation: { min: 0 } })} value={l.qty} onChange={(v) => setLine(i, 'qty', v)} /></td>
                  case 'Uom': return <td key={k}><TextField chrome="bare" spec={lineSpec(i, 'uom', { placeholder: 'Unit', readOnly: ro })} value={l.uom} onChange={(v) => setLine(i, 'uom', v)} /></td>
                  case 'EstUnitPrice': return <td key={k}><MoneyField chrome="bare" spec={lineSpec(i, 'rate', { dataType: 'money', placeholder: '0', readOnly: ro })} value={l.estUnitPrice} onChange={(v) => setLine(i, 'estUnitPrice', v)} /></td>
                  default: return null
                }
              }
              const custCell = (d: (typeof lineDefs)[number]) => (
                <td key={d.code}>
                  {l.id
                    ? renderField(
                        { key: `line-${i}-${d.code}`, label: `${d.label} line ${i + 1}`, dataType: LINE_SPEC_TYPE[d.dataType] ?? 'text',
                          ...(d.displayType === 'Disabled' || ro ? { displayType: 'disabled' as const } : {}),
                          ...(d.dataType === 'ListValue' && d.customListCode ? { options: { kind: 'customList' as const, listCode: d.customListCode } } : {}) },
                        lineVals[l.id]?.[d.code] ?? '',
                        (v) => { setDirty(true); setLineVals((m) => ({ ...m, [l.id!]: { ...m[l.id!], [d.code]: String(v ?? '') } })) },
                        { chrome: 'bare' })
                    : <span className="hint" title="Save the PR first — new lines get their id on save">—</span>}
                </td>
              )
              return (
                <tr key={l.id ?? `new-${i}`}>
                  {sublistOrder.map((k) => NATIVE_LINE_COLS.includes(k) ? nativeCell(k) : (lineDefByCode.get(k) ? custCell(lineDefByCode.get(k)!) : null))}
                  {appendedDefs.map((d) => custCell(d))}
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
      {id && <CustomFieldsSection recordType="Requisition" recordId={id} excludeKeys={placedCustom} />}
      {id && <SegmentsSection recordType="Requisition" recordId={id} excludeKeys={placedSegment} />}
    </TransactionPage>
  )
}

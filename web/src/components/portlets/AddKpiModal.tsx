import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getViews, getViewFields } from '../../api/client'
import type { PortletUpsert } from '../../api/client'
import { Modal } from '../ui'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { NumberField } from '../../ui/NumberField'
import type { FieldSpec } from '../../ui/fieldSpec'

const RECORD_TYPES = ['Rfq', 'Requisition', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']
const FNS = ['count', 'sum', 'avg']

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

/**
 * The D4 gate's flow: build a KPI from a saved view — pick view, fn (sum/avg need a
 * Money/Number field), optional target — end-to-end without code.
 */
export function AddKpiModal({ onClose, onAdd }: { onClose: () => void; onAdd: (p: PortletUpsert) => void }) {
  const [recordType, setRecordType] = useState('Rfq')
  const [viewId, setViewId] = useState('')
  const [fn, setFn] = useState('count')
  const [fieldKey, setFieldKey] = useState('')
  const [target, setTarget] = useState('')
  const [title, setTitle] = useState('')
  const [groupBy, setGroupBy] = useState('')

  const { data: views = [] } = useQuery({ queryKey: ['views', recordType], queryFn: () => getViews(recordType) })
  const { data: fields = [] } = useQuery({ queryKey: ['view-fields', recordType], queryFn: () => getViewFields(recordType), staleTime: Infinity })
  const numericKeys = fields.filter((f) => f.dataType === 'Money' || f.dataType === 'Number').map((f) => f.fieldKey)
  const segments = fields.filter((f) => f.kind === 'Segment')
  const view = views.find((v) => v.id === viewId)
  const ready = !!view && !!title.trim() && (fn === 'count' || !!fieldKey)

  const add = () => {
    if (!ready) return
    onAdd({
      id: null,
      portletType: 'KpiMeter',
      title: title.trim(),
      col: 0, row: 99, width: 1,           // appended; the renderer re-packs on save
      savedViewId: viewId,
      configJson: JSON.stringify({
        fn, fieldKey: fn === 'count' ? null : fieldKey,
        target: target ? Number(target) : null, link: null, metricId: null,
        groupBy: groupBy || null,
      }),
    })
  }

  return (
    <Modal
      title="Add KPI from a saved view"
      icon="dashboard"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={!ready} onClick={add}>Add KPI</button>
        </>
      }
    >
      <TextField spec={spec('kpi-title', 'KPI title', 'text')} value={title} onChange={(v) => setTitle(String(v ?? ''))} />
      <SelectField spec={spec('kpi-type', 'Record type', 'select', RECORD_TYPES)} value={recordType}
        onChange={(v) => { setRecordType(String(v ?? 'Rfq')); setViewId(''); setFieldKey('') }} />
      <SelectField
        spec={{ key: 'kpi-view', label: 'Saved view', dataType: 'select', options: { kind: 'static', options: views.map((v) => ({ code: v.id, label: v.name })) } }}
        value={viewId} onChange={(v) => setViewId(String(v ?? ''))} />
      <SelectField spec={spec('kpi-fn', 'Function', 'select', FNS)} value={fn} onChange={(v) => setFn(String(v ?? 'count'))} />
      {fn !== 'count' && (
        <SelectField spec={spec('kpi-field', 'Field', 'select', numericKeys)} value={fieldKey} onChange={(v) => setFieldKey(String(v ?? ''))} />
      )}
      {segments.length > 0 && (
        <SelectField
          spec={{ key: 'kpi-groupby', label: 'Slice by segment (optional)', dataType: 'select', options: { kind: 'static', options: segments.map((s) => ({ code: s.fieldKey, label: s.label })) } }}
          value={groupBy} onChange={(v) => setGroupBy(String(v ?? ''))} />
      )}
      <NumberField spec={spec('kpi-target', 'Target (optional)', 'number')} value={target} onChange={(v) => setTarget(String(v ?? ''))} />
    </Modal>
  )
}

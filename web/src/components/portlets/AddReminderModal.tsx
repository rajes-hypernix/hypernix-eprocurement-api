import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getViews } from '../../api/client'
import { LIST_ROUTE } from '../../lib/listRoutes'
import { Modal } from '../ui'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { recordTypeOptions } from '../../lib/recordTypeLabel'
import type { ReminderItem } from './portletConfig'

const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

/**
 * D7.5 task 5 (the operator's journey): add a reminder row from ANY view the caller can
 * run. The D4 Reminders portlet consumes it unchanged — counts stay live via aggregate,
 * click-through opens the list with the view selected (route from the ONE map). No new
 * engine: this writes an item into the existing RemindersConfig shape.
 */
export function AddReminderModal({ onClose, onAdd, onGoCreateViews }: {
  onClose: () => void; onAdd: (item: ReminderItem) => void
  /** CF3-T11: the create-view→bind loop, discoverable from an empty picker. */
  onGoCreateViews?: () => void
}) {
  const [recordType, setRecordType] = useState('Requisition')
  const [viewId, setViewId] = useState('')
  const [label, setLabel] = useState('')
  const { data: views = [] } = useQuery({ queryKey: ['views', recordType], queryFn: () => getViews(recordType) })
  const view = views.find((v) => v.id === viewId)
  const ready = !!view && !!(label.trim() || view.name)

  return (
    <Modal
      title="Add reminder from a saved view"
      icon="clock"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button
            type="button" className="btn btn-pri" disabled={!ready}
            onClick={() => view && onAdd({
              savedViewId: view.id,
              label: label.trim() || view.name,
              route: LIST_ROUTE[recordType] ?? 'views',
            })}
          >
            Add reminder
          </button>
        </>
      }
    >
      <SelectField spec={{ key: 'rem-type', searchable: true, label: 'Record type', dataType: 'select', options: { kind: 'static', options: recordTypeOptions(RECORD_TYPES) } }} value={recordType}
        onChange={(v) => { setRecordType(String(v ?? 'Requisition')); setViewId('') }} />
      <SelectField
        spec={{ key: 'rem-view', searchable: true, label: 'Saved view', dataType: 'select', options: { kind: 'static', options: views.map((v) => ({ code: v.id, label: v.name })) } }}
        value={viewId} onChange={(v) => setViewId(String(v ?? ''))} />
      <TextField spec={{ ...spec('rem-label', 'Label', 'text'), placeholder: 'defaults to the view name' }} value={label} onChange={(v) => setLabel(String(v ?? ''))} />
      {views.length === 0 && onGoCreateViews && (
        <p className="hint">
          No saved views for {recordType} yet —{' '}
          <button type="button" className="lnk" onClick={() => { onClose(); onGoCreateViews() }}>create one in Saved Views</button>{' '}
          and it appears here.
        </p>
      )}
      <p className="hint">The count stays live; clicking it opens the list with this view selected.</p>
    </Modal>
  )
}

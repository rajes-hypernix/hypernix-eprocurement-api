import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getCustomFieldDefs, createCustomFieldDef, updateCustomFieldDef, setCustomFieldActive, deleteCustomFieldDef,
  getCustomLists, type CustomFieldDefDto,
} from '../../api/client'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Modal, Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { TextAreaField } from '../../ui/TextAreaField'
import { SelectField } from '../../ui/SelectField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { recordTypeLabel } from '../../lib/recordTypeLabel'

/**
 * Custom field definitions (D5) — the Admin Setup surface (A65). Lifecycle per the rulings,
 * stated in the UI copy: a field with values is NEVER deleted (deactivate only — values
 * persist, entry surfaces and the view builder hide it, views referencing it fail loudly);
 * only a field with zero values may be deleted (typo cleanup).
 */

const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']
const DATA_TYPES = ['Text', 'LongText', 'Int', 'Decimal', 'Money', 'Date', 'Bool', 'ListValue']
const DISPLAY_TYPES = ['Normal', 'Disabled', 'Inline']   // CF4-T12: def-level display (Inline = plain text, not user-editable)

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

export function AdminCustomFields() {
  const qc = useQueryClient()
  const [recordType, setRecordType] = useState('PurchaseOrder')
  const [editing, setEditing] = useState<{ def: CustomFieldDefDto | null } | null>(null)
  const { data: defs = [] } = useQuery({ queryKey: ['custom-field-defs', recordType], queryFn: () => getCustomFieldDefs(recordType) })
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['custom-field-defs'] }) }

  const toggle = useMutation({
    mutationFn: (d: CustomFieldDefDto) => setCustomFieldActive(d.id, !d.active),
    onSuccess: refresh,
  })
  const remove = useMutation({ mutationFn: (id: string) => deleteCustomFieldDef(id), onSuccess: refresh })

  return (
    <SetupPage
      title="Custom Fields"
      primaryAction={<Button variant="primary" size="sm" icon="plus" onClick={() => setEditing({ def: null })}>New field</Button>}
      railItems={RECORD_TYPES.map((rt) => ({ key: rt, label: recordTypeLabel(rt), hint: rt === recordType ? `${defs.length} field(s)` : undefined }))}
      selectedKey={recordType}
      onSelect={setRecordType}
      detail={
        <div>
          <table>
            <thead><tr><th>Field</th><th>Code</th><th>Type</th><th>Values</th><th>Status</th><th /></tr></thead>
            <tbody>
              {defs.map((d) => (
                <tr key={d.id}>
                  <td>{d.label}{d.required && <span title="Required at value-save"> *</span>}</td>
                  <td className="mono">{d.code}</td>
                  <td>{d.dataType}{d.scope === 'Line' && <span className="badge b-grey" style={{ marginLeft: 6 }}>line</span>}</td>
                  <td className="amt">{d.valueCount}</td>
                  <td><span className={`badge ${d.active ? 'b-green' : 'b-grey'}`}>{d.active ? 'Active' : 'Deactivated'}</span></td>
                  <td className="amt">
                    <Button variant="ghost" size="sm" onClick={() => setEditing({ def: d })}>Edit</Button>
                    <Button variant="ghost" size="sm" onClick={() => toggle.mutate(d)} ariaLabel={`${d.active ? 'Deactivate' : 'Reactivate'} ${d.label}`}>
                      {d.active ? 'Deactivate' : 'Reactivate'}
                    </Button>
                    {d.valueCount === 0 && (
                      <Button variant="ghost" size="sm" red onClick={() => remove.mutate(d.id)} ariaLabel={`Delete ${d.label}`}>Delete</Button>
                    )}
                  </td>
                </tr>
              ))}
              {defs.length === 0 && <tr><td colSpan={6} className="hint">No custom fields on {recordTypeLabel(recordType)} yet.</td></tr>}
            </tbody>
          </table>
        </div>
      }
    >
      {editing && (
        <DefModal recordType={recordType} def={editing.def} siblings={defs}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); refresh() }} />
      )}
    </SetupPage>
  )
}

function DefModal({ recordType, def, siblings, onClose, onSaved }: {
  recordType: string; def: CustomFieldDefDto | null; siblings: CustomFieldDefDto[]
  onClose: () => void; onSaved: () => void
}) {
  const [label, setLabel] = useState(def?.label ?? '')
  const [dataType, setDataType] = useState(def?.dataType ?? 'Text')
  const [listId, setListId] = useState(def?.customListId ?? '')
  const [required, setRequired] = useState(def?.required ?? false)
  const [help, setHelp] = useState(def?.helpText ?? '')
  const [displayType, setDisplayType] = useState(def?.displayType ?? 'Normal')
  const [scope, setScope] = useState(def?.scope ?? 'Header')
  const [showInList, setShowInList] = useState(def?.showInList ?? false)
  const [insertBefore, setInsertBefore] = useState('')
  const placeTargets = siblings.filter((s) => s.id !== def?.id)
  const [error, setError] = useState<string | null>(null)
  const { data: lists = [] } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })

  const save = useMutation({
    mutationFn: () => {
      const req = {
        label, recordType, dataType,
        customListId: dataType === 'ListValue' ? (listId || null) : null,
        required, helpText: help, sort: def?.sort ?? 0,
        displayType, showInList, insertBeforeId: insertBefore || null, scope,
      }
      return def ? updateCustomFieldDef(def.id, req) : createCustomFieldDef(req)
    },
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the field.'),
  })

  return (
    <Modal
      title={def ? `Edit field — ${def.label}` : `New custom field on ${recordTypeLabel(recordType)}`}
      icon="edit"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={!label.trim() || save.isPending || (dataType === 'ListValue' && !listId)} onClick={() => save.mutate()}>
            {def ? 'Save changes' : 'Create field'}
          </button>
        </>
      }
    >
      <TextField spec={spec('cf-label', 'Label', 'text')} value={label} onChange={(v) => setLabel(String(v ?? ''))} />
      {def
        ? <p className="hint">Type ({def.dataType}) and code (<span className="mono">{def.code}</span>) are immutable — create a new field to change them.</p>
        : (
          <>
            <SelectField spec={spec('cf-type', 'Data type', 'select', DATA_TYPES)} value={dataType} onChange={(v) => setDataType(String(v ?? 'Text'))} />
            {dataType === 'ListValue' && (
              <SelectField
                spec={{ key: 'cf-list', label: 'Custom list', dataType: 'select', options: { kind: 'static', options: lists.map((l) => ({ code: l.id ?? '', label: l.name ?? l.code ?? '' })) } }}
                value={listId} onChange={(v) => setListId(String(v ?? ''))} />
            )}
            <SelectField spec={spec('cf-scope', 'Scope (header field or line column)', 'select', ['Header', 'Line'])}
              value={scope} onChange={(v) => setScope(String(v ?? 'Header'))} />
            {scope === 'Line' && <p className="hint">A line column on the record’s lines table (PR/PO/RFQ). Values live per line; not searchable in views yet.</p>}
          </>
        )}
      <SelectField spec={spec('cf-display', 'Display type', 'select', DISPLAY_TYPES)} value={displayType} onChange={(v) => setDisplayType(String(v ?? 'Normal'))} />
      {displayType !== 'Normal' && <p className="hint">Disabled and Inline fields render read-only and reject user edits — values arrive via defaults or imports.</p>}
      {placeTargets.length > 0 && (
        <SelectField
          spec={{ key: 'cf-before', label: 'Insert before', dataType: 'select', placeholder: '(at the end)',
            options: { kind: 'static', options: placeTargets.map((s) => ({ code: s.id, label: s.label })) } }}
          value={insertBefore} onChange={(v) => setInsertBefore(String(v ?? ''))} />
      )}
      <CheckboxField spec={spec('cf-showinlist', 'Show On Default List', 'boolean')} value={showInList} onChange={(v) => setShowInList(v === true)} />
      <CheckboxField spec={spec('cf-required', 'Mandatory', 'boolean')} value={required} onChange={(v) => setRequired(v === true)} />
      <TextAreaField spec={spec('cf-help', 'Help text', 'longText')} value={help} onChange={(v) => setHelp(String(v ?? ''))} />
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

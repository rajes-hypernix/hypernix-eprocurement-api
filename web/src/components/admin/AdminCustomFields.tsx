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

/**
 * Custom field definitions (D5) — the Admin Setup surface (A65). Lifecycle per the rulings,
 * stated in the UI copy: a field with values is NEVER deleted (deactivate only — values
 * persist, entry surfaces and the view builder hide it, views referencing it fail loudly);
 * only a field with zero values may be deleted (typo cleanup).
 */

const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']
const DATA_TYPES = ['Text', 'LongText', 'Int', 'Decimal', 'Money', 'Date', 'Bool', 'ListValue']

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
      subtitle="Fields your team defines — they render, filter and aggregate exactly like built-ins."
      primaryAction={<Button variant="primary" size="sm" icon="plus" onClick={() => setEditing({ def: null })}>New field</Button>}
      railItems={RECORD_TYPES.map((rt) => ({ key: rt, label: rt, hint: rt === recordType ? `${defs.length} field(s)` : undefined }))}
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
                  <td>{d.dataType}</td>
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
              {defs.length === 0 && <tr><td colSpan={6} className="hint">No custom fields on {recordType} yet.</td></tr>}
            </tbody>
          </table>
          <p className="hint" style={{ marginTop: 10 }}>
            A field with values is never deleted — deactivating hides it from entry screens and the
            view builder while its data persists; saved views still referencing it fail loudly until
            fixed. Only a field with zero values can be deleted. Required is enforced when values are
            saved — it does not block record transitions (that arrives with the form engine).
          </p>
        </div>
      }
    >
      {editing && (
        <DefModal recordType={recordType} def={editing.def}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); refresh() }} />
      )}
    </SetupPage>
  )
}

function DefModal({ recordType, def, onClose, onSaved }: {
  recordType: string; def: CustomFieldDefDto | null; onClose: () => void; onSaved: () => void
}) {
  const [label, setLabel] = useState(def?.label ?? '')
  const [dataType, setDataType] = useState(def?.dataType ?? 'Text')
  const [listId, setListId] = useState(def?.customListId ?? '')
  const [required, setRequired] = useState(def?.required ?? false)
  const [help, setHelp] = useState(def?.helpText ?? '')
  const [error, setError] = useState<string | null>(null)
  const { data: lists = [] } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })

  const save = useMutation({
    mutationFn: () => {
      const req = {
        label, recordType, dataType,
        customListId: dataType === 'ListValue' ? (listId || null) : null,
        required, helpText: help, sort: def?.sort ?? 0,
      }
      return def ? updateCustomFieldDef(def.id, req) : createCustomFieldDef(req)
    },
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the field.'),
  })

  return (
    <Modal
      title={def ? `Edit field — ${def.label}` : `New custom field on ${recordType}`}
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
          </>
        )}
      <CheckboxField spec={spec('cf-required', 'Required (at value-save only)', 'boolean')} value={required} onChange={(v) => setRequired(v === true)} />
      <TextAreaField spec={spec('cf-help', 'Help text', 'longText')} value={help} onChange={(v) => setHelp(String(v ?? ''))} />
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

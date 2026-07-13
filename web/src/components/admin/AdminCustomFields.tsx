import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getCustomFieldDefs, createCustomFieldDef, updateCustomFieldDef, setCustomFieldActive, deleteCustomFieldDef,
  getCustomFieldReferences, purgeCustomField, getEntryForms, archiveCustomField, unarchiveCustomField,
  getCustomLists, type CustomFieldDefDto, type FieldPlacementRequest,
} from '../../api/client'
import { ImpactReportDialog } from './ImpactReportDialog'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Modal, Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { TextAreaField } from '../../ui/TextAreaField'
import { SelectField } from '../../ui/SelectField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { recordTypeLabel, recordTypeOptions } from '../../lib/recordTypeLabel'

/**
 * Custom field definitions (D5) — the Admin Setup surface (A65). Lifecycle per the rulings,
 * stated in the UI copy: a field with values is NEVER deleted (deactivate only — values
 * persist, entry surfaces and the view builder hide it, views referencing it fail loudly);
 * only a field with zero values may be deleted (typo cleanup).
 */

const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']
// CF-FIX1-T8: full professional type names (the operator's NetSuite set) ↔ enum codes.
const DATA_TYPES: { code: string; label: string }[] = [
  { code: 'Text', label: 'Free-Form Text' },
  { code: 'LongText', label: 'Long Text' },
  { code: 'Int', label: 'Integer Number' },
  { code: 'Decimal', label: 'Decimal Number' },
  { code: 'Money', label: 'Currency' },
  { code: 'Percent', label: 'Percent' },
  { code: 'Bool', label: 'Check Box' },
  { code: 'Date', label: 'Date' },
  { code: 'DateTime', label: 'Date/Time' },
  { code: 'ListValue', label: 'List/Record' },
  { code: 'Email', label: 'Email Address' },
  { code: 'Telephone', label: 'Phone Number' },
  { code: 'Hyperlink', label: 'Hyperlink' },
  { code: 'Image', label: 'Image' },
  { code: 'Document', label: 'Document' },
]
export const dataTypeLabel = (code: string): string => DATA_TYPES.find((d) => d.code === code)?.label ?? code
const DISPLAY_TYPES = ['Normal', 'Disabled', 'Inline']   // CF4-T12: def-level display (Inline = plain text, not user-editable)

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

export function AdminCustomFields() {
  const qc = useQueryClient()
  const [recordType, setRecordType] = useState('PurchaseOrder')
  const [editing, setEditing] = useState<{ def: CustomFieldDefDto | null; replacing?: CustomFieldDefDto } | null>(null)
  // CF-FIX3-T3: Delete opens the impact report — the admin sees WHAT blocks (or what
  // a purge would remove) before any destructive verb; the server re-enforces all tiers.
  const [impact, setImpact] = useState<CustomFieldDefDto | null>(null)
  const { data: defs = [] } = useQuery({ queryKey: ['custom-field-defs', recordType], queryFn: () => getCustomFieldDefs(recordType) })
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['custom-field-defs'] }) }

  const toggle = useMutation({
    mutationFn: (d: CustomFieldDefDto) => setCustomFieldActive(d.id, !d.active),
    onSuccess: refresh,
  })
  // CF-FIX4-T8: the reversible ARCHIVE tier — values hidden from every live surface,
  // preserved verbatim; un-archive restores. Distinct from Deactivate (values stay
  // visible) and Purge (irreversible).
  const archive = useMutation({
    mutationFn: (d: CustomFieldDefDto) => (d.archived ? unarchiveCustomField(d.id) : archiveCustomField(d.id)),
    onSuccess: refresh,
  })

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
                  <td>{d.label}{d.required && <span title="Required at value-save"> *</span>}
                    {(d.recordTypes?.length ?? 0) > 1 && <span className="badge b-blue" style={{ marginLeft: 6 }} title={d.recordTypes!.map(recordTypeLabel).join(', ')}>shared ×{d.recordTypes!.length}</span>}
                  </td>
                  <td className="mono">{d.code}</td>
                  <td>{dataTypeLabel(d.dataType)}{d.scope === 'Line' && <span className="badge b-grey" style={{ marginLeft: 6 }}>line</span>}</td>
                  <td className="amt">{d.valueCount}</td>
                  <td>
                    <span className={`badge ${d.archived ? 'b-grey' : d.active ? 'b-green' : 'b-grey'}`}>
                      {d.archived ? 'Archived' : d.active ? 'Active' : 'Deactivated'}
                    </span>
                  </td>
                  <td className="amt">
                    <Button variant="ghost" size="sm" onClick={() => setEditing({ def: d })}>Edit</Button>
                    <Button variant="ghost" size="sm" onClick={() => toggle.mutate(d)} ariaLabel={`${d.active ? 'Deactivate' : 'Reactivate'} ${d.label}`}>
                      {d.active ? 'Deactivate' : 'Reactivate'}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => archive.mutate(d)}
                      ariaLabel={`${d.archived ? 'Unarchive' : 'Archive'} ${d.label}`}
                      title={d.archived ? 'Restore the values to every surface' : 'Hide the values from every surface, reversibly — they stay in storage and the audit trail'}>
                      {d.archived ? 'Unarchive' : 'Archive'}
                    </Button>
                    <Button variant="ghost" size="sm" red onClick={() => setImpact(d)} ariaLabel={`Delete ${d.label}`}>Delete</Button>
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
        <DefModal key={editing.def?.id ?? (editing.replacing ? `repl-${editing.replacing.id}` : 'new')}
          recordType={recordType} def={editing.def} replacing={editing.replacing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); refresh() }}
          onReplace={(d) => setEditing({ def: null, replacing: d })} />
      )}
      {impact && (
        <ImpactReportDialog
          title={`Delete field — ${impact.label}`}
          kind="field"
          load={() => getCustomFieldReferences(impact.id)}
          onDeactivate={impact.active ? () => setCustomFieldActive(impact.id, false) : undefined}
          onDelete={() => deleteCustomFieldDef(impact.id)}
          onPurge={() => purgeCustomField(impact.id)}
          onClose={(changed) => { setImpact(null); if (changed) refresh() }}
        />
      )}
    </SetupPage>
  )
}

function DefModal({ recordType, def, replacing, onClose, onSaved, onReplace }: {
  recordType: string; def: CustomFieldDefDto | null; replacing?: CustomFieldDefDto
  onClose: () => void; onSaved: () => void; onReplace: (d: CustomFieldDefDto) => void
}) {
  // CF-FIX3-T5: "Create replacement" — the change-the-type path. A NEW field prefilled
  // from the old one (label, scope, applies-to, list binding) with a FRESH Internal ID;
  // the admin picks the new type, then inactivates the original.
  const from = def ?? replacing ?? null
  const [label, setLabel] = useState(from?.label ?? '')
  // CF-FIX1-T5: user-set Internal ID — auto-suggested from the label until the user edits it.
  const suggestId = (l: string) => l.trim().toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '')
  const [internalId, setInternalId] = useState(replacing ? `${suggestId(replacing.label)}_2` : '')
  const [idTouched, setIdTouched] = useState(!!replacing)
  const [dataType, setDataType] = useState(from?.dataType ?? 'Text')
  const [listId, setListId] = useState(from?.customListId ?? '')
  const [required, setRequired] = useState(from?.required ?? false)
  const [help, setHelp] = useState(from?.helpText ?? '')
  const [displayType, setDisplayType] = useState(from?.displayType ?? 'Normal')
  const [scope, setScope] = useState(from?.scope ?? 'Header')
  // CF-FIX2-T3: the applies-to SET (NetSuite) — authored once, applied to many types.
  const [appliesTo, setAppliesTo] = useState<string>((from?.recordTypes?.length ? from.recordTypes : [recordType]).join('|'))
  const [showInList, setShowInList] = useState(from?.showInList ?? false)

  const [error, setError] = useState<string | null>(null)
  const { data: lists = [] } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })
  // CF-FIX4-T4: the mandatory cascade — per chosen form-bearing record type, form(s) with
  // the STANDARD form pre-selected (option c, locked) and a group per form (Header default).
  const { data: allForms = [] } = useQuery({ queryKey: ['entry-form-defs'], queryFn: () => getEntryForms() })
  const [placeForms, setPlaceForms] = useState<Record<string, string>>({})        // recordType -> 'formId|formId'
  const [placeGroups, setPlaceGroups] = useState<Record<string, string>>({})      // formId -> groupId
  const chosenTypes = (appliesTo ? appliesTo.split('|') : [recordType])
  const newTypes = def ? chosenTypes.filter((t) => !(def.recordTypes ?? []).includes(t)) : chosenTypes
  const placementTypes = scope === 'Line' ? [] : newTypes.filter((t) => allForms.some((f) => f.recordType === t && f.active))
  const formsFor = (t: string) => allForms.filter((f) => f.recordType === t && f.active)
  const selectedFormIds = (t: string) => {
    const cur = placeForms[t]
    if (cur !== undefined) return cur ? cur.split('|') : []
    const std = formsFor(t).find((f) => f.isSystem)
    return std ? [std.id] : []            // option (c): standard pre-selected
  }
  const buildPlacements = (): FieldPlacementRequest[] | null => {
    if (placementTypes.length === 0) return null   // legacy seam: nothing to place (or Line scope)
    return placementTypes.flatMap((t) => selectedFormIds(t).map((fid) => ({
      recordType: t, formId: fid, groupId: placeGroups[fid] || null,
    })))
  }

  const save = useMutation({
    mutationFn: () => {
      const req = {
        label, recordType, dataType,
        customListId: dataType === 'ListValue' ? (listId || null) : null,
        required, helpText: help, sort: def?.sort ?? 0,
        displayType, showInList, scope,
        code: def ? null : (internalId.trim() || null),
        recordTypes: appliesTo ? appliesTo.split('|') : [recordType],
        placements: buildPlacements(),
      }
      return def ? updateCustomFieldDef(def.id, req) : createCustomFieldDef(req)
    },
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the field.'),
  })

  return (
    <Modal
      title={def ? `Edit field — ${def.label}` : replacing ? `Replacement for — ${replacing.label}` : `New custom field on ${recordTypeLabel(recordType)}`}
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
      <TextField spec={spec('cf-label', 'Label', 'text')} value={label}
        onChange={(v) => { const l = String(v ?? ''); setLabel(l); if (!idTouched) setInternalId(suggestId(l)) }} />
      {!def && (
        <TextField spec={{ ...spec('cf-id', 'Internal ID', 'text'),
            affix: scope === 'Line' ? 'custcol_' : 'custbody_',
            placeholder: internalId ? undefined : 'auto-suggested from the label',
            help: 'Letters, digits, underscores. Immutable after create.' }}
          value={internalId} onChange={(v) => { setIdTouched(true); setInternalId(String(v ?? '')) }} />
      )}
      {def
        ? (
          <p className="hint">
            Type ({dataTypeLabel(def.dataType)}) and Internal ID (<span className="mono">{def.code}</span>) are immutable —
            to change the type, inactivate this field and create a new one.{' '}
            <Button variant="ghost" size="sm" onClick={() => onReplace(def)} ariaLabel={`Create replacement for ${def.label}`}>Create replacement</Button>
          </p>
        )
        : (
          <>
            <SelectField spec={{ key: 'cf-type', label: 'Data type', dataType: 'select', searchable: true,
              options: { kind: 'static', options: DATA_TYPES } }} value={dataType} onChange={(v) => setDataType(String(v ?? 'Text'))} />
            {dataType === 'ListValue' && (
              <SelectField
                spec={{ key: 'cf-list', label: 'Custom list', dataType: 'select', searchable: true, options: { kind: 'static', options: lists.map((l) => ({ code: l.id ?? '', label: l.name ?? l.code ?? '' })) } }}
                value={listId} onChange={(v) => setListId(String(v ?? ''))} />
            )}
            <SelectField spec={{ ...spec('cf-scope', 'Scope (header field or line column)', 'select', ['Header', 'Line']), searchable: true }}
              value={scope} onChange={(v) => setScope(String(v ?? 'Header'))} />
            {scope === 'Line' && <p className="hint">A line column on the record’s lines table (PR/PO/RFQ). Values live per line; not searchable in views yet.</p>}
          </>
        )}
      <SelectField
        spec={{ key: 'cf-applies', label: 'Applies to', dataType: 'multiSelect', searchable: true,
          help: 'The record types this ONE field applies to — its value can carry between them (PR → PO).',
          options: { kind: 'static', options: recordTypeOptions(RECORD_TYPES) } }}
        value={appliesTo} onChange={(v) => setAppliesTo(String(v ?? ''))} />
      {placementTypes.length > 0 && (
        <div className="card" style={{ padding: 10, marginBottom: 10 }}>
          <div className="hint" style={{ fontWeight: 700, marginBottom: 6 }}>Placement — where this field appears (record → form → group)</div>
          {placementTypes.map((t) => (
            <div key={t} style={{ marginBottom: 8 }}>
              <SelectField
                spec={{ key: `cf-place-form-${t}`, label: `${recordTypeLabel(t)} — form(s)`, dataType: 'multiSelect', searchable: true,
                  help: 'Mandatory. The standard form is pre-selected; the group defaults to Header.',
                  options: { kind: 'static', options: formsFor(t).map((f) => ({ code: f.id, label: f.name })) } }}
                value={selectedFormIds(t).join('|')}
                onChange={(v) => setPlaceForms((cur) => ({ ...cur, [t]: String(v ?? '') }))} />
              {selectedFormIds(t).map((fid) => {
                const form = allForms.find((f) => f.id === fid)
                if (!form) return null
                return (
                  <SelectField key={fid}
                    spec={{ key: `cf-place-group-${fid}`, label: `${form.name} — field group`, dataType: 'select', searchable: true,
                      options: { kind: 'static', options: (form.groups ?? []).map((g) => ({ code: g.id, label: g.title })) } }}
                    value={placeGroups[fid] ?? (form.groups ?? []).find((g) => g.isHeader)?.id ?? ''}
                    onChange={(v) => setPlaceGroups((cur) => ({ ...cur, [fid]: String(v ?? '') }))} />
                )
              })}
            </div>
          ))}
        </div>
      )}
      <SelectField spec={{ ...spec('cf-display', 'Display type', 'select', DISPLAY_TYPES), searchable: true }} value={displayType} onChange={(v) => setDisplayType(String(v ?? 'Normal'))} />
      {displayType !== 'Normal' && <p className="hint">Disabled and Inline fields render read-only and reject user edits — values arrive via defaults or imports.</p>}
      <CheckboxField spec={spec('cf-showinlist', 'Show On Default List', 'boolean')} value={showInList} onChange={(v) => setShowInList(v === true)} />
      <CheckboxField spec={spec('cf-required', 'Mandatory', 'boolean')} value={required} onChange={(v) => setRequired(v === true)} />
      <TextAreaField spec={spec('cf-help', 'Help text', 'longText')} value={help} onChange={(v) => setHelp(String(v ?? ''))} />
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

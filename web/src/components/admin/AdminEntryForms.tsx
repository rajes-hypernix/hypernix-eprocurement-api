import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getEntryForms, createEntryForm, updateEntryForm, deleteEntryForm, assignEntryFormRoles, setEntryFormActive,
  saveEntryFormSubtab, deleteEntryFormSubtab, saveEntryFormGroup, deleteEntryFormGroup,
  getViewFields, type EntryFormDefDto, type EntryFormFieldDto,
} from '../../api/client'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'

/**
 * Entry-form composer (D7) — the Admin Setup surface (A69). "Entry forms" carry entry
 * BEHAVIOUR (which fields, in what groups/subtabs, display type, required-at-submit,
 * defaults) per role — distinct from Sourcing→Forms (RFQ bid questionnaires). The
 * Standard form is the seeded parity baseline and is read-only; "New form" starts as a
 * copy of the selected form. Requisition-only this slice (OD-D7-5) — each entry
 * surface's migration gate widens the record types.
 */

const ROLES = ['Buyer', 'Approver', 'TechEvaluator', 'CommEvaluator', 'Admin']
const DISPLAY_TYPES = ['Normal', 'Disabled', 'ReadOnly', 'Hidden']
// Mirror of the server's EntryFormVocabulary (display gating only — the server enforces):
// a native key is placeable iff SavePrRequest carries it.
const CONTROLLABLE_NATIVE = ['Requestor', 'Department', 'Location', 'Category', 'Job', 'Memo', 'RequiredDate']

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

export function AdminEntryForms() {
  const qc = useQueryClient()
  const { data: forms = [] } = useQuery({ queryKey: ['entry-form-defs'], queryFn: () => getEntryForms('Requisition') })
  const [selected, setSelected] = useState<string | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const form = forms.find((f) => f.id === selected) ?? forms[0]
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['entry-form-defs'] }); void qc.invalidateQueries({ queryKey: ['entry-form'] }) }

  const copy = useMutation({
    mutationFn: (source: EntryFormDefDto) => createEntryForm({
      name: `${source.name} (copy)`, recordType: 'Requisition', fields: source.fields,
    }),
    onSuccess: (d) => { setSelected(d.id); setListError(null); refresh() },
    onError: (e) => setListError(e instanceof Error ? e.message : 'Could not copy the form.'),
  })

  return (
    <SetupPage
      title="Entry Forms"
      subtitle="Role-specific entry layouts — fields, groups, subtabs, display types, required-at-submit, defaults. Zero deployments."
      primaryAction={form && (
        <Button variant="primary" size="sm" icon="plus" busy={copy.isPending} onClick={() => copy.mutate(form)}>
          New form (copy of {form.name})
        </Button>
      )}
      railItems={forms.map((f) => ({
        key: f.id, label: f.name,
        hint: f.isSystem ? 'standard' : f.roles.length > 0 ? f.roles.join(', ') : `${f.fields.length} field(s)`,
      }))}
      selectedKey={form?.id ?? ''}
      onSelect={setSelected}
      detail={
        <>
          {listError && <Notice tone="error">{listError}</Notice>}
          {form
            ? <FormComposer key={form.id} form={form} onChanged={refresh} onDeleted={() => { setSelected(null); refresh() }} />
            : <p className="hint">No entry forms yet.</p>}
        </>}
    />
  )
}

function FormComposer({ form, onChanged, onDeleted }: {
  form: EntryFormDefDto; onChanged: () => void; onDeleted: () => void
}) {
  const [name, setName] = useState(form.name)
  const [fields, setFields] = useState<EntryFormFieldDto[]>(form.fields)
  const [roles, setRoles] = useState<string[]>(form.roles)
  const [addKey, setAddKey] = useState('')
  const [error, setError] = useState<string | null>(null)
  useEffect(() => { setName(form.name); setFields(form.fields); setRoles(form.roles) }, [form])

  const { data: registry = [] } = useQuery({
    queryKey: ['view-fields', 'Requisition'], queryFn: () => getViewFields('Requisition'), staleTime: Infinity,
  })
  const placeable = registry.filter((r) =>
    (r.kind === 'Native' ? CONTROLLABLE_NATIVE.includes(r.fieldKey) : true)
    && !fields.some((f) => f.fieldKey === r.fieldKey))

  const save = useMutation({
    mutationFn: async () => {
      await updateEntryForm(form.id, { name, recordType: 'Requisition', fields: fields.map((f, i) => ({ ...f, sort: i })) })
      await assignEntryFormRoles(form.id, roles)
    },
    onSuccess: () => { setError(null); onChanged() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the form.'),
  })
  const toggleActive = useMutation({
    mutationFn: () => setEntryFormActive(form.id, !form.active),
    onSuccess: onChanged,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not update the form.'),
  })
  const remove = useMutation({
    mutationFn: () => deleteEntryForm(form.id),
    onSuccess: onDeleted,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not delete the form.'),
  })

  const set = (i: number, patch: Partial<EntryFormFieldDto>) =>
    setFields((fs) => fs.map((f, x) => (x === i ? { ...f, ...patch } : f)))
  const move = (i: number, d: -1 | 1) =>
    setFields((fs) => {
      const j = i + d
      if (j < 0 || j >= fs.length) return fs
      const next = [...fs]
      ;[next[i], next[j]] = [next[j], next[i]]
      return next
    })

  const ro = form.isSystem
  return (
    <div>
      <div className="chead" style={{ paddingLeft: 0 }}>
        <h3>{form.name} <span className="mono hint" style={{ fontWeight: 400 }}>{form.code}</span></h3>
        <div className="spacer" />
        {ro
          ? <span className="badge b-blue">Standard — the parity baseline, read-only</span>
          : (
            <>
              <Button variant="ghost" size="sm" onClick={() => toggleActive.mutate()} ariaLabel={form.active ? 'Deactivate form' : 'Reactivate form'}>
                {form.active ? 'Deactivate' : 'Reactivate'}
              </Button>
              <Button variant="ghost" size="sm" red onClick={() => remove.mutate()} ariaLabel={`Delete ${form.name}`}>Delete</Button>
              <Button variant="primary" size="sm" busy={save.isPending} onClick={() => save.mutate()} ariaLabel="Save form">Save form</Button>
            </>
          )}
      </div>
      {error && <Notice tone="error">{error}</Notice>}

      {!ro && <TextField spec={spec('ef-name', 'Form name', 'text')} value={name} onChange={(v) => setName(String(v ?? ''))} />}

      <h4>Fields ({fields.length})</h4>
      <table>
        <thead><tr><th>Field</th><th>Group</th><th>Subtab</th><th>Display</th><th>Required</th><th>Default</th><th /></tr></thead>
        <tbody>
          {fields.map((f, i) => (
            <tr key={f.fieldKey}
              draggable={!ro}
              onDragStart={(e) => e.dataTransfer.setData('text/field-key', f.fieldKey)}
              style={!ro ? { cursor: 'grab' } : undefined}>
              <td className="mono">{f.fieldKey}{f.label ? ` · ${f.label}` : ''}{f.fullWidth ? ' · full-width' : ''}</td>
              <td>{ro ? f.fieldGroup : <TextField chrome="bare" spec={spec(`ef-${i}-group`, `${f.fieldKey} group`, 'text')} value={f.fieldGroup} onChange={(v) => set(i, { fieldGroup: String(v ?? '') })} />}</td>
              <td>{ro ? f.subtab ?? '—' : <TextField chrome="bare" spec={{ ...spec(`ef-${i}-subtab`, `${f.fieldKey} subtab`, 'text'), placeholder: 'main body' }} value={f.subtab ?? ''} onChange={(v) => set(i, { subtab: String(v ?? '') || null })} />}</td>
              <td>{ro ? f.displayType : <SelectField chrome="bare" spec={spec(`ef-${i}-display`, `${f.fieldKey} display`, 'select', DISPLAY_TYPES)} value={f.displayType} onChange={(v) => set(i, { displayType: String(v ?? 'Normal') })} />}</td>
              <td>{ro ? (f.requiredOnForm ? '✓' : '—') : (
                <CheckboxField chrome="bare" spec={spec(`ef-${i}-req`, `${f.fieldKey} required at submit`, 'boolean')} value={f.requiredOnForm} onChange={(v) => set(i, { requiredOnForm: v === true })} />
              )}</td>
              <td>{ro ? f.defaultValue ?? '—' : <TextField chrome="bare" spec={{ ...spec(`ef-${i}-default`, `${f.fieldKey} default`, 'text'), placeholder: '@today+7d …' }} value={f.defaultValue ?? ''} onChange={(v) => set(i, { defaultValue: String(v ?? '') || null })} />}</td>
              <td className="amt">
                {!ro && (
                  <>
                    <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${f.fieldKey} up`} onClick={() => move(i, -1)}>↑</button>
                    <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${f.fieldKey} down`} onClick={() => move(i, 1)}>↓</button>
                    <button type="button" className="btn btn-sm btn-out" aria-label={`Remove ${f.fieldKey}`} onClick={() => setFields((fs) => fs.filter((_, x) => x !== i))}>✕</button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {!ro && (
        <SelectField
          spec={{
            key: 'ef-add', label: 'Add field (from the registry — native, custom and segment kinds)', dataType: 'select',
            options: {
              kind: 'static',
              options: [
                { label: 'Fields', options: placeable.filter((r) => r.kind === 'Native').map((r) => ({ code: r.fieldKey, label: r.label })) },
                { label: 'Custom fields', options: placeable.filter((r) => r.kind === 'Custom').map((r) => ({ code: r.fieldKey, label: r.label })) },
                { label: 'Segments', options: placeable.filter((r) => r.kind === 'Segment').map((r) => ({ code: r.fieldKey, label: r.label })) },
              ].filter((g) => g.options.length > 0),
            },
          }}
          value={addKey}
          onChange={(v) => {
            const k = String(v ?? '')
            if (!k) return
            setFields((fs) => [...fs, {
              fieldKey: k, subtab: null, fieldGroup: 'Header', sort: fs.length,
              displayType: 'Normal', requiredOnForm: false, defaultValue: null,
              sourceFieldKey: null, fullWidth: false, label: null, placeholder: null,
            }])
            setAddKey('')
          }}
        />
      )}

      {!ro && <LayoutPanel form={form} fields={fields} onChanged={onChanged} onError={setError} />}

      <h4 style={{ marginTop: 18 }}>Preferred for roles</h4>
      {ro
        ? <p className="hint">The Standard form is the fallback for every role without a preferred form — it is not assigned directly.</p>
        : (
          <div className="grid g3">
            {ROLES.map((r) => (
              <CheckboxField key={r} spec={spec(`ef-role-${r}`, r, 'boolean')} value={roles.includes(r)}
                onChange={(v) => setRoles((cur) => (v === true ? [...cur, r] : cur.filter((x) => x !== r)))} />
            ))}
          </div>
        )}
      <p className="hint" style={{ marginTop: 10 }}>
        Resolution follows a fixed global role precedence (Buyer first), never a user record's
        role order; roles without a preferred form get the Standard form. Required applies at
        SUBMIT only — drafts always save. Hidden means hidden, not forbidden: the server's
        role matrix is unchanged by form layout.
      </p>
    </div>
  )
}

/**
 * CF5-T2..T5: the layout objects panel. Subtabs are chips (add / hide with a required-field
 * warning / delete-guarded) that double as DRAG TARGETS — drop a field row from the table
 * onto a chip (or Body) to move it there; the move persists immediately through the same
 * whole-form save the composer uses. Groups list their column-break toggle (T3).
 */
function LayoutPanel({ form, fields, onChanged, onError }: {
  form: EntryFormDefDto
  fields: EntryFormFieldDto[]
  onChanged: () => void
  onError: (msg: string | null) => void
}) {
  const [newSubtab, setNewSubtab] = useState('')
  const subtabs = form.subtabs ?? []
  const groups = form.groups ?? []
  const fail = (e: unknown) => onError(e instanceof Error ? e.message : 'Layout change failed.')
  const ok = () => { onError(null); onChanged() }

  const saveFields = (next: EntryFormFieldDto[]) =>
    updateEntryForm(form.id, { name: form.name, recordType: form.recordType, fields: next.map((x, i) => ({ ...x, sort: i })) })

  const dropField = (subtabName: string | null) => (e: React.DragEvent) => {
    e.preventDefault()
    const key = e.dataTransfer.getData('text/field-key')
    if (!key) return
    void saveFields(fields.map((f) => (f.fieldKey === key ? { ...f, subtab: subtabName } : f))).then(ok, fail)
  }

  const toggleHidden = (id: string, name: string, sort: number, hidden: boolean) => {
    if (!hidden) return void saveEntryFormSubtab(form.id, { name, sort, hidden }, id).then(ok, fail)
    const requiredHere = fields.filter((f) => f.subtab === name && f.requiredOnForm).map((f) => f.fieldKey)
    if (requiredHere.length > 0 && !window.confirm(
      `Subtab “${name}” holds required field(s): ${requiredHere.join(', ')}. Hidden fields STILL gate submit — users of this form must fill them elsewhere. Hide anyway?`))
      return
    void saveEntryFormSubtab(form.id, { name, sort, hidden }, id).then(ok, fail)
  }

  return (
    <div style={{ marginTop: 14 }}>
      <h4>Subtabs</h4>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
        <span className="badge b-grey" onDragOver={(e) => e.preventDefault()} onDrop={dropField(null)}
          style={{ padding: '6px 10px' }} aria-label="Body drop target">Body (drop here)</span>
        {subtabs.map((s) => (
          <span key={s.id} className={`badge ${s.hidden ? 'b-grey' : 'b-blue'}`}
            onDragOver={(e) => e.preventDefault()} onDrop={dropField(s.name)}
            style={{ padding: '6px 10px', display: 'inline-flex', gap: 6, alignItems: 'center' }}
            aria-label={`Subtab ${s.name}`}>
            {s.name}{s.hidden ? ' (hidden)' : ''}
            <button type="button" className="lnk" aria-label={`${s.hidden ? 'Show' : 'Hide'} subtab ${s.name}`}
              onClick={() => toggleHidden(s.id, s.name, s.sort, !s.hidden)}>{s.hidden ? 'show' : 'hide'}</button>
            <button type="button" className="lnk" aria-label={`Delete subtab ${s.name}`}
              onClick={() => void deleteEntryFormSubtab(form.id, s.id).then(ok, fail)}>✕</button>
          </span>
        ))}
        <div style={{ width: 170 }}>
          <TextField chrome="bare" spec={{ ...spec('cf5-new-subtab', 'New subtab name', 'text'), placeholder: 'New subtab name' }}
            value={newSubtab} onChange={(v) => setNewSubtab(String(v ?? ''))} />
        </div>
        <Button variant="outline" size="sm" ariaLabel="Add subtab" onClick={() => {
          if (!newSubtab.trim()) return
          void saveEntryFormSubtab(form.id, { name: newSubtab.trim(), sort: subtabs.length, hidden: false })
            .then(() => { setNewSubtab(''); ok() }, fail)
        }}>Add subtab</Button>
      </div>
      <p className="hint">Drag a field row onto a subtab (or Body) to move it. Hiding a subtab hides its fields from the form — required fields still gate submit.</p>

      <h4 style={{ marginTop: 12 }}>Field groups</h4>
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
        {groups.map((g) => {
          const subtabName = subtabs.find((s) => s.id === g.subtabId)?.name
          const empty = !fields.some((f) => (f.fieldGroup || 'Header') === g.title && (f.subtab ?? null) === (subtabName ?? null))
          return (
            <span key={g.id} className="badge b-grey" style={{ padding: '6px 10px', display: 'inline-flex', gap: 6, alignItems: 'center' }}>
              {g.title}{subtabName ? ` · ${subtabName}` : ''}
              <CheckboxField chrome="bare" spec={spec(`cf5-break-${g.id}`, `Column break at ${g.title}`, 'boolean')}
                value={g.columnBreak}
                onChange={(v) => void saveEntryFormGroup(form.id,
                  { title: g.title, subtabId: g.subtabId ?? null, sort: g.sort, columnBreak: v === true }, g.id).then(ok, fail)} />
              {empty && (
                <button type="button" className="lnk" aria-label={`Delete group ${g.title}`}
                  onClick={() => void deleteEntryFormGroup(form.id, g.id).then(ok, fail)}>✕</button>
              )}
            </span>
          )
        })}
        {groups.length === 0 && <span className="hint">Groups appear as you name them on fields.</span>}
      </div>
    </div>
  )
}

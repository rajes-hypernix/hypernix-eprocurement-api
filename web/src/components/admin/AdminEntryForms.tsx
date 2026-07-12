import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getEntryForms, createEntryForm, updateEntryForm, deleteEntryForm, assignEntryFormRoles,
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
  const form = forms.find((f) => f.id === selected) ?? forms[0]
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['entry-form-defs'] }); void qc.invalidateQueries({ queryKey: ['entry-form'] }) }

  const copy = useMutation({
    mutationFn: (source: EntryFormDefDto) => createEntryForm({
      name: `${source.name} (copy)`, recordType: 'Requisition', fields: source.fields,
    }),
    onSuccess: (d) => { setSelected(d.id); refresh() },
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
      detail={form
        ? <FormComposer key={form.id} form={form} onChanged={refresh} onDeleted={() => { setSelected(null); refresh() }} />
        : <p className="hint">No entry forms yet.</p>}
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
            <tr key={f.fieldKey}>
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

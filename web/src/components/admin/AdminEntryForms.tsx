import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getEntryForms, createEntryForm, updateEntryForm, deleteEntryForm, assignEntryFormRoles, setEntryFormActive,
  saveEntryFormSubtab, deleteEntryFormSubtab, saveEntryFormGroup, deleteEntryFormGroup,
  moveEntryFormField, saveEntryFormSublist,
  getViewFields, type EntryFormDefDto, type EntryFormFieldDto, type EntryFormGroupDto,
} from '../../api/client'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { recordTypeLabel } from '../../lib/recordTypeLabel'

/**
 * Entry-form designer (D7 → CF-FIX4-T3 redesign). Body fields render INSIDE their
 * field-group cards (the placement object made visible); dragging a field row onto
 * another group re-parents it — ONE surgical write to the SAME EntryFormField row the
 * creation cascade authors (L1). Groups and subtabs are managed on screen (Header is
 * pinned per L3 — the server enforces; the UI just doesn't offer the knife). The item
 * sublist is a FLAT column order (L4 — no groups on sublists). Field property edits
 * (display/required/default) stage locally and commit on Save form; STRUCTURE moves
 * (drag, group/subtab CRUD, sublist order) persist immediately.
 */

const ROLES = ['Buyer', 'Approver', 'TechEvaluator', 'CommEvaluator', 'Admin']
const DISPLAY_TYPES = ['Normal', 'Disabled', 'ReadOnly', 'Hidden']
// Mirror of the server's EntryFormVocabulary (display gating only — the server enforces).
const CONTROLLABLE_NATIVE: Record<string, string[]> = {
  Requisition: ['Requestor', 'Department', 'Location', 'Category', 'Job', 'Memo', 'RequiredDate'],
  PurchaseOrder: [],
  Grn: [],
  Invoice: ['InvoiceNo'],
}
const SUBLIST_LABELS: Record<string, string> = {
  ItemCode: 'Item code', Description: 'Description', Qty: 'Qty', Uom: 'UoM', EstUnitPrice: 'Est. rate',
}

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

export function AdminEntryForms() {
  const qc = useQueryClient()
  const { data: forms = [] } = useQuery({ queryKey: ['entry-form-defs'], queryFn: () => getEntryForms() })
  const [selected, setSelected] = useState<string | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const form = forms.find((f) => f.id === selected) ?? forms[0]
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['entry-form-defs'] }); void qc.invalidateQueries({ queryKey: ['entry-form'] }) }

  const copy = useMutation({
    mutationFn: (source: EntryFormDefDto) => createEntryForm({
      name: `${source.name} (copy)`, recordType: source.recordType, fields: source.fields,
    }),
    onSuccess: (d) => { setSelected(d.id); setListError(null); refresh() },
    onError: (e) => setListError(e instanceof Error ? e.message : 'Could not copy the form.'),
  })

  return (
    <SetupPage
      title="Entry Forms"
      subtitle="Layouts per record type — field groups, subtabs, the item sublist, display types, required-at-submit, defaults. Zero deployments."
      primaryAction={form && (
        <Button variant="primary" size="sm" icon="plus" busy={copy.isPending} onClick={() => copy.mutate(form)}>
          New form (copy of {form.name})
        </Button>
      )}
      railItems={forms.map((f) => ({
        key: f.id, label: f.name,
        hint: `${recordTypeLabel(f.recordType)}${f.isSystem ? ' · standard' : f.roles.length > 0 ? ` · ${f.roles.join(', ')}` : ''}`,
      }))}
      selectedKey={form?.id ?? ''}
      onSelect={setSelected}
      detail={
        <>
          {listError && <Notice tone="error">{listError}</Notice>}
          {form
            ? <FormDesigner key={form.id} form={form} onChanged={refresh} onDeleted={() => { setSelected(null); refresh() }} />
            : <p className="hint">No entry forms yet.</p>}
        </>}
    />
  )
}

function FormDesigner({ form, onChanged, onDeleted }: {
  form: EntryFormDefDto; onChanged: () => void; onDeleted: () => void
}) {
  const [name, setName] = useState(form.name)
  const [fields, setFields] = useState<EntryFormFieldDto[]>(form.fields)
  const [roles, setRoles] = useState<string[]>(form.roles)
  const [error, setError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<string | null>(null)   // null = Body; else subtab id
  // STAGED field edits (props, adds, removes) must SURVIVE the refetches that immediate
  // structure writes (drag, group/subtab CRUD) trigger — reconcile on every form refresh.
  const staged = useRef<{ added: EntryFormFieldDto[]; removed: Set<string>; patched: Map<string, Partial<EntryFormFieldDto>> }>(
    { added: [], removed: new Set(), patched: new Map() })
  const nameEdited = useRef(false)
  const rolesEdited = useRef(false)
  useEffect(() => {
    const s = staged.current
    if (!nameEdited.current) setName(form.name)
    if (!rolesEdited.current) setRoles(form.roles)
    setFields([
      ...form.fields.filter((f) => !s.removed.has(f.fieldKey)).map((f) => ({ ...f, ...(s.patched.get(f.fieldKey) ?? {}) })),
      ...s.added.filter((a) => !form.fields.some((f) => f.fieldKey === a.fieldKey)),
    ])
  }, [form])

  const { data: registry = [] } = useQuery({
    queryKey: ['view-fields', form.recordType], queryFn: () => getViewFields(form.recordType), staleTime: Infinity,
  })
  const controllable = CONTROLLABLE_NATIVE[form.recordType] ?? []
  const placeable = registry.filter((r) =>
    (r.kind === 'Native' ? controllable.includes(r.fieldKey) : true)
    && !fields.some((f) => f.fieldKey === r.fieldKey))

  const fail = (e: unknown) => setError(e instanceof Error ? e.message : 'Layout change failed.')
  const ok = () => { setError(null); onChanged() }

  const save = useMutation({
    mutationFn: async () => {
      await updateEntryForm(form.id, { name, recordType: form.recordType, fields: fields.map((f, i) => ({ ...f, sort: i })) })
      await assignEntryFormRoles(form.id, roles)
    },
    onSuccess: () => { staged.current = { added: [], removed: new Set(), patched: new Map() }; nameEdited.current = false; rolesEdited.current = false; ok() },
    onError: fail,
  })
  const toggleActive = useMutation({
    mutationFn: () => setEntryFormActive(form.id, !form.active), onSuccess: onChanged, onError: fail,
  })
  const remove = useMutation({ mutationFn: () => deleteEntryForm(form.id), onSuccess: onDeleted, onError: fail })

  const subtabs = [...(form.subtabs ?? [])].sort((a, b) => a.sort - b.sort || a.name.localeCompare(b.name))
  const groups = [...(form.groups ?? [])].sort((a, b) => a.sort - b.sort || a.title.localeCompare(b.title))
  const tabGroups = groups.filter((g) => (g.subtabId ?? null) === activeTab)
  const activeSubtab = subtabs.find((s) => s.id === activeTab)

  const setField = (key: string, patch: Partial<EntryFormFieldDto>) => {
    const s = staged.current
    s.patched.set(key, { ...(s.patched.get(key) ?? {}), ...patch })
    setFields((fs) => fs.map((f) => (f.fieldKey === key ? { ...f, ...patch } : f)))
  }
  const removeField = (key: string) => {
    const s = staged.current
    if (s.added.some((a) => a.fieldKey === key)) s.added = s.added.filter((a) => a.fieldKey !== key)
    else s.removed.add(key)
    setFields((fs) => fs.filter((f) => f.fieldKey !== key))
  }

  // THE drag-drop write (L1): re-point the shared placement row; persists immediately.
  const dropOnGroup = (g: EntryFormGroupDto) => (e: React.DragEvent) => {
    e.preventDefault()
    const key = e.dataTransfer.getData('text/field-key')
    if (!key) return
    // Sort is the GLOBAL form order — append the moved field after everything so the
    // target group's screen position (group Sort) decides where it renders, not the field.
    const nextSort = Math.max(0, ...fields.map((f) => f.sort)) + 1
    void moveEntryFormField(form.id, key, g.id, nextSort).then(ok, fail)
  }
  // Dropping on a subtab chip: land in that container's FIRST group (create "Header"-titled
  // container group via the string seam if the subtab is empty).
  const dropOnSubtab = (subtabName: string | null) => (e: React.DragEvent) => {
    e.preventDefault()
    const key = e.dataTransfer.getData('text/field-key')
    if (!key) return
    void updateEntryForm(form.id, {
      name: form.name, recordType: form.recordType,
      fields: fields.map((f, i) => ({ ...f, sort: i, ...(f.fieldKey === key ? { subtab: subtabName } : {}) })),
    }).then(ok, fail)
  }

  const ro = form.isSystem
  return (
    <div>
      <div className="chead" style={{ paddingLeft: 0 }}>
        <h3>{form.name} <span className="mono hint" style={{ fontWeight: 400 }}>{form.code}</span>
          <span className="badge b-grey" style={{ marginLeft: 8 }}>{recordTypeLabel(form.recordType)}</span>
        </h3>
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
      {!ro && <TextField spec={spec('ef-name', 'Form name', 'text')} value={name} onChange={(v) => { nameEdited.current = true; setName(String(v ?? '')) }} />}

      {/* ---- Subtab bar: Body + each subtab; chips are drop targets and manage-verbs ---- */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center', marginTop: 10 }}>
        <button type="button" className={`btn btn-sm ${activeTab === null ? 'btn-pri' : 'btn-out'}`}
          onDragOver={(e) => e.preventDefault()} onDrop={dropOnSubtab(null)}
          onClick={() => setActiveTab(null)} aria-label="Body tab">Body</button>
        {subtabs.map((s) => (
          <button key={s.id} type="button" className={`btn btn-sm ${activeTab === s.id ? 'btn-pri' : 'btn-out'}`}
            onDragOver={(e) => e.preventDefault()} onDrop={dropOnSubtab(s.name)}
            onClick={() => setActiveTab(s.id)} aria-label={`Subtab ${s.name}`}>
            {s.name}{s.hidden ? ' (hidden)' : ''}
          </button>
        ))}
        {!ro && <NewSubtab form={form} count={subtabs.length} onOk={ok} onFail={fail} />}
      </div>
      {activeSubtab && !ro && (
        <div style={{ marginTop: 6, display: 'flex', gap: 10, alignItems: 'center' }}>
          <SubtabVerbs form={form} subtab={activeSubtab} fields={fields} onOk={() => { setActiveTab(null); ok() }} onToggled={ok} onFail={fail} />
        </div>
      )}

      {/* ---- Group cards for the active container ---- */}
      <div style={{ marginTop: 12, display: 'flex', flexDirection: 'column', gap: 12 }}>
        {tabGroups.map((g) => (
          <GroupCard key={g.id} form={form} group={g} ro={ro}
            fields={fields.filter((f) => f.groupId === g.id
              // A STAGED (not yet saved) field has no groupId — place it by its string
              // placement so it is visible and draggable before the first save.
              || (!f.groupId
                && (f.subtab ?? null) === (subtabs.find((s) => s.id === g.subtabId)?.name ?? null)
                && (f.fieldGroup || 'Header') === g.title)).sort((a, b) => a.sort - b.sort)}
            onDrop={dropOnGroup(g)} onFieldChange={setField}
            onRemoveField={removeField}
            onOk={ok} onFail={fail} />
        ))}
        {tabGroups.length === 0 && <p className="hint">No field groups in this container yet.</p>}
        {!ro && <NewGroup form={form} subtabId={activeTab} count={tabGroups.length} onOk={ok} onFail={fail} />}
      </div>

      {/* ---- Add field (standardized searchable picker) ---- */}
      {!ro && (
        <div style={{ marginTop: 12, maxWidth: 460 }}>
          <SelectField
            spec={{
              key: 'ef-add', label: 'Add field', dataType: 'select', searchable: true,
              help: 'From the registry — native, custom and segment kinds. Lands in Header; drag it to its group.',
              options: {
                kind: 'static',
                options: [
                  ...placeable.filter((r) => r.kind === 'Native').map((r) => ({ code: r.fieldKey, label: `${r.label} (native)` })),
                  ...placeable.filter((r) => r.kind === 'Custom').map((r) => ({ code: r.fieldKey, label: `${r.label} (custom)` })),
                  ...placeable.filter((r) => r.kind === 'Segment').map((r) => ({ code: r.fieldKey, label: `${r.label} (segment)` })),
                ],
              },
            }}
            value={''}
            onChange={(v) => {
              const k = String(v ?? '')
              if (!k) return
              const added: EntryFormFieldDto = {
                fieldKey: k, subtab: null, fieldGroup: 'Header', sort: fields.length,
                displayType: 'Normal', requiredOnForm: false, defaultValue: null,
                sourceFieldKey: null, fullWidth: false, label: null, placeholder: null,
              }
              staged.current.added = [...staged.current.added, added]
              setFields((fs) => [...fs, added])
            }}
          />
          <p className="hint">Property edits and added/removed fields commit on Save form; drags and group/subtab changes save immediately.</p>
        </div>
      )}

      {/* ---- Item sublist (L4: flat, rearrangeable, NO groups) ---- */}
      {(form.sublistColumns?.length ?? 0) > 0 && (
        <SublistPanel form={form} ro={ro} onOk={ok} onFail={fail} />
      )}

      <h4 style={{ marginTop: 18 }}>Preferred for roles</h4>
      {ro
        ? <p className="hint">The Standard form is the fallback for every role without a preferred form — it is not assigned directly.</p>
        : (
          <div className="grid g3">
            {ROLES.map((r) => (
              <CheckboxField key={r} spec={spec(`ef-role-${r}`, r, 'boolean')} value={roles.includes(r)}
                onChange={(v) => { rolesEdited.current = true; setRoles((cur) => (v === true ? [...cur, r] : cur.filter((x) => x !== r))) }} />
            ))}
          </div>
        )}
      <p className="hint" style={{ marginTop: 10 }}>
        Resolution follows a fixed global role precedence (Buyer first), never a user record's
        role order; roles without a preferred form get the Standard form. Required applies at
        SUBMIT only — drafts always save. Hidden means hidden, not forbidden: the server's
        role matrix is unchanged by form layout. Removing a field from a form is a LAYOUT
        change — stored values are never touched (use the field's lifecycle to retire data).
      </p>
    </div>
  )
}

function GroupCard({ form, group, ro, fields, onDrop, onFieldChange, onRemoveField, onOk, onFail }: {
  form: EntryFormDefDto; group: EntryFormGroupDto; ro: boolean
  fields: EntryFormFieldDto[]
  onDrop: (e: React.DragEvent) => void
  onFieldChange: (key: string, patch: Partial<EntryFormFieldDto>) => void
  onRemoveField: (key: string) => void
  onOk: () => void; onFail: (e: unknown) => void
}) {
  const [title, setTitle] = useState(group.title)
  useEffect(() => setTitle(group.title), [group.title])
  const renameBlocked = ro   // Header rename allowed on non-system forms (L3); system forms are wholly read-only

  return (
    <div className="card" style={{ padding: 12 }} onDragOver={(e) => e.preventDefault()} onDrop={onDrop}
      aria-label={`Field group ${group.title}`}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
        {renameBlocked
          ? <b>{group.title}</b>
          : (
            <span style={{ maxWidth: 220 }}>
              <TextField chrome="bare" spec={spec(`g-title-${group.id}`, `Group ${group.title} title`, 'text')} value={title}
                onChange={(v) => setTitle(String(v ?? ''))} />
            </span>
          )}
        {group.isHeader && <span className="badge b-blue" title="Every form keeps its Header group — the guaranteed landing zone (L3)">Header — always present</span>}
        <div className="spacer" style={{ flex: 1 }} />
        {!ro && (
          <>
            {title.trim() !== group.title && (
              <Button variant="outline" size="sm" ariaLabel={`Rename group ${group.title}`}
                onClick={() => void saveEntryFormGroup(form.id,
                  { title: title.trim(), subtabId: group.subtabId ?? null, sort: group.sort, columnBreak: group.columnBreak }, group.id).then(onOk, onFail)}>
                Rename
              </Button>
            )}
            <CheckboxField chrome="bare" spec={spec(`g-break-${group.id}`, `Column break at ${group.title}`, 'boolean')}
              value={group.columnBreak}
              onChange={(v) => void saveEntryFormGroup(form.id,
                { title: group.title, subtabId: group.subtabId ?? null, sort: group.sort, columnBreak: v === true }, group.id).then(onOk, onFail)} />
            {!group.isHeader && fields.length === 0 && (
              <Button variant="ghost" size="sm" red ariaLabel={`Delete group ${group.title}`}
                onClick={() => void deleteEntryFormGroup(form.id, group.id).then(onOk, onFail)}>Delete group</Button>
            )}
          </>
        )}
      </div>
      {fields.length === 0 && <p className="hint" style={{ margin: 4 }}>Empty — drop a field here.</p>}
      {fields.length > 0 && (
        <table>
          <thead><tr><th>Field</th><th>Display</th><th>Required</th><th>Default</th><th /></tr></thead>
          <tbody>
            {fields.map((f) => (
              <tr key={f.fieldKey}
                draggable={!ro}
                onDragStart={(e) => e.dataTransfer.setData('text/field-key', f.fieldKey)}
                style={!ro ? { cursor: 'grab' } : undefined}
                aria-label={`Field row ${f.fieldKey}`}>
                <td className="mono">{f.fieldKey}{f.label ? ` · ${f.label}` : ''}{f.fullWidth ? ' · full-width' : ''}</td>
                <td>{ro ? f.displayType : <SelectField chrome="bare" spec={spec(`ef-${f.fieldKey}-display`, `${f.fieldKey} display`, 'select', DISPLAY_TYPES)} value={f.displayType} onChange={(v) => onFieldChange(f.fieldKey, { displayType: String(v ?? 'Normal') })} />}</td>
                <td>{ro ? (f.requiredOnForm ? '✓' : '—') : (
                  <CheckboxField chrome="bare" spec={spec(`ef-${f.fieldKey}-req`, `${f.fieldKey} required at submit`, 'boolean')} value={f.requiredOnForm} onChange={(v) => onFieldChange(f.fieldKey, { requiredOnForm: v === true })} />
                )}</td>
                <td>{ro ? f.defaultValue ?? '—' : <TextField chrome="bare" spec={{ ...spec(`ef-${f.fieldKey}-default`, `${f.fieldKey} default`, 'text'), placeholder: '@today+7d …' }} value={f.defaultValue ?? ''} onChange={(v) => onFieldChange(f.fieldKey, { defaultValue: String(v ?? '') || null })} />}</td>
                <td className="amt">
                  {!ro && (
                    <button type="button" className="btn btn-sm btn-out" aria-label={`Remove ${f.fieldKey}`}
                      title="Removes the PLACEMENT only — stored values are never touched (L6)"
                      onClick={() => onRemoveField(f.fieldKey)}>✕</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function NewGroup({ form, subtabId, count, onOk, onFail }: {
  form: EntryFormDefDto; subtabId: string | null; count: number
  onOk: () => void; onFail: (e: unknown) => void
}) {
  const [title, setTitle] = useState('')
  return (
    <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
      <div style={{ width: 200 }}>
        <TextField chrome="bare" spec={{ ...spec('ef-new-group', 'New group title', 'text'), placeholder: 'New group title' }}
          value={title} onChange={(v) => setTitle(String(v ?? ''))} />
      </div>
      <Button variant="outline" size="sm" icon="plus" ariaLabel="Add group" onClick={() => {
        if (!title.trim()) return
        void saveEntryFormGroup(form.id, { title: title.trim(), subtabId, sort: count + 1, columnBreak: false })
          .then(() => { setTitle(''); onOk() }, onFail)
      }}>Add group</Button>
    </div>
  )
}

function NewSubtab({ form, count, onOk, onFail }: {
  form: EntryFormDefDto; count: number; onOk: () => void; onFail: (e: unknown) => void
}) {
  const [name, setName] = useState('')
  return (
    <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
      <span style={{ width: 150 }}>
        <TextField chrome="bare" spec={{ ...spec('ef-new-subtab', 'New subtab name', 'text'), placeholder: 'New subtab name' }}
          value={name} onChange={(v) => setName(String(v ?? ''))} />
      </span>
      <Button variant="outline" size="sm" ariaLabel="Add subtab" onClick={() => {
        if (!name.trim()) return
        void saveEntryFormSubtab(form.id, { name: name.trim(), sort: count, hidden: false })
          .then(() => { setName(''); onOk() }, onFail)
      }}>Add subtab</Button>
    </span>
  )
}

function SubtabVerbs({ form, subtab, fields, onOk, onToggled, onFail }: {
  form: EntryFormDefDto
  subtab: { id: string; name: string; sort: number; hidden: boolean }
  fields: EntryFormFieldDto[]
  onOk: () => void; onToggled: () => void; onFail: (e: unknown) => void
}) {
  const toggleHidden = () => {
    if (!subtab.hidden) {
      const requiredHere = fields.filter((f) => f.subtab === subtab.name && f.requiredOnForm).map((f) => f.fieldKey)
      if (requiredHere.length > 0 && !window.confirm(
        `Subtab “${subtab.name}” holds required field(s): ${requiredHere.join(', ')}. Hidden fields STILL gate submit — users of this form must fill them elsewhere. Hide anyway?`))
        return
    }
    void saveEntryFormSubtab(form.id, { name: subtab.name, sort: subtab.sort, hidden: !subtab.hidden }, subtab.id).then(onToggled, onFail)
  }
  return (
    <>
      <button type="button" className="lnk" aria-label={`${subtab.hidden ? 'Show' : 'Hide'} subtab ${subtab.name}`} onClick={toggleHidden}>
        {subtab.hidden ? 'Show subtab' : 'Hide subtab'}
      </button>
      <button type="button" className="lnk" aria-label={`Delete subtab ${subtab.name}`}
        onClick={() => void deleteEntryFormSubtab(form.id, subtab.id).then(onOk, onFail)}>Delete subtab</button>
      <span className="hint">Hiding hides its fields from the form — required fields still gate submit.</span>
    </>
  )
}

/** L4: the item sublist — flat column ORDER, rearrangeable, deliberately group-free. */
function SublistPanel({ form, ro, onOk, onFail }: {
  form: EntryFormDefDto; ro: boolean; onOk: () => void; onFail: (e: unknown) => void
}) {
  const cols = form.sublistColumns ?? []
  const move = (i: number, d: -1 | 1) => {
    const j = i + d
    if (j < 0 || j >= cols.length) return
    const next = [...cols]
    ;[next[i], next[j]] = [next[j], next[i]]
    void saveEntryFormSublist(form.id, next).then(onOk, onFail)
  }
  return (
    <div style={{ marginTop: 16 }}>
      <h4>Item sublist</h4>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
        {cols.map((k, i) => (
          <span key={k} className="badge b-grey" style={{ padding: '6px 10px', display: 'inline-flex', gap: 6, alignItems: 'center' }}>
            {SUBLIST_LABELS[k] ?? k}
            {!ro && (
              <>
                <button type="button" className="lnk" aria-label={`Move ${k} left`} onClick={() => move(i, -1)}>←</button>
                <button type="button" className="lnk" aria-label={`Move ${k} right`} onClick={() => move(i, 1)}>→</button>
              </>
            )}
          </span>
        ))}
      </div>
      <p className="hint">Line columns are FLAT — rearrangeable, no field groups (ruled). Line-scope custom fields append after these.</p>
    </div>
  )
}

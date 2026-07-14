import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getEntryForms, createEntryForm, updateEntryForm, deleteEntryForm, assignEntryFormRoles, setEntryFormActive,
  saveEntryFormSubtab, deleteEntryFormSubtab, saveEntryFormGroup, deleteEntryFormGroup,
  saveEntryFormSublist, getLineCustomDefs,
  getViewFields, type EntryFormDefDto, type EntryFormFieldDto, type EntryFormGroupDto,
} from '../../api/client'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Modal, Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { CodeField } from '../../ui/CodeField'
import { SelectField } from '../../ui/SelectField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { recordTypeLabel } from '../../lib/recordTypeLabel'

/**
 * Entry-form designer (D7). Body fields render INSIDE their field-group cards (the
 * placement object made visible). CF-FIX5-T3: reorder is by up/down ARROWS only (drag
 * was removed — its drop position was architecturally uncaptured; arrows match NetSuite).
 * Within a group the arrow swaps neighbours; at a group edge it crosses into the adjacent
 * group of the same container (placement follows position, L1). Groups and subtabs are
 * managed on screen (Header is pinned per L3 — the server enforces). The item sublist is
 * a FLAT column order (L4). Property edits stage locally and commit on Save form;
 * structure moves (arrows, group/subtab CRUD, sublist order) persist immediately.
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
// CF-FIX5-T4: native line columns per record type (mirrors the server's SublistNativeColumns).
const NATIVE_LINE_COLS: Record<string, { code: string; label: string }[]> = {
  Requisition: [
    { code: 'ItemCode', label: 'Item code' }, { code: 'Description', label: 'Description' },
    { code: 'Qty', label: 'Qty' }, { code: 'Uom', label: 'UoM' }, { code: 'EstUnitPrice', label: 'Est. rate' },
  ],
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

  // CF-FIX5-T8: New form opens a modal to set the Name + Internal ID (customform_ affix).
  const [copying, setCopying] = useState<EntryFormDefDto | null>(null)

  return (
    <SetupPage
      title="Entry Forms"
      subtitle="Layouts per record type — field groups, subtabs, the item sublist, display types, required-at-submit, defaults. Zero deployments."
      primaryAction={form && (
        <Button variant="primary" size="sm" icon="plus" onClick={() => setCopying(form)}>
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
    >
      {copying && (
        <NewFormModal source={copying}
          onClose={() => setCopying(null)}
          onCreated={(d) => { setCopying(null); setSelected(d.id); setListError(null); refresh() }}
          onError={setListError} />
      )}
    </SetupPage>
  )
}

function NewFormModal({ source, onClose, onCreated, onError }: {
  source: EntryFormDefDto
  onClose: () => void
  onCreated: (d: EntryFormDefDto) => void
  onError: (msg: string | null) => void
}) {
  const [name, setName] = useState(`${source.name} (copy)`)
  const [code, setCode] = useState('')   // CF-FIX5-T8: user-input customform_ id (auto-derived if blank)
  const create = useMutation({
    mutationFn: () => createEntryForm({ name: name.trim(), code: code.trim() || null, recordType: source.recordType, fields: source.fields }),
    onSuccess: onCreated,
    onError: (e) => onError(e instanceof Error ? e.message : 'Could not create the form.'),
  })
  return (
    <Modal title="New entry form" icon="plus"
      footer={<>
        <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
        <button type="button" className="btn btn-pri" disabled={!name.trim() || create.isPending} onClick={() => create.mutate()}>Create form</button>
      </>}>
      <TextField spec={{ key: 'nf-name', label: 'Form name', dataType: 'text' }} value={name} onChange={(v) => setName(String(v ?? ''))} />
      <CodeField spec={{ key: 'nf-id', label: 'Internal ID', dataType: 'code', affix: 'customform_', placeholder: 'auto-derived from the name' }}
        value={code} onChange={setCode} />
    </Modal>
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
  // structure writes (arrows, group/subtab CRUD) trigger — reconcile on every form refresh.
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

  // T3-FIX(4): ALL placement moves persist through the whole-form save — the string→object
  // seam re-points the SAME EntryFormField row (L1) and, unlike the surgical endpoint,
  // works identically for STAGED (not-yet-saved) and persisted fields. Sorts normalize 0..n.
  const persistFields = (next: EntryFormFieldDto[]) => {
    setFields(next)
    void updateEntryForm(form.id, {
      name: form.name, recordType: form.recordType,
      fields: next.map((f, i) => ({ ...f, sort: i })),
    }).then(() => { staged.current = { added: [], removed: new Set(), patched: new Map() }; ok() }, fail)
  }
  const groupContainer = (g: EntryFormGroupDto) => subtabs.find((s) => s.id === g.subtabId)?.name ?? null
  /** Reposition `key` to the END of group `g` (array order = global Sort on save). Used by
   *  the arrows' cross-group step — drop-drag was REMOVED in CF-FIX5-T3 (its drop position
   *  was architecturally uncaptured and never fixed; the arrows are the sole reorder path,
   *  matching NetSuite's arrow buttons). */
  const moveIntoGroup = (key: string, g: EntryFormGroupDto) => {
    const mine = fields.find((f) => f.fieldKey === key)
    if (!mine) return
    const moved = { ...mine, fieldGroup: g.title, subtab: groupContainer(g), groupId: g.id }
    const rest = fields.filter((f) => f.fieldKey !== key)
    const lastIdx = rest.map((f) => f.groupId === g.id
      || (!f.groupId && (f.subtab ?? null) === groupContainer(g) && (f.fieldGroup || 'Header') === g.title)).lastIndexOf(true)
    // An EMPTY target group appends at the END — array order drives section order on the
    // rendered form, and a leading splice would hoist the group above Header.
    rest.splice(lastIdx >= 0 ? lastIdx + 1 : rest.length, 0, moved)
    persistFields(rest)
  }
  // CF-FIX5-T3: the always-works arrows are the SOLE reorder mechanism. Within a group: swap
  // with the neighbour. At a group edge: cross into the adjacent group of the SAME container
  // (placement follows position — the operator's cross-group requirement).
  const arrowMove = (key: string, dir: -1 | 1) => {
    const mine = fields.find((f) => f.fieldKey === key)
    if (!mine) return
    const g = tabGroups.find((x) => x.id === mine.groupId)
      ?? tabGroups.find((x) => x.title === (mine.fieldGroup || 'Header') && groupContainer(x) === (mine.subtab ?? null))
    if (!g) return
    const inGroup = (f: EntryFormFieldDto) => f.groupId === g.id
      || (!f.groupId && (f.subtab ?? null) === groupContainer(g) && (f.fieldGroup || 'Header') === g.title)
    const siblings = fields.filter(inGroup)
    const pos = siblings.findIndex((f) => f.fieldKey === key)
    const next = [...fields]
    const at = next.findIndex((f) => f.fieldKey === key)
    if (dir === -1 && pos > 0) {
      const swapWith = next.indexOf(siblings[pos - 1])
      ;[next[at], next[swapWith]] = [next[swapWith], next[at]]
      persistFields(next)
    } else if (dir === 1 && pos < siblings.length - 1) {
      const swapWith = next.indexOf(siblings[pos + 1])
      ;[next[at], next[swapWith]] = [next[swapWith], next[at]]
      persistFields(next)
    } else {
      const gi = tabGroups.indexOf(g)
      const target = tabGroups[gi + dir]
      if (target) moveIntoGroup(key, target)
    }
  }
  const groupArrow = (g: EntryFormGroupDto, dir: -1 | 1) => {
    const gi = tabGroups.indexOf(g)
    const other = tabGroups[gi + dir]
    if (!other) return
    void saveEntryFormGroup(form.id, { title: g.title, subtabId: g.subtabId ?? null, sort: other.sort, columnBreak: g.columnBreak }, g.id)
      .then(() => saveEntryFormGroup(form.id, { title: other.title, subtabId: other.subtabId ?? null, sort: g.sort, columnBreak: other.columnBreak }, other.id))
      .then(ok, fail)
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

      {/* ---- Subtab bar: Body + each subtab (container tabs + manage-verbs) ---- */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center', marginTop: 10 }}>
        <button type="button" className={`btn btn-sm ${activeTab === null ? 'btn-pri' : 'btn-out'}`}
          onClick={() => setActiveTab(null)} aria-label="Body tab">Body</button>
        {subtabs.map((s) => (
          <button key={s.id} type="button" className={`btn btn-sm ${activeTab === s.id ? 'btn-pri' : 'btn-out'}`}
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
              // placement so it is visible before the first save.
              || (!f.groupId
                && (f.subtab ?? null) === (subtabs.find((s) => s.id === g.subtabId)?.name ?? null)
                && (f.fieldGroup || 'Header') === g.title))}
            labelFor={(key) => registry.find((r) => r.fieldKey === key)?.label ?? key}
            onFieldChange={setField}
            onRemoveField={removeField}
            onArrow={arrowMove} onGroupArrow={(dir) => groupArrow(g, dir)}
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
              help: 'From the registry — native, custom and segment kinds. Lands in Header; use the arrows to move it.',
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
          <p className="hint">Property edits and added/removed fields commit on Save form; group/subtab changes save immediately.</p>
        </div>
      )}

      {/* ---- Item sublist (L4: flat, vertical, NO groups) ---- */}
      {((NATIVE_LINE_COLS[form.recordType]?.length ?? 0) > 0 || (form.sublistColumns?.length ?? 0) > 0) && (
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

function GroupCard({ form, group, ro, fields, labelFor, onFieldChange, onRemoveField, onArrow, onGroupArrow, onOk, onFail }: {
  form: EntryFormDefDto; group: EntryFormGroupDto; ro: boolean
  fields: EntryFormFieldDto[]
  labelFor: (key: string) => string
  onFieldChange: (key: string, patch: Partial<EntryFormFieldDto>) => void
  onRemoveField: (key: string) => void
  onArrow: (key: string, dir: -1 | 1) => void
  onGroupArrow: (dir: -1 | 1) => void
  onOk: () => void; onFail: (e: unknown) => void
}) {
  const [title, setTitle] = useState(group.title)
  useEffect(() => setTitle(group.title), [group.title])
  const renameBlocked = ro   // Header rename allowed on non-system forms (L3); system forms are wholly read-only

  return (
    <div className="card" style={{ padding: 12 }} aria-label={`Field group ${group.title}`}>
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
            <button type="button" className="btn btn-sm btn-out" aria-label={`Move group ${group.title} up`} onClick={() => onGroupArrow(-1)}>↑</button>
            <button type="button" className="btn btn-sm btn-out" aria-label={`Move group ${group.title} down`} onClick={() => onGroupArrow(1)}>↓</button>
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
      {fields.length === 0 && <p className="hint" style={{ margin: 4 }}>Empty group.</p>}
      {fields.length > 0 && (
        <table>
          <thead><tr><th>Field</th><th>Display</th><th>Required</th><th /></tr></thead>
          <tbody>
            {fields.map((f) => (
              <tr key={f.fieldKey} aria-label={`Field row ${f.fieldKey}`}>
                {/* T3-FIX(2): the NAME is the display; the internal id is secondary/muted. */}
                <td>
                  <span style={{ fontWeight: 600 }}>{f.label ?? labelFor(f.fieldKey)}</span>
                  {f.fullWidth ? <span className="hint"> · full-width</span> : null}
                  <div className="mono hint" style={{ fontSize: 11 }}>{f.fieldKey}</div>
                </td>
                <td>{ro ? f.displayType : <SelectField chrome="bare" spec={spec(`ef-${f.fieldKey}-display`, `${f.fieldKey} display`, 'select', DISPLAY_TYPES)} value={f.displayType} onChange={(v) => onFieldChange(f.fieldKey, { displayType: String(v ?? 'Normal') })} />}</td>
                <td>{ro ? (f.requiredOnForm ? '✓' : '—') : (
                  <CheckboxField chrome="bare" spec={spec(`ef-${f.fieldKey}-req`, `${f.fieldKey} required at submit`, 'boolean')} value={f.requiredOnForm} onChange={(v) => onFieldChange(f.fieldKey, { requiredOnForm: v === true })} />
                )}</td>
                <td className="amt">
                  {!ro && (
                    <>
                      <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${f.fieldKey} up`}
                        title="Up — crossing a group boundary re-groups the field" onClick={() => onArrow(f.fieldKey, -1)}>↑</button>
                      <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${f.fieldKey} down`}
                        title="Down — crossing a group boundary re-groups the field" onClick={() => onArrow(f.fieldKey, 1)}>↓</button>
                      <button type="button" className="btn btn-sm btn-out" aria-label={`Remove ${f.fieldKey}`}
                        title="Removes the PLACEMENT only — stored values are never touched (L6)"
                        onClick={() => onRemoveField(f.fieldKey)}>✕</button>
                    </>
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

/** CF-FIX5-T4 (L4): the item sublist — NetSuite Sublist-Fields style. A VERTICAL list,
 *  top = leftmost column on the line grid, reorder up/down, show/hide via a Show checkbox
 *  (membership = shown), and add applied custcol_ LINE fields as columns. Flat — no groups. */
function SublistPanel({ form, ro, onOk, onFail }: {
  form: EntryFormDefDto; ro: boolean; onOk: () => void; onFail: (e: unknown) => void
}) {
  const { data: lineDefs = [] } = useQuery({
    queryKey: ['line-defs', form.recordType], queryFn: () => getLineCustomDefs(form.recordType), staleTime: 60_000,
  })
  const shown = form.sublistColumns ?? []
  const natives = NATIVE_LINE_COLS[form.recordType] ?? []
  const labelOf = (k: string) =>
    natives.find((c) => c.code === k)?.label ?? lineDefs.find((d) => d.code === k)?.label ?? SUBLIST_LABELS[k] ?? k
  // Available to add = native + applied custcol_ line fields not already shown.
  const available = [...natives, ...lineDefs.map((d) => ({ code: d.code, label: d.label }))]
    .filter((c) => !shown.includes(c.code))
  const save = (next: string[]) => void saveEntryFormSublist(form.id, next).then(onOk, onFail)
  const move = (i: number, d: -1 | 1) => {
    const j = i + d
    if (j < 0 || j >= shown.length) return
    const n = [...shown]
    ;[n[i], n[j]] = [n[j], n[i]]
    save(n)
  }
  const custId = (k: string) => (k.startsWith('custcol_') ? <span className="mono hint" style={{ marginLeft: 6, fontSize: 11 }}>{k}</span> : null)
  return (
    <div style={{ marginTop: 16 }}>
      <h4>Item sublist</h4>
      <table>
        <thead><tr><th>Column (top = leftmost)</th><th>Show</th><th /></tr></thead>
        <tbody>
          {shown.map((k, i) => (
            <tr key={k} aria-label={`Sublist column ${k}`}>
              <td>{labelOf(k)}{custId(k)}</td>
              <td>{ro ? '✓' : <CheckboxField chrome="bare" spec={spec(`sublist-show-${k}`, `Show ${k}`, 'boolean')} value={true} onChange={() => save(shown.filter((x) => x !== k))} />}</td>
              <td className="amt">
                {!ro && (
                  <>
                    <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${k} up`} onClick={() => move(i, -1)}>↑</button>
                    <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${k} down`} onClick={() => move(i, 1)}>↓</button>
                  </>
                )}
              </td>
            </tr>
          ))}
          {!ro && available.map((c) => (
            <tr key={c.code} aria-label={`Available column ${c.code}`} style={{ opacity: 0.6 }}>
              <td>{c.label}{custId(c.code)}</td>
              <td><CheckboxField chrome="bare" spec={spec(`sublist-add-${c.code}`, `Show ${c.code}`, 'boolean')} value={false} onChange={() => save([...shown, c.code])} /></td>
              <td />
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

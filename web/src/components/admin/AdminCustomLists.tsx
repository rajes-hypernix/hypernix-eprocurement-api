import { useCallback, useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getCustomLists, createCustomList, addCustomListValue, updateCustomListValue, updateCustomList, setCustomListActive, deleteCustomList, deleteCustomListValue,
  getListValueReferences, purgeListValue,
  type CustomList, type CustomListValue,
} from '../../api/client'
import { ImpactReportDialog } from './ImpactReportDialog'
import { Modal, Notice, Spinner } from '../ui'
import type { FieldOption } from '../../ui/fieldSpec'
import { TextField } from '../../ui/TextField'
import { TextAreaField } from '../../ui/TextAreaField'
import { CodeField } from '../../ui/CodeField'
import { SelectField } from '../../ui/SelectField'
import { NumberField } from '../../ui/NumberField'
import { CheckboxField } from '../../ui/CheckboxField'
import { Button } from '../../ui/Button'
import { StatusBadge } from '../../ui/badges'
import { useNotify } from '../../ui/Notify'

/**
 * Custom Lists admin (NetSuite-style, VENDOR-ONBOARDING / DATA-MODEL §4). A list is a reusable coded
 * value set (COUNTRY, BANK, PAYMENT_TERMS…) that fields are tagged to; values store a CODE (source of
 * truth) and a LABEL (shown). Dependent lists (STATE→COUNTRY, CITY→STATE) scope each value to a parent
 * value. Editable here without a code change — the forms read the same lists live.
 * T6: the Entry Forms navigation shell — a LIST of value sets → Open → a dedicated FULL PAGE
 * editor (list hidden) with a Back control. New list rides as a modal off the list view.
 */
export function AdminCustomLists() {
  const qc = useQueryClient()
  const { data: lists = [], isPending } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })
  const [openCode, setOpenCode] = useState<string | null>(null)
  const [newList, setNewList] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  const refresh = () => { void qc.invalidateQueries({ queryKey: ['custom-lists'] }) }
  const onErr = (e: Error) => setErr(e.message)

  if (isPending) return <Spinner label="Loading custom lists…" />

  const opened = lists.find((l) => l.code === openCode) ?? null

  if (opened) {
    const parentList = opened.parentListCode ? lists.find((l) => l.code === opened.parentListCode) : undefined
    return <ListEditor list={opened} parentList={parentList} onBack={() => setOpenCode(null)} onRefresh={refresh}
      onDeleted={() => { setOpenCode(null); refresh() }} />
  }

  return (
    <div className="panel-fade">
      <div className="pagehead">
        <div><h1>Custom Lists</h1><p>Reusable value sets tagged to fields — like NetSuite Custom Lists.</p></div>
        <div className="spacer" />
        <Button variant="primary" icon="plus" onClick={() => setNewList(true)}>New list</Button>
      </div>
      {err && <Notice tone="error" icon="x">{err}</Notice>}
      <table>
        <thead><tr><th>List</th><th>Internal ID</th><th>Depends on</th><th>Values</th><th>Status</th><th /></tr></thead>
        <tbody>
          {lists.map((l) => (
            <tr key={l.code}>
              <td style={{ fontWeight: 600 }}><span>{l.name}</span>
                {l.orderMode === 'Alphabetical' && <span className="badge b-blue" style={{ marginLeft: 8 }}>A→Z</span>}
              </td>
              <td className="mono">{l.code}</td>
              <td className="hint">{l.parentListCode ? `↳ ${l.parentListCode}` : '—'}</td>
              <td className="amt">{l.values.length}</td>
              <td><StatusBadge tone={l.active === false ? 'grey' : 'green'}>{l.active === false ? 'Inactive' : 'Active'}</StatusBadge></td>
              <td className="amt">
                <div className="rowactions">
                  <Button variant="ghost" size="sm" onClick={() => setOpenCode(l.code)} ariaLabel={`Open ${l.name}`}>Open</Button>
                </div>
              </td>
            </tr>
          ))}
          {lists.length === 0 && <tr><td colSpan={6} className="hint">No lists yet.</td></tr>}
        </tbody>
      </table>
      {newList && <NewListModal lists={lists} onClose={() => setNewList(false)}
        onCreated={(code) => { setNewList(false); setOpenCode(code); void refresh() }} onErr={onErr} />}
    </div>
  )
}

function ListEditor({ list, parentList, onBack, onRefresh, onDeleted }: {
  list: CustomList; parentList?: CustomList; onBack: () => void; onRefresh: () => void; onDeleted: () => void
}) {
  const [editList, setEditList] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const onErr = (e: Error) => setErr(e.message)
  // Unsaved-changes guard (the Entry Forms pattern): staged, not-yet-committed values mark
  // the page dirty; Back warns before discarding them.
  const dirty = useRef(false)
  const onDirtyChange = useCallback((d: boolean) => { dirty.current = d }, [])
  const back = () => {
    if (!dirty.current || window.confirm('You have unsaved changes — all changes will be lost. Leave anyway?')) onBack()
  }

  const setListActive = useMutation({
    mutationFn: (v: { code: string; active: boolean }) => setCustomListActive(v.code, v.active),
    onSuccess: onRefresh, onError: onErr,
  })
  const removeList = useMutation({
    mutationFn: (code: string) => deleteCustomList(code),
    // A guarded delete DEACTIVATES (returns the list) — stay on the page so the admin SEES
    // the Inactive badge; only a hard delete (204/no body) returns to the list.
    onSuccess: (result) => { if (!result) onDeleted(); else onRefresh() }, onError: onErr,
  })

  return (
    <div className="panel-fade">
      <div className="chead" style={{ paddingLeft: 0 }}>
        <Button variant="ghost" size="sm" icon="back" onClick={back} ariaLabel="Back to lists">Back</Button>
        {/* CF1-T2: the list-SELF verbs the operator's complaint named. */}
        <h3 style={{ marginLeft: 8 }}>{list.name} <span className="mono hint" style={{ fontWeight: 400 }}>{list.code}</span>
          {list.active === false && <span className="badge b-grey" style={{ marginLeft: 8 }}>Inactive</span>}
          {list.orderMode === 'Alphabetical' && <span className="badge b-blue" style={{ marginLeft: 8 }}>A→Z</span>}
        </h3>
        <div className="spacer" />
        <Button variant="ghost" size="sm" icon="edit" onClick={() => setEditList(true)} ariaLabel="Edit list">Edit list</Button>
        {!list.isSystem && (
          <>
            <Button variant="ghost" size="sm"
              onClick={() => setListActive.mutate({ code: list.code, active: !(list.active ?? true) })}
              ariaLabel={list.active === false ? 'Reactivate list' : 'Deactivate list'}>
              {list.active === false ? 'Reactivate' : 'Deactivate'}
            </Button>
            <Button variant="ghost" size="sm" red onClick={() => removeList.mutate(list.code)} ariaLabel="Delete list">Delete</Button>
          </>
        )}
      </div>
      {err && <Notice tone="error" icon="x">{err}</Notice>}
      <ListValues list={list} parentList={parentList} onRefresh={onRefresh} onErr={onErr} clearErr={() => setErr(null)} onDirtyChange={onDirtyChange} />
      {editList && <EditListModal list={list} onClose={() => setEditList(false)}
        onSaved={() => { setEditList(false); onRefresh() }} onErr={onErr} />}
    </div>
  )
}

const parentOptions = (parentList: CustomList): FieldOption[] =>
  parentList.values.map((p) => ({ code: p.code, label: p.label }))

function ListValues({ list, parentList, onRefresh, onErr, clearErr, onDirtyChange }: {
  list: CustomList; parentList?: CustomList; onRefresh: () => void; onErr: (e: Error) => void; clearErr: () => void
  onDirtyChange: (dirty: boolean) => void
}) {
  const [label, setLabel] = useState('')
  const [parentValue, setParentValue] = useState('')
  const [editing, setEditing] = useState<CustomListValue | null>(null)
  // CF-FIX3-T4: the X no longer deletes (or silently deactivates) — it opens the impact
  // report, which offers only the tier the value is actually eligible for.
  const [impact, setImpact] = useState<CustomListValue | null>(null)
  // CF-FIX2-T5: values STAGE locally — add several, then ONE Save commits them all
  // (operator ruling: no auto-save). A staged value may parent another staged value
  // (referenced as staged:<idx>, resolved to the real system-assigned id on save).
  const [staged, setStaged] = useState<{ label: string; parent: string }[]>([])
  const notify = useNotify()
  useEffect(() => { onDirtyChange(staged.length > 0) }, [staged.length, onDirtyChange])

  const save = useMutation({
    mutationFn: async () => {
      const codes: string[] = []
      for (const s of staged) {
        const parent = s.parent.startsWith('staged:') ? codes[Number(s.parent.slice(7))] : (s.parent || null)
        const created = await addCustomListValue(list.code, { code: null, label: s.label, parentValueCode: parent ?? null })
        codes.push(created.code ?? '')
      }
    },
    onSuccess: () => { notify('List saved', { kind: 'toast' }); setStaged([]); onRefresh() }, onError: onErr,
  })
  const update = useMutation({
    mutationFn: (v: CustomListValue) => updateCustomListValue(v.id, { label: v.label, parentValueCode: v.parentValueCode, sort: v.sort, active: v.active }),
    onSuccess: () => { notify('List value saved', { kind: 'toast' }); setEditing(null); onRefresh() }, onError: onErr,
  })

  const parentLabel = (pc: string | null) => pc ? (parentList?.values.find((x) => x.code === pc)?.label ?? pc) : '—'
  const stagedParentLabel = (p: string) =>
    p.startsWith('staged:') ? `${staged[Number(p.slice(7))]?.label} (unsaved)` : p ? (list.values.find((x) => x.code === p)?.label ?? parentLabel(p)) : '—'
  const submitAdd = () => {
    clearErr()
    if (!label.trim()) { onErr(new Error('Enter a label.')); return }
    setStaged((s) => [...s, { label: label.trim(), parent: parentValue }])
    setLabel(''); setParentValue('')
  }

  return (
    <div className="card">
      <div className="chead">
        <h3>{list.name}</h3>
        {list.isSystem && <span className="badge b-grey" style={{ marginLeft: 8 }}>system</span>}
        <div className="spacer" style={{ flex: 1 }} />
        <span className="hint">{list.values.length} value{list.values.length === 1 ? '' : 's'}{parentList ? ` · depends on ${parentList.name}` : ''}</span>
      </div>
      <table>
        <thead>
          <tr><th>ID</th><th>Label</th>{parentList && <th>{parentList.name}</th>}{!parentList && <th>Parent</th>}<th>Active</th><th /></tr>
        </thead>
        <tbody>
          {[...list.values].sort((a, b) => a.sort - b.sort || a.label.localeCompare(b.label)).map((v) => (
            <tr key={v.id}>
              <td className="mono">{v.code}</td>
              <td style={{ fontWeight: 600 }}>{v.label}</td>
              {parentList && <td className="hint">{parentLabel(v.parentValueCode)}</td>}
              {!parentList && <td className="hint">{v.parentValueCode ? (list.values.find((x) => x.code === v.parentValueCode)?.label ?? v.parentValueCode) : '—'}</td>}
              <td><StatusBadge tone={v.active ? 'green' : 'grey'}>{v.active ? 'Active' : 'Hidden'}</StatusBadge></td>
              <td className="amt">
                <div className="rowactions">
                  <Button variant="outline" size="sm" icon="edit" onClick={() => setEditing(v)}>Edit</Button>
                  <Button variant="ghost" size="sm" icon="x" onClick={() => { clearErr(); setImpact(v) }} ariaLabel={`Delete ${v.code}`} />
                </div>
              </td>
            </tr>
          ))}
          {staged.map((s, i) => (
            <tr key={`staged-${i}`} style={{ background: 'var(--soft)' }}>
              <td className="mono hint">•</td>
              <td style={{ fontWeight: 600 }}>{s.label} <span className="badge b-grey">unsaved</span></td>
              {parentList && <td className="hint">{stagedParentLabel(s.parent)}</td>}
              {!parentList && <td className="hint">{stagedParentLabel(s.parent)}</td>}
              <td className="hint">—</td>
              <td className="amt">
                <Button variant="ghost" size="sm" icon="x" onClick={() => setStaged((x) => x.filter((_, j) => j !== i))} ariaLabel={`Discard staged ${s.label}`} />
              </td>
            </tr>
          ))}
          {list.values.length === 0 && staged.length === 0 && <tr><td colSpan={5}><span className="hint">No values yet — add the first below.</span></td></tr>}
        </tbody>
      </table>

      <div className="cbody" style={{ borderTop: '1px solid var(--line)' }}>
        <div className="hint" style={{ fontWeight: 700, marginBottom: 8 }}>Add a value</div>
        <div className="grid g3">
          <TextField spec={{ key: 'label', label: 'Label', dataType: 'text', placeholder: 'e.g. 30 days', help: 'The ID is assigned automatically (1, 2, 3… in entry order).' }} value={label} onChange={setLabel} />
          {parentList && (
            <SelectField
              spec={{ key: 'parentValue', label: parentList.name, dataType: 'select', placeholder: `Select ${parentList.name.toLowerCase()}`, options: { kind: 'static', options: parentOptions(parentList) } }}
              value={parentValue} onChange={setParentValue}
            />
          )}
          {!parentList && (list.values.length > 0 || staged.length > 0) && (
            <SelectField
              spec={{ key: 'parentValue', label: 'Parent (optional)', dataType: 'select', searchable: true, placeholder: 'None — top level',
                help: 'Build a tree: pick a previously-entered value of this list as the parent.',
                options: { kind: 'static', options: [
                  ...list.values.map((v) => ({ code: v.code, label: v.label })),
                  ...staged.map((s, i) => ({ code: `staged:${i}`, label: `${s.label} (unsaved)` })),
                ] } }}
              value={parentValue} onChange={setParentValue}
            />
          )}
        </div>
        <div style={{ marginTop: 12, display: 'flex', gap: 8, alignItems: 'center' }}>
          <Button variant="outline" size="sm" icon="plus" onClick={submitAdd}>Add value</Button>
          <Button variant="primary" size="sm" icon="check" busy={save.isPending} disabled={staged.length === 0}
            onClick={() => save.mutate()} ariaLabel="Save values">Save</Button>
          {staged.length > 0 && (
            <>
              <span className="hint">{staged.length} unsaved value{staged.length === 1 ? '' : 's'}</span>
              <Button variant="ghost" size="sm" onClick={() => setStaged([])} ariaLabel="Discard unsaved values">Cancel</Button>
            </>
          )}
        </div>
      </div>

      {impact && (
        <ImpactReportDialog
          title={`Delete value — ${impact.label} (${impact.code})`}
          kind="value"
          load={() => getListValueReferences(impact.id)}
          onDeactivate={impact.active
            ? () => updateCustomListValue(impact.id, { label: impact.label, parentValueCode: impact.parentValueCode, sort: impact.sort, active: false })
            : undefined}
          onDelete={() => deleteCustomListValue(impact.id)}
          onPurge={() => purgeListValue(impact.id)}
          onClose={(changed) => { setImpact(null); if (changed) onRefresh() }}
        />
      )}
      {editing && (
        <Modal title={`Edit — ${editing.code}`} icon="edit"
          footer={<>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button variant="primary" icon="check" busy={update.isPending} onClick={() => update.mutate(editing)}>Save</Button>
          </>}>
          <CodeField spec={{ key: 'ecode', label: 'ID', dataType: 'code', readOnly: true }} value={editing.code} onChange={() => {}} />
          <TextField spec={{ key: 'elabel', label: 'Label', dataType: 'text' }} value={editing.label} onChange={(v) => setEditing({ ...editing, label: v })} />
          {parentList && (
            <SelectField
              spec={{ key: 'eparent', label: parentList.name, dataType: 'select', placeholder: `Select ${parentList.name.toLowerCase()}`, options: { kind: 'static', options: parentOptions(parentList) } }}
              value={editing.parentValueCode ?? ''} onChange={(v) => setEditing({ ...editing, parentValueCode: v || null })}
            />
          )}
          {!parentList && (
            <SelectField
              spec={{ key: 'eparent', label: 'Parent (optional)', dataType: 'select', searchable: true, placeholder: 'None — top level',
                options: { kind: 'static', options: list.values.filter((x) => x.code !== editing.code).map((x) => ({ code: x.code, label: x.label })) } }}
              value={editing.parentValueCode ?? ''} onChange={(v) => setEditing({ ...editing, parentValueCode: v || null })}
            />
          )}
          <div className="grid g2">
            <NumberField spec={{ key: 'esort', label: 'Sort order', dataType: 'number' }} value={String(editing.sort)} onChange={(v) => setEditing({ ...editing, sort: Number(v) || 0 })} />
            <CheckboxField spec={{ key: 'eactive', label: 'Active (offered in dropdowns)', dataType: 'boolean' }} value={editing.active} onChange={(v) => setEditing({ ...editing, active: v })} />
          </div>
        </Modal>
      )}
    </div>
  )
}

function NewListModal({ lists, onClose, onCreated, onErr }: {
  lists: CustomList[]; onClose: () => void; onCreated: (code: string) => void; onErr: (e: Error) => void
}) {
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [parentListCode, setParentListCode] = useState('')
  const [orderMode, setOrderMode] = useState('Entered')   // CF-FIX1-T3: choosable at create
  const notify = useNotify()

  const create = useMutation({
    mutationFn: () => createCustomList({ code: code.trim().toUpperCase(), name: name.trim(), description: description.trim() || null, parentListCode: parentListCode || null, orderMode }),
    onSuccess: (l) => { notify('Custom list created', { kind: 'toast' }); onCreated(l.code) }, onError: onErr,
  })
  const submit = () => { if (!code.trim() || !name.trim()) { onErr(new Error('Enter both an Internal ID and a name.')); return } create.mutate() }

  return (
    <Modal title="New custom list" icon="plus"
      footer={<>
        <Button variant="outline" onClick={onClose}>Cancel</Button>
        <Button variant="primary" icon="check" busy={create.isPending} onClick={submit}>Create list</Button>
      </>}>
      <div className="grid g2">
        <CodeField spec={{ key: 'lcode', label: 'Internal ID', dataType: 'code', affix: 'CUSTLIST_', placeholder: 'e.g. INCOTERM' }} value={code} onChange={setCode} />
        <TextField spec={{ key: 'lname', label: 'Name', dataType: 'text', placeholder: 'e.g. Incoterms' }} value={name} onChange={setName} />
      </div>
      <TextField spec={{ key: 'ldesc', label: 'Description', dataType: 'text', placeholder: 'Optional' }} value={description} onChange={setDescription} />
      <SelectField
        spec={{
          key: 'lparent', label: 'Depends on (parent list)', dataType: 'select', searchable: true, placeholder: 'None — a flat list',
          help: 'A dependent list scopes each value to a parent value (e.g. City depends on State).',
          options: { kind: 'static', options: lists.map((l) => ({ code: l.code, label: l.name })) },
        }}
        value={parentListCode} onChange={setParentListCode}
      />
      <SelectField
        spec={{ key: 'lorder', label: 'Show options in', dataType: 'select', searchable: true,
          options: { kind: 'static', options: [
            { code: 'Entered', label: 'The order entered' }, { code: 'Alphabetical', label: 'Alphabetical order' },
          ] } }}
        value={orderMode} onChange={(v) => setOrderMode(String(v ?? 'Entered'))}
      />
    </Modal>
  )
}

function EditListModal({ list, onClose, onSaved, onErr }: {
  list: CustomList; onClose: () => void; onSaved: () => void; onErr: (e: Error) => void
}) {
  const [name, setName] = useState(list.name)
  const [desc, setDesc] = useState(list.description ?? '')
  const [orderMode, setOrderMode] = useState(list.orderMode ?? 'Entered')
  const notify = useNotify()
  const save = useMutation({
    mutationFn: () => updateCustomList(list.code, { name, description: desc || null, orderMode }),
    onSuccess: () => { notify('Custom list saved', { kind: 'toast' }); onSaved() }, onError: onErr,
  })
  return (
    <Modal
      title={`Edit list — ${list.name}`} icon="edit"
      footer={<>
        <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
        <button type="button" className="btn btn-pri" disabled={!name.trim() || save.isPending} onClick={() => save.mutate()}>Save list</button>
      </>}
    >
      <TextField spec={{ key: 'el-name', label: 'Name', dataType: 'text' }} value={name} onChange={(v) => setName(String(v ?? ''))} />
      <TextAreaField spec={{ key: 'el-desc', label: 'Description', dataType: 'longText' }} value={desc} onChange={(v) => setDesc(String(v ?? ''))} />
      <SelectField
        spec={{ key: 'el-order', label: 'Show options in', dataType: 'select', searchable: true, options: { kind: 'static', options: [
          { code: 'Entered', label: 'The order entered' }, { code: 'Alphabetical', label: 'Alphabetical order' },
        ] } }}
        value={orderMode} onChange={(v) => setOrderMode(String(v ?? 'Entered'))} />
      <p className="hint">The Internal ID (<span className="mono">{list.code}</span>) is immutable — fields are tagged to it.</p>
    </Modal>
  )
}

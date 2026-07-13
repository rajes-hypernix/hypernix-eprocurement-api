import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getCustomLists, createCustomList, addCustomListValue, updateCustomListValue, updateCustomList, setCustomListActive, deleteCustomList, deleteCustomListValue,
  type CustomList, type CustomListValue,
} from '../../api/client'
import { Modal, Spinner } from '../ui'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import type { FieldOption } from '../../ui/fieldSpec'
import { TextField } from '../../ui/TextField'
import { TextAreaField } from '../../ui/TextAreaField'
import { CodeField } from '../../ui/CodeField'
import { SelectField } from '../../ui/SelectField'
import { NumberField } from '../../ui/NumberField'
import { CheckboxField } from '../../ui/CheckboxField'
import { Button } from '../../ui/Button'
import { StatusBadge } from '../../ui/badges'

/**
 * Custom Lists admin (NetSuite-style, VENDOR-ONBOARDING / DATA-MODEL §4). A list is a reusable coded
 * value set (COUNTRY, BANK, PAYMENT_TERMS…) that fields are tagged to; values store a CODE (source of
 * truth) and a LABEL (shown). Dependent lists (STATE→COUNTRY, CITY→STATE) scope each value to a parent
 * value. Editable here without a code change — the forms read the same lists live.
 * D2: the rail + detail shell is the Setup archetype; editor fields are ui/ primitives.
 */
export function AdminCustomLists() {
  const qc = useQueryClient()
  const { data: lists = [], isPending } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })
  const [selCode, setSelCode] = useState<string | null>(null)
  const [newList, setNewList] = useState(false)
  const [editList, setEditList] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  const refresh = () => { void qc.invalidateQueries({ queryKey: ['custom-lists'] }) }
  const onErr = (e: Error) => setErr(e.message)

  const setListActive = useMutation({
    mutationFn: (v: { code: string; active: boolean }) => setCustomListActive(v.code, v.active),
    onSuccess: refresh, onError: onErr,
  })
  const removeList = useMutation({
    mutationFn: (code: string) => deleteCustomList(code),
    onSuccess: () => { setSelCode(null); refresh() }, onError: onErr,
  })

  if (isPending) return <Spinner label="Loading custom lists…" />

  const selected = lists.find((l) => l.code === selCode) ?? lists[0] ?? null
  const parentList = selected?.parentListCode ? lists.find((l) => l.code === selected.parentListCode) : undefined

  return (
    <SetupPage
      title="Custom Lists"
      subtitle="Reusable value sets tagged to fields — like NetSuite Custom Lists."
      primaryAction={<Button variant="primary" icon="plus" onClick={() => setNewList(true)}>New list</Button>}
      error={err}
      railItems={lists.map((l) => ({ key: l.code, label: l.name, hint: `${l.code}${l.parentListCode ? ` · ↳ ${l.parentListCode}` : ''}` }))}
      selectedKey={selected?.code ?? null}
      onSelect={setSelCode}
      railEmpty="No lists yet."
      detail={selected && (
        <div>
          {/* CF1-T2: the list-SELF verbs the operator's complaint named. */}
          <div className="chead" style={{ paddingLeft: 0 }}>
            <h3>{selected.name} <span className="mono hint" style={{ fontWeight: 400 }}>{selected.code}</span>
              {selected.active === false && <span className="badge b-grey" style={{ marginLeft: 8 }}>Inactive</span>}
              {selected.orderMode === 'Alphabetical' && <span className="badge b-blue" style={{ marginLeft: 8 }}>A→Z</span>}
            </h3>
            <div className="spacer" />
            <Button variant="ghost" size="sm" icon="edit" onClick={() => setEditList(true)} ariaLabel="Edit list">Edit list</Button>
            {!selected.isSystem && (
              <>
                <Button variant="ghost" size="sm"
                  onClick={() => setListActive.mutate({ code: selected.code, active: !(selected.active ?? true) })}
                  ariaLabel={selected.active === false ? 'Reactivate list' : 'Deactivate list'}>
                  {selected.active === false ? 'Reactivate' : 'Deactivate'}
                </Button>
                <Button variant="ghost" size="sm" red onClick={() => removeList.mutate(selected.code)} ariaLabel="Delete list">Delete</Button>
              </>
            )}
          </div>
          <ListValues list={selected} parentList={parentList} onRefresh={refresh} onErr={onErr} clearErr={() => setErr(null)} />
        </div>
      )}
    >
      {newList && <NewListModal lists={lists} onClose={() => setNewList(false)}
        onCreated={(code) => { setNewList(false); setSelCode(code); void refresh() }} onErr={onErr} />}
      {editList && selected && <EditListModal list={selected} onClose={() => setEditList(false)}
        onSaved={() => { setEditList(false); void refresh() }} onErr={onErr} />}
    </SetupPage>
  )
}

const parentOptions = (parentList: CustomList): FieldOption[] =>
  parentList.values.map((p) => ({ code: p.code, label: p.label }))

function ListValues({ list, parentList, onRefresh, onErr, clearErr }: {
  list: CustomList; parentList?: CustomList; onRefresh: () => void; onErr: (e: Error) => void; clearErr: () => void
}) {
  const [label, setLabel] = useState('')
  const [parentValue, setParentValue] = useState('')
  const [editing, setEditing] = useState<CustomListValue | null>(null)

  const add = useMutation({
    // CF-FIX1-T6: the id is system-assigned (1,2,3… entry order) — the user types only the label.
    mutationFn: () => addCustomListValue(list.code, { code: null, label: label.trim(), parentValueCode: parentList ? (parentValue || null) : null }),
    onSuccess: () => { setLabel(''); setParentValue(''); onRefresh() }, onError: onErr,
  })
  const update = useMutation({
    mutationFn: (v: CustomListValue) => updateCustomListValue(v.id, { label: v.label, parentValueCode: v.parentValueCode, sort: v.sort, active: v.active }),
    onSuccess: () => { setEditing(null); onRefresh() }, onError: onErr,
  })
  const remove = useMutation({ mutationFn: (id: string) => deleteCustomListValue(id), onSuccess: onRefresh, onError: onErr })

  const parentLabel = (pc: string | null) => pc ? (parentList?.values.find((x) => x.code === pc)?.label ?? pc) : '—'
  const submitAdd = () => { clearErr(); if (!label.trim()) { onErr(new Error('Enter a label.')); return } add.mutate() }

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
          <tr><th>ID</th><th>Label</th>{parentList && <th>{parentList.name}</th>}<th>Active</th><th /></tr>
        </thead>
        <tbody>
          {[...list.values].sort((a, b) => a.sort - b.sort || a.label.localeCompare(b.label)).map((v) => (
            <tr key={v.id}>
              <td className="mono">{v.code}</td>
              <td style={{ fontWeight: 600 }}>{v.label}</td>
              {parentList && <td className="hint">{parentLabel(v.parentValueCode)}</td>}
              <td><StatusBadge tone={v.active ? 'green' : 'grey'}>{v.active ? 'Active' : 'Hidden'}</StatusBadge></td>
              <td className="amt">
                <div className="rowactions">
                  <Button variant="outline" size="sm" icon="edit" onClick={() => setEditing(v)}>Edit</Button>
                  <Button variant="ghost" size="sm" icon="x" busy={remove.isPending} onClick={() => remove.mutate(v.id)} ariaLabel={`Delete ${v.code}`} />
                </div>
              </td>
            </tr>
          ))}
          {list.values.length === 0 && <tr><td colSpan={parentList ? 5 : 4}><span className="hint">No values yet — add the first below.</span></td></tr>}
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
        </div>
        <div style={{ marginTop: 12 }}>
          <Button variant="primary" size="sm" icon="plus" busy={add.isPending} onClick={submitAdd}>Add value</Button>
        </div>
      </div>

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

  const create = useMutation({
    mutationFn: () => createCustomList({ code: code.trim().toUpperCase(), name: name.trim(), description: description.trim() || null, parentListCode: parentListCode || null, orderMode }),
    onSuccess: (l) => onCreated(l.code), onError: onErr,
  })
  const submit = () => { if (!code.trim() || !name.trim()) { onErr(new Error('Enter both an Internal ID and a name.')); return } create.mutate() }

  return (
    <Modal title="New custom list" icon="plus"
      footer={<>
        <Button variant="outline" onClick={onClose}>Cancel</Button>
        <Button variant="primary" icon="check" busy={create.isPending} onClick={submit}>Create list</Button>
      </>}>
      <div className="grid g2">
        <CodeField spec={{ key: 'lcode', label: 'Internal ID', dataType: 'code', placeholder: 'e.g. INCOTERM' }} value={code} onChange={setCode} />
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
        spec={{ key: 'lorder', label: 'Show options in', dataType: 'select',
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
  const save = useMutation({
    mutationFn: () => updateCustomList(list.code, { name, description: desc || null, orderMode }),
    onSuccess: onSaved, onError: onErr,
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
        spec={{ key: 'el-order', label: 'Show options in', dataType: 'select', options: { kind: 'static', options: [
          { code: 'Entered', label: 'The order entered' }, { code: 'Alphabetical', label: 'Alphabetical order' },
        ] } }}
        value={orderMode} onChange={(v) => setOrderMode(String(v ?? 'Entered'))} />
      <p className="hint">The Internal ID (<span className="mono">{list.code}</span>) is immutable — fields are tagged to it.</p>
    </Modal>
  )
}

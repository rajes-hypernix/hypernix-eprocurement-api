import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getCustomLists, createCustomList, addCustomListValue, updateCustomListValue, deleteCustomListValue,
  type CustomList, type CustomListValue,
} from '../../api/client'
import { Icon } from '../Icon'
import { Modal, Notice, Spinner } from '../ui'

/**
 * Custom Lists admin (NetSuite-style, VENDOR-ONBOARDING / DATA-MODEL §4). A list is a reusable coded
 * value set (COUNTRY, BANK, PAYMENT_TERMS…) that fields are tagged to; values store a CODE (source of
 * truth) and a LABEL (shown). Dependent lists (STATE→COUNTRY, CITY→STATE) scope each value to a parent
 * value. Editable here without a code change — the forms read the same lists live.
 */
export function AdminCustomLists() {
  const qc = useQueryClient()
  const { data: lists = [], isPending } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })
  const [selCode, setSelCode] = useState<string | null>(null)
  const [newList, setNewList] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  const refresh = () => { void qc.invalidateQueries({ queryKey: ['custom-lists'] }) }
  const onErr = (e: Error) => setErr(e.message)

  if (isPending) return <Spinner label="Loading custom lists…" />

  const selected = lists.find((l) => l.code === selCode) ?? lists[0] ?? null
  const parentList = selected?.parentListCode ? lists.find((l) => l.code === selected.parentListCode) : undefined

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Custom Lists</h1>
          <p>Reusable coded value sets tagged to fields — like NetSuite Custom Lists. Values store a code (kept) and a label (shown). Maintained here, no code change needed.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" onClick={() => setNewList(true)}><Icon name="plus" size={15} /> New list</button>
      </div>

      {err && <Notice tone="error" icon="x">{err}</Notice>}

      <div className="obwrap">
        <div className="obside" style={{ position: 'static' }}>
          {lists.map((l) => (
            <button type="button" key={l.code} className={`obstep ${selected?.code === l.code ? 'on' : ''}`} onClick={() => setSelCode(l.code)}>
              <span style={{ display: 'flex', flexDirection: 'column', gap: 2, textAlign: 'left' }}>
                <span>{l.name}</span>
                <span className="hint" style={{ fontWeight: 400 }}>{l.code}{l.parentListCode ? ` · ↳ ${l.parentListCode}` : ''}</span>
              </span>
            </button>
          ))}
          {lists.length === 0 && <p className="hint">No lists yet.</p>}
        </div>
        <div className="obmain">
          {selected && <ListValues list={selected} parentList={parentList} onRefresh={refresh} onErr={onErr} clearErr={() => setErr(null)} />}
        </div>
      </div>

      {newList && <NewListModal lists={lists} onClose={() => setNewList(false)}
        onCreated={(code) => { setNewList(false); setSelCode(code); void refresh() }} onErr={onErr} />}
    </>
  )
}

function ListValues({ list, parentList, onRefresh, onErr, clearErr }: {
  list: CustomList; parentList?: CustomList; onRefresh: () => void; onErr: (e: Error) => void; clearErr: () => void
}) {
  const [code, setCode] = useState('')
  const [label, setLabel] = useState('')
  const [parentValue, setParentValue] = useState('')
  const [editing, setEditing] = useState<CustomListValue | null>(null)

  const add = useMutation({
    mutationFn: () => addCustomListValue(list.code, { code: code.trim(), label: label.trim(), parentValueCode: parentList ? (parentValue || null) : null }),
    onSuccess: () => { setCode(''); setLabel(''); setParentValue(''); onRefresh() }, onError: onErr,
  })
  const update = useMutation({
    mutationFn: (v: CustomListValue) => updateCustomListValue(v.id, { label: v.label, parentValueCode: v.parentValueCode, sort: v.sort, active: v.active }),
    onSuccess: () => { setEditing(null); onRefresh() }, onError: onErr,
  })
  const remove = useMutation({ mutationFn: (id: string) => deleteCustomListValue(id), onSuccess: onRefresh, onError: onErr })

  const parentLabel = (pc: string | null) => pc ? (parentList?.values.find((x) => x.code === pc)?.label ?? pc) : '—'
  const submitAdd = () => { clearErr(); if (!code.trim() || !label.trim()) { onErr(new Error('Enter both a code and a label.')); return } add.mutate() }

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
          <tr><th>Code</th><th>Label</th>{parentList && <th>{parentList.name}</th>}<th>Active</th><th /></tr>
        </thead>
        <tbody>
          {[...list.values].sort((a, b) => a.sort - b.sort || a.label.localeCompare(b.label)).map((v) => (
            <tr key={v.id}>
              <td className="mono">{v.code}</td>
              <td style={{ fontWeight: 600 }}>{v.label}</td>
              {parentList && <td className="hint">{parentLabel(v.parentValueCode)}</td>}
              <td><span className={`badge ${v.active ? 'b-green' : 'b-grey'}`}>{v.active ? 'Active' : 'Hidden'}</span></td>
              <td className="amt">
                <div className="rowactions">
                  <button type="button" className="btn btn-out btn-sm" onClick={() => setEditing(v)}><Icon name="edit" size={13} /> Edit</button>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={remove.isPending} onClick={() => remove.mutate(v.id)}><Icon name="x" size={13} /></button>
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
          <div className="field" style={{ margin: 0 }}><label>Code (stored)</label>
            <input value={code} aria-label="Value code" placeholder="e.g. NET30" onChange={(e) => setCode(e.target.value)} /></div>
          <div className="field" style={{ margin: 0 }}><label>Label (shown)</label>
            <input value={label} aria-label="Value label" placeholder="e.g. 30 days" onChange={(e) => setLabel(e.target.value)} /></div>
          {parentList && (
            <div className="field" style={{ margin: 0 }}><label>{parentList.name}</label>
              <select value={parentValue} aria-label="Parent value" onChange={(e) => setParentValue(e.target.value)}>
                <option value="">Select {parentList.name.toLowerCase()}</option>
                {parentList.values.map((p) => <option key={p.code} value={p.code}>{p.label}</option>)}
              </select></div>
          )}
        </div>
        <button type="button" className="btn btn-pri btn-sm" style={{ marginTop: 12 }} disabled={add.isPending} onClick={submitAdd}>
          <Icon name="plus" size={14} /> Add value
        </button>
      </div>

      {editing && (
        <Modal title={`Edit — ${editing.code}`} icon="edit"
          footer={<><button type="button" className="btn btn-out" onClick={() => setEditing(null)}>Cancel</button>
            <button type="button" className="btn btn-pri" disabled={update.isPending} onClick={() => update.mutate(editing)}><Icon name="check" size={15} /> Save</button></>}>
          <div className="field"><label>Code</label><input readOnly value={editing.code} aria-label="Code (read only)" /></div>
          <div className="field"><label>Label</label><input value={editing.label} aria-label="Edit label" onChange={(e) => setEditing({ ...editing, label: e.target.value })} /></div>
          {parentList && (
            <div className="field"><label>{parentList.name}</label>
              <select value={editing.parentValueCode ?? ''} aria-label="Edit parent value" onChange={(e) => setEditing({ ...editing, parentValueCode: e.target.value || null })}>
                <option value="">Select {parentList.name.toLowerCase()}</option>
                {parentList.values.map((p) => <option key={p.code} value={p.code}>{p.label}</option>)}
              </select></div>
          )}
          <div className="grid g2">
            <div className="field" style={{ marginBottom: 0 }}><label>Sort order</label>
              <input type="number" value={editing.sort} aria-label="Sort order" onChange={(e) => setEditing({ ...editing, sort: Number(e.target.value) || 0 })} /></div>
            <div className="field" style={{ marginBottom: 0 }}><label>Status</label>
              <label className="ck" style={{ marginTop: 8 }}><input type="checkbox" checked={editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} /> Active (offered in dropdowns)</label></div>
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

  const create = useMutation({
    mutationFn: () => createCustomList({ code: code.trim().toUpperCase(), name: name.trim(), description: description.trim() || null, parentListCode: parentListCode || null }),
    onSuccess: (l) => onCreated(l.code), onError: onErr,
  })
  const submit = () => { if (!code.trim() || !name.trim()) { onErr(new Error('Enter both a code and a name.')); return } create.mutate() }

  return (
    <Modal title="New custom list" icon="plus"
      footer={<><button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
        <button type="button" className="btn btn-pri" disabled={create.isPending} onClick={submit}><Icon name="check" size={15} /> Create list</button></>}>
      <div className="grid g2">
        <div className="field"><label>Code</label><input value={code} aria-label="List code" placeholder="e.g. INCOTERM" onChange={(e) => setCode(e.target.value)} /></div>
        <div className="field"><label>Name</label><input value={name} aria-label="List name" placeholder="e.g. Incoterms" onChange={(e) => setName(e.target.value)} /></div>
      </div>
      <div className="field"><label>Description</label><input value={description} aria-label="List description" placeholder="Optional" onChange={(e) => setDescription(e.target.value)} /></div>
      <div className="field" style={{ marginBottom: 0 }}><label>Depends on (parent list)</label>
        <select value={parentListCode} aria-label="Parent list" onChange={(e) => setParentListCode(e.target.value)}>
          <option value="">None — a flat list</option>
          {lists.map((l) => <option key={l.code} value={l.code}>{l.name}</option>)}
        </select>
        <p className="hint" style={{ marginTop: 6 }}>A dependent list scopes each value to a parent value (e.g. City depends on State).</p>
      </div>
    </Modal>
  )
}

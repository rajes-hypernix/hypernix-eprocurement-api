import { useState } from 'react'
import { Icon } from '../Icon'
import {
  QTYPES, typeLabel, defaultConfig, newItemId, sectionsOf,
  type EditItem, type FormItemConfig, type FormSections, type GroupKey,
} from '../../lib/formTypes'

const GROUPS: [GroupKey, string][] = [['commercial', 'Commercial'], ['technical', 'Technical']]
const ADD_OPTIONS: { kind: string; type: string; label: string }[] = [
  ...QTYPES.map(([t, l]) => ({ kind: 'question', type: t, label: l })),
  { kind: 'instruction', type: '', label: 'Instructions' },
  { kind: 'terms', type: '', label: 'Terms & conditions' },
]

export function QuestionEditor({ items, sections, onChange }: {
  items: EditItem[]
  sections: FormSections
  onChange: (items: EditItem[], sections: FormSections) => void
}) {
  const [openId, setOpenId] = useState<string | null>(null)
  const [menu, setMenu] = useState<string | null>(null)
  const [dragId, setDragId] = useState<string | null>(null)

  const setItems = (next: EditItem[]) => onChange(next, sections)
  const update = (id: string, patch: Partial<EditItem>) => setItems(items.map((it) => (it.id === id ? { ...it, ...patch } : it)))
  const updateCfg = (id: string, patch: Partial<FormItemConfig>) => {
    const it = items.find((x) => x.id === id)
    if (it) update(id, { config: { ...it.config, ...patch } })
  }
  const remove = (id: string) => { setItems(items.filter((it) => it.id !== id)); if (openId === id) setOpenId(null) }

  const addItem = (group: GroupKey, section: string, kind: string, type: string) => {
    const it: EditItem = {
      id: newItemId(), kind, group, section,
      label: kind === 'terms' ? 'Terms & conditions' : kind === 'instruction' ? 'Instruction to bidders' : '',
      type: kind === 'question' ? (type || 'short_text') : '',
      required: kind !== 'instruction',
      config: kind === 'question' ? defaultConfig(type) : {}, help: '',
    }
    const next = [...items]
    let pos = -1
    next.forEach((x, i) => { if (x.group === group && (x.section || '') === (section || '')) pos = i })
    if (pos >= 0) next.splice(pos + 1, 0, it); else next.push(it)
    setItems(next); setMenu(null); setOpenId(it.id)
  }

  const move = (id: string, dir: number) => {
    const it = items.find((x) => x.id === id); if (!it) return
    const sibs = items.filter((x) => x.group === it.group && (x.section || '') === (it.section || ''))
    const p = sibs.findIndex((x) => x.id === id); const t = p + dir
    if (t < 0 || t >= sibs.length) return
    const next = [...items]; const a = next.indexOf(sibs[p]); const b = next.indexOf(sibs[t])
    ;[next[a], next[b]] = [next[b], next[a]]; setItems(next)
  }

  // Drag-and-drop reorder / move across sub-groups + groups.
  const moveTo = (dragged: string, g: GroupKey, sec: string, beforeId: string | null) => {
    const idx = items.findIndex((x) => x.id === dragged)
    if (idx < 0) return
    const next = [...items]
    const [it] = next.splice(idx, 1)
    const moved = { ...it, group: g, section: sec }
    if (beforeId && beforeId !== dragged) {
      const bi = next.findIndex((x) => x.id === beforeId)
      if (bi >= 0) { next.splice(bi, 0, moved); setItems(next); return }
    }
    let pos = -1
    next.forEach((x, i) => { if (x.group === g && (x.section || '') === (sec || '')) pos = i })
    if (pos >= 0) next.splice(pos + 1, 0, moved); else next.push(moved)
    setItems(next)
  }
  const onItemDragStart = (e: React.DragEvent, id: string) => {
    const tag = (e.target as HTMLElement).tagName
    if (['INPUT', 'TEXTAREA', 'SELECT', 'BUTTON'].includes(tag)) { e.preventDefault(); return }
    setDragId(id); e.dataTransfer.effectAllowed = 'move'
  }

  const addSection = (g: GroupKey) => onChange(items, { ...sections, [g]: [...(sections[g] ?? []), 'New sub-group'] })
  const renameSection = (g: GroupKey, idx: number, name: string) => {
    const old = sectionsOf(g, sections, items)[idx]
    const declared = [...(sections[g] ?? [])]; const di = declared.indexOf(old); if (di >= 0) declared[di] = name
    const nextItems = items.map((it) => (it.group === g && it.section === old ? { ...it, section: name } : it))
    onChange(nextItems, { ...sections, [g]: declared })
  }
  const delSection = (g: GroupKey, idx: number) => {
    const sec = sectionsOf(g, sections, items)[idx]
    const declared = (sections[g] ?? []).filter((s) => s !== sec)
    const nextItems = items.map((it) => (it.group === g && it.section === sec ? { ...it, section: '' } : it))
    onChange(nextItems, { ...sections, [g]: declared })
  }

  // Rendered as inline functions (not nested components) so their inputs keep focus
  // across re-renders — a nested component remounts on every keystroke.
  const addBtn = (token: string, label: string) => (
    <div className="qaddwrap">
      <button type="button" className={`qaddbtn${menu === token ? ' on' : ''}`} onClick={(e) => { e.stopPropagation(); setMenu(menu === token ? null : token) }}>
        <Icon name="plus" size={13} /> {label}
      </button>
      {menu === token && (
        <div className="qmenu" onClick={(e) => e.stopPropagation()}>
          {ADD_OPTIONS.map((o, i) => (
            <button key={i} type="button" onClick={() => { const [g, s] = token.split('::'); addItem(g as GroupKey, s, o.kind, o.type) }}>{o.label}</button>
          ))}
        </div>
      )}
    </div>
  )

  const renderTypeConfig = (it: EditItem) => {
    const c = it.config
    const listEdit = (key: 'options' | 'columns' | 'rows', label: string) => {
      const arr = c[key] ?? []
      return (
        <div className="field"><label>{label}</label>
          {arr.map((v, vi) => (
            <div className="optrow" key={vi}>
              <input type="text" value={v} onChange={(e) => updateCfg(it.id, { [key]: arr.map((x, i) => (i === vi ? e.target.value : x)) } as Partial<FormItemConfig>)} />
              <button type="button" className="lnk" onClick={() => updateCfg(it.id, { [key]: arr.filter((_, i) => i !== vi) } as Partial<FormItemConfig>)}><Icon name="x" size={13} /></button>
            </div>
          ))}
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => updateCfg(it.id, { [key]: [...arr, `${label.replace(/s$/, '')} ${arr.length + 1}`] } as Partial<FormItemConfig>)}><Icon name="plus" size={13} /> Add {label.toLowerCase().replace(/s$/, '')}</button>
        </div>
      )
    }
    if (it.type === 'list' || it.type === 'multi') return listEdit('options', 'Options')
    if (it.type === 'number' || it.type === 'money' || it.type === 'percent')
      return <div className="field"><label>Unit / note</label><input type="text" value={c.unit ?? ''} placeholder={it.type === 'money' ? 'currency, e.g. MYR' : it.type === 'percent' ? '%' : 'e.g. weeks'} onChange={(e) => updateCfg(it.id, { unit: e.target.value })} /></div>
    if (it.type === 'attachment')
      return (
        <div className="grid g2">
          <div className="field"><label>Allowed file types</label><input type="text" value={c.filetypes ?? 'PDF'} placeholder="PDF, XLSX" onChange={(e) => updateCfg(it.id, { filetypes: e.target.value })} /></div>
          <label className="ck" style={{ alignSelf: 'end', marginBottom: 14 }}><input type="checkbox" checked={c.multiple ?? false} onChange={(e) => updateCfg(it.id, { multiple: e.target.checked })} /> Allow multiple files</label>
        </div>
      )
    if (it.type === 'table')
      return (
        <>
          <div className="grid g2">{listEdit('columns', 'Columns')}{listEdit('rows', 'Rows')}</div>
          <div className="field"><label>Cell type</label>
            <select value={c.cellType ?? 'money'} onChange={(e) => updateCfg(it.id, { cellType: e.target.value })}>
              <option value="money">Money</option><option value="number">Number</option><option value="text">Text</option>
            </select>
          </div>
        </>
      )
    if (it.type === 'group') {
      const fields = c.fields ?? []
      return (
        <>
          <div className="field"><label>Fields per entry</label>
            {fields.map((f, fi) => (
              <div className="optrow" key={fi}>
                <input type="text" value={f.label} placeholder="Field label" onChange={(e) => updateCfg(it.id, { fields: fields.map((x, i) => (i === fi ? { ...x, label: e.target.value } : x)) })} />
                <select value={f.type} style={{ maxWidth: 130 }} onChange={(e) => updateCfg(it.id, { fields: fields.map((x, i) => (i === fi ? { ...x, type: e.target.value } : x)) })}>
                  {[['short_text', 'Text'], ['number', 'Number'], ['money', 'Money'], ['date', 'Date']].map(([t, l]) => <option key={t} value={t}>{l}</option>)}
                </select>
                <button type="button" className="lnk" onClick={() => updateCfg(it.id, { fields: fields.filter((_, i) => i !== fi) })}><Icon name="x" size={13} /></button>
              </div>
            ))}
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => updateCfg(it.id, { fields: [...fields, { label: `Field ${fields.length + 1}`, type: 'short_text' }] })}><Icon name="plus" size={13} /> Add field</button>
          </div>
          <div className="field"><label>Maximum entries</label><input type="number" style={{ maxWidth: 120 }} value={c.max ?? 3} onChange={(e) => updateCfg(it.id, { max: Number(e.target.value) || 1 })} /></div>
        </>
      )
    }
    return null
  }

  const renderConfig = (it: EditItem) => {
    const c = it.config
    const lbl = it.kind === 'question'
      ? <div className="field"><label>Question</label><input type="text" value={it.label} placeholder="Enter the question" onChange={(e) => update(it.id, { label: e.target.value })} /></div>
      : <div className="field"><label>{it.kind === 'terms' ? 'Terms & conditions text' : 'Instruction text'}</label><textarea rows={3} value={it.label} onChange={(e) => update(it.id, { label: e.target.value })} /></div>
    const meta = (
      <div className="grid g2">
        <div className="field"><label>Group</label>
          <select value={it.group} onChange={(e) => update(it.id, { group: e.target.value, section: '' })}>
            <option value="commercial">Commercial</option><option value="technical">Technical</option>
          </select>
        </div>
        <div className="field"><label>Sub-group</label>
          <select value={it.section} onChange={(e) => update(it.id, { section: e.target.value })}>
            <option value="">Ungrouped</option>
            {sectionsOf(it.group as GroupKey, sections, items).map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
      </div>
    )
    let cfg: React.ReactNode = null
    if (it.kind === 'question') {
      cfg = (
        <>
          <div className="field"><label>Answer type</label>
            <select value={it.type} onChange={(e) => { const v = e.target.value; const patch: Partial<EditItem> = { type: v }; if ((v === 'list' || v === 'multi') && !(c.options && c.options.length)) patch.config = { ...c, options: ['Option 1'] }; update(it.id, patch) }}>
              {QTYPES.map(([t, l]) => <option key={t} value={t}>{l}</option>)}
            </select>
          </div>
          {renderTypeConfig(it)}
        </>
      )
    } else if (it.kind === 'terms') {
      cfg = <p className="hint"><Icon name="check" size={12} /> Vendors must tick “I accept” to submit.</p>
    }
    return <>{lbl}{meta}{cfg}</>
  }

  const itemRow = (it: EditItem) => {
    const open = openId === it.id
    const chip = it.kind === 'instruction' ? <span className="chip ck-i">Instructions</span>
      : it.kind === 'terms' ? <span className="chip ck-t">Terms</span>
      : <span className="chip">{typeLabel(it.type)}</span>
    return (
      <div
        className={`qitem${open ? ' open' : ''}${dragId === it.id ? ' dragging' : ''}`}
        key={it.id}
        draggable
        onDragStart={(e) => onItemDragStart(e, it.id)}
        onDragEnd={() => setDragId(null)}
        onDragOver={(e) => { if (dragId) e.preventDefault() }}
        onDrop={(e) => { if (dragId) { e.preventDefault(); e.stopPropagation(); moveTo(dragId, it.group as GroupKey, it.section, it.id); setDragId(null) } }}
      >
        <div className="qih">
          <span className="grip" title="Drag to reorder">⠿</span>
          <span className="qlabel" onClick={() => setOpenId(open ? null : it.id)}>{it.label || <span className="qmuted">{it.kind === 'question' ? 'Untitled question' : it.kind === 'terms' ? 'Terms text…' : 'Instruction text…'}</span>}</span>
          {chip}
          {it.kind !== 'instruction' && <button type="button" className={`qreq${it.required ? ' on' : ''}`} onClick={() => update(it.id, { required: !it.required })}>{it.required ? 'Required' : 'Optional'}</button>}
          <button type="button" className="ic mv" title="Move up" onClick={() => move(it.id, -1)}>▲</button>
          <button type="button" className="ic mv" title="Move down" onClick={() => move(it.id, 1)}>▼</button>
          <button type="button" className="ic exch" onClick={() => setOpenId(open ? null : it.id)}><Icon name="chev" size={14} /></button>
          <button type="button" className="ic del" onClick={() => remove(it.id)}><Icon name="x" size={15} /></button>
        </div>
        {open && <div className="qbody">{renderConfig(it)}</div>}
      </div>
    )
  }

  return (
    <>
      {menu && <div className="qmscrim" onClick={() => setMenu(null)} />}
      {GROUPS.map(([g, gl]) => {
        const secs = sectionsOf(g, sections, items)
        const ungrouped = items.filter((it) => it.group === g && !it.section)
        return (
          <div className="qgroup" key={g}>
            <div className="qghead">
              <span className="qgtitle"><span className={`envdot ${g}`} />{gl}</span>
              <span className="qgsub">sealed {gl.toLowerCase()} envelope</span>
              <div className="sp" />
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => addSection(g)}><Icon name="plus" size={13} /> Sub-group</button>
            </div>
            {secs.map((sec, si) => (
              <div className="qsec" key={si} onDragOver={(e) => { if (dragId) e.preventDefault() }} onDrop={(e) => { if (dragId) { e.preventDefault(); moveTo(dragId, g, sec, null); setDragId(null) } }}>
                <div className="qsechead">
                  <input className="secname" value={sec} placeholder="Sub-group name" onChange={(e) => renameSection(g, si, e.target.value)} />
                  <button type="button" className="lnk" title="Delete sub-group" onClick={() => delSection(g, si)}><Icon name="x" size={13} /></button>
                </div>
                {items.filter((it) => it.group === g && it.section === sec).map((it) => itemRow(it))}
                {addBtn(`${g}::${sec}`, `Add to “${sec || 'sub-group'}”`)}
              </div>
            ))}
            <div className="qsec" onDragOver={(e) => { if (dragId) e.preventDefault() }} onDrop={(e) => { if (dragId) { e.preventDefault(); moveTo(dragId, g, '', null); setDragId(null) } }}>
              {ungrouped.map((it) => itemRow(it))}
              {addBtn(`${g}::`, secs.length ? 'Add item (ungrouped)' : 'Add item')}
            </div>
          </div>
        )
      })}
    </>
  )
}

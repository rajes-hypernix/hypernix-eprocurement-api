import { useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getItems, createItem, updateItem, setItemActive, deleteItem, type ItemDto } from '../../api/client'
import { Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { useNotify } from '../../ui/Notify'

/**
 * CFH-T4/T6: the Item Master — a LOOKUP SOURCE for line Item Codes (lines keep storing ItemCode
 * as a plain string; the PR entry form picks from here and auto-fills Description + UoM). Follows
 * the Entry Forms pattern: a LIST → Open → dedicated FULL PAGE → Back (with a real dirty guard).
 */
export function AdminItems() {
  const qc = useQueryClient()
  const { data: items = [] } = useQuery({ queryKey: ['items'], queryFn: () => getItems() })
  const [editingId, setEditingId] = useState<string | null>(null)   // 'new' | item id | null (list)
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['items'] }) }
  const editing = editingId === 'new' ? null : items.find((i) => i.id === editingId)

  if (editingId !== null && (editingId === 'new' || editing)) {
    return <ItemEditor item={editing ?? null} onBack={() => setEditingId(null)} onChanged={refresh} />
  }

  return (
    <div className="panel-fade">
      <div className="pagehead">
        <div><h1>Item Master</h1></div>
        <div className="spacer" />
        <Button variant="primary" size="sm" icon="plus" onClick={() => setEditingId('new')}>New item</Button>
      </div>
      <table>
        <thead><tr><th>Item code</th><th>Description</th><th>UoM</th><th>Status</th><th /></tr></thead>
        <tbody>
          {items.map((i) => (
            <tr key={i.id}>
              <td className="mono" style={{ fontWeight: 600 }}>{i.itemCode}</td>
              <td>{i.description}</td>
              <td>{i.uom}</td>
              <td><span className={`badge ${i.active ? 'b-green' : 'b-grey'}`}>{i.active ? 'Active' : 'Inactive'}</span></td>
              <td className="amt">
                <div className="rowactions">
                  <Button variant="ghost" size="sm" onClick={() => setEditingId(i.id)} ariaLabel={`Open ${i.itemCode}`}>Open</Button>
                </div>
              </td>
            </tr>
          ))}
          {items.length === 0 && <tr><td colSpan={5} className="hint">No items yet.</td></tr>}
        </tbody>
      </table>
    </div>
  )
}

function ItemEditor({ item, onBack, onChanged }: { item: ItemDto | null; onBack: () => void; onChanged: () => void }) {
  const notify = useNotify()
  const isNew = item === null
  const [itemCode, setItemCode] = useState(item?.itemCode ?? '')
  const [description, setDescription] = useState(item?.description ?? '')
  const [uom, setUom] = useState(item?.uom ?? 'Unit')
  const [error, setError] = useState<string | null>(null)
  const dirty = useRef(false)
  const touch = () => { dirty.current = true }

  const save = useMutation({
    mutationFn: () => {
      const req = { itemCode: itemCode.trim(), description: description.trim(), uom: uom.trim() || 'Unit' }
      return isNew ? createItem(req) : updateItem(item!.id, req)
    },
    onSuccess: () => { dirty.current = false; notify(isNew ? 'Item created' : 'Item saved', { kind: 'toast' }); onChanged(); onBack() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the item.'),
  })
  const toggle = useMutation({
    mutationFn: () => setItemActive(item!.id, !item!.active),
    onSuccess: () => { notify(item!.active ? 'Item deactivated' : 'Item reactivated', { kind: 'toast' }); onChanged(); onBack() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not update the item.'),
  })
  const remove = useMutation({
    mutationFn: () => deleteItem(item!.id),
    onSuccess: () => { dirty.current = false; notify('Item deleted', { kind: 'toast' }); onChanged(); onBack() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not delete the item.'),
  })

  const back = () => {
    if (!dirty.current || window.confirm('You have unsaved changes — all changes will be lost. Leave anyway?')) onBack()
  }

  return (
    <div className="panel-fade">
      <div className="chead" style={{ paddingLeft: 0 }}>
        <Button variant="ghost" size="sm" icon="back" onClick={back} ariaLabel="Back to items">Back</Button>
        <h3 style={{ marginLeft: 8 }}>{isNew ? 'New item' : item!.itemCode}</h3>
        <div className="spacer" />
        {!isNew && (
          <>
            <Button variant="ghost" size="sm" onClick={() => toggle.mutate()} ariaLabel={item!.active ? 'Deactivate item' : 'Reactivate item'}>
              {item!.active ? 'Deactivate' : 'Reactivate'}
            </Button>
            <Button variant="ghost" size="sm" red onClick={() => remove.mutate()} ariaLabel={`Delete ${item!.itemCode}`}>Delete</Button>
          </>
        )}
        <Button variant="primary" size="sm" busy={save.isPending} disabled={!itemCode.trim() || !description.trim()} onClick={() => save.mutate()} ariaLabel="Save item">Save item</Button>
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="card" style={{ padding: 18, marginTop: 12, maxWidth: 560 }}>
        <TextField spec={{ key: 'item-code', label: 'Item code', dataType: 'text', placeholder: 'e.g. VLV-GT-0150', readOnly: !isNew }}
          value={itemCode} onChange={(v) => { touch(); setItemCode(String(v ?? '')) }} />
        <TextField spec={{ key: 'item-desc', label: 'Description', dataType: 'text', placeholder: 'e.g. Gate Valve, DN150, PN16, CS' }}
          value={description} onChange={(v) => { touch(); setDescription(String(v ?? '')) }} />
        <TextField spec={{ key: 'item-uom', label: 'Unit of measure', dataType: 'text', placeholder: 'EA / M / Unit' }}
          value={uom} onChange={(v) => { touch(); setUom(String(v ?? '')) }} />
      </div>
    </div>
  )
}

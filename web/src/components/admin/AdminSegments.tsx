import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getSegmentDefs, createSegmentDef, updateSegmentDef, addSegmentValue, applySegment, unapplySegment,
  type SegmentDefDto,
} from '../../api/client'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Modal, Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'

/**
 * Segment definitions (D6) — the Admin Setup surface (A68). A segment is a reporting
 * dimension: values are dimension KEYS (codes derived like every sourcing code), applied
 * per record type, assigned per record, sliced in views and KPIs. The four system
 * segments mirror the PR's dimension columns one-way — they are read-only here (the
 * convergence backlog row owns changes to them). Dimension keys are never silently
 * dropped: un-applying a type with live assignments is refused by the server.
 */

const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[]): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

export function AdminSegments() {
  const qc = useQueryClient()
  const { data: defs = [] } = useQuery({ queryKey: ['segment-defs'], queryFn: getSegmentDefs })
  const [selected, setSelected] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const def = defs.find((d) => d.id === selected) ?? defs[0]
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['segment-defs'] }) }

  return (
    <SetupPage
      title="Segments"
      subtitle="Reporting dimensions — define once, apply to record types, slice any view or KPI by them."
      primaryAction={<Button variant="primary" size="sm" icon="plus" onClick={() => setCreating(true)}>New segment</Button>}
      railItems={defs.map((d) => ({
        key: d.id, label: d.name,
        hint: d.isSystem ? 'system' : `${d.values.length} value(s)`,
      }))}
      selectedKey={def?.id ?? ''}
      onSelect={setSelected}
      detail={def ? <SegmentDetail def={def} onChanged={refresh} /> : <p className="hint">No segments defined yet.</p>}
    >
      {creating && (
        <DefModal def={null}
          onClose={() => setCreating(false)}
          onSaved={(d) => { setCreating(false); setSelected(d.id); refresh() }} />
      )}
    </SetupPage>
  )
}

function SegmentDetail({ def, onChanged }: { def: SegmentDefDto; onChanged: () => void }) {
  const [editing, setEditing] = useState(false)
  const [addingValue, setAddingValue] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const byId = new Map(def.values.map((v) => [v.id, v]))

  const apply = useMutation({
    mutationFn: ({ recordType, lineLevel }: { recordType: string; lineLevel: boolean }) =>
      applySegment(def.id, { recordType, lineLevel }),
    onSuccess: () => { setError(null); onChanged() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not apply the segment.'),
  })
  const unapply = useMutation({
    mutationFn: (recordType: string) => unapplySegment(def.id, recordType),
    onSuccess: () => { setError(null); onChanged() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not remove the application.'),
  })

  return (
    <div>
      <div className="chead" style={{ paddingLeft: 0 }}>
        <h3>{def.name} <span className="mono hint" style={{ fontWeight: 400 }}>{def.code}</span></h3>
        <div className="spacer" />
        {def.isSystem
          ? <span className="badge b-blue">System — mirrors the PR's dimension columns</span>
          : <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>Edit</Button>}
      </div>
      {def.isSystem && (
        <p className="hint">
          Values and assignments are projected one-way from requisition entry — edit the PR,
          not the segment. Converging the PR onto user-defined segments is a recorded backlog decision.
        </p>
      )}
      {error && <Notice tone="error">{error}</Notice>}

      <h4>Values ({def.values.length})</h4>
      <table>
        <thead><tr><th>Code</th><th>Label</th>{def.hasHierarchy && <th>Parent</th>}<th>Status</th></tr></thead>
        <tbody>
          {def.values.map((v) => (
            <tr key={v.id}>
              <td className="mono">{v.code}</td>
              <td>{v.label}</td>
              {def.hasHierarchy && <td>{v.parentValueId ? byId.get(v.parentValueId)?.label ?? '—' : '—'}</td>}
              <td><span className={`badge ${v.active ? 'b-green' : 'b-grey'}`}>{v.active ? 'Active' : 'Inactive'}</span></td>
            </tr>
          ))}
          {def.values.length === 0 && <tr><td colSpan={def.hasHierarchy ? 4 : 3} className="hint">No values yet — add the first dimension key.</td></tr>}
        </tbody>
      </table>
      {!def.isSystem && (
        <Button variant="ghost" size="sm" icon="plus" onClick={() => setAddingValue(true)}>Add value</Button>
      )}

      <h4 style={{ marginTop: 18 }}>Applied to</h4>
      <table>
        <thead><tr><th>Record type</th><th>Applied</th><th>Line level</th></tr></thead>
        <tbody>
          {RECORD_TYPES.map((rt) => {
            const app = def.applications.find((a) => a.recordType === rt)
            return (
              <tr key={rt}>
                <td>{rt}</td>
                <td>
                  {def.isSystem
                    ? <span className={`badge ${app ? 'b-green' : 'b-grey'}`}>{app ? 'Applied' : '—'}</span>
                    : app
                      ? <Button variant="ghost" size="sm" onClick={() => unapply.mutate(rt)} ariaLabel={`Remove ${def.name} from ${rt}`}>Remove</Button>
                      : <Button variant="ghost" size="sm" onClick={() => apply.mutate({ recordType: rt, lineLevel: false })} ariaLabel={`Apply ${def.name} to ${rt}`}>Apply</Button>}
                </td>
                <td>
                  {/* Line-level is fixed at apply time (an application is immutable once live). */}
                  {!app && !def.isSystem && rt === 'PurchaseOrder' && (
                    <Button variant="ghost" size="sm" onClick={() => apply.mutate({ recordType: rt, lineLevel: true })} ariaLabel={`Apply ${def.name} to ${rt} per line`}>Apply per-line</Button>
                  )}
                  {app?.lineLevel && <span className="badge b-blue">per line</span>}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      <p className="hint" style={{ marginTop: 10 }}>
        Removing an application is refused while assignments exist — dimension keys are never
        silently dropped. Value codes derive from the label the same way sourcing codes do.
      </p>

      {editing && (
        <DefModal def={def} onClose={() => setEditing(false)} onSaved={() => { setEditing(false); onChanged() }} />
      )}
      {addingValue && (
        <ValueModal def={def} onClose={() => setAddingValue(false)} onSaved={() => { setAddingValue(false); onChanged() }} />
      )}
    </div>
  )
}

function DefModal({ def, onClose, onSaved }: {
  def: SegmentDefDto | null; onClose: () => void; onSaved: (d: SegmentDefDto) => void
}) {
  const [name, setName] = useState(def?.name ?? '')
  const [hasHierarchy, setHasHierarchy] = useState(def?.hasHierarchy ?? false)
  const [required, setRequired] = useState(def?.required ?? false)
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => {
      const req = { name, hasHierarchy, required }
      return def ? updateSegmentDef(def.id, req) : createSegmentDef(req)
    },
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the segment.'),
  })

  return (
    <Modal
      title={def ? `Edit segment — ${def.name}` : 'New segment'}
      icon="edit"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={!name.trim() || save.isPending} onClick={() => save.mutate()}>
            {def ? 'Save changes' : 'Create segment'}
          </button>
        </>
      }
    >
      <TextField spec={spec('seg-name', 'Name', 'text')} value={name} onChange={(v) => setName(String(v ?? ''))} />
      <CheckboxField spec={spec('seg-hier', 'Hierarchical values (parent stored; rollup reporting arrives gate-driven)', 'boolean')}
        value={hasHierarchy} onChange={(v) => setHasHierarchy(v === true)} />
      <CheckboxField spec={spec('seg-req', 'Required (enforced at assignment-save)', 'boolean')}
        value={required} onChange={(v) => setRequired(v === true)} />
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

function ValueModal({ def, onClose, onSaved }: { def: SegmentDefDto; onClose: () => void; onSaved: () => void }) {
  const [label, setLabel] = useState('')
  const [parentId, setParentId] = useState('')
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () => addSegmentValue(def.id, { label, parentValueId: parentId || null, sort: def.values.length }),
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not add the value.'),
  })

  return (
    <Modal
      title={`New value on ${def.name}`}
      icon="plus"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={!label.trim() || save.isPending} onClick={() => save.mutate()}>Add value</button>
        </>
      }
    >
      <TextField spec={spec('segv-label', 'Label (the code derives from it)', 'text')} value={label} onChange={(v) => setLabel(String(v ?? ''))} />
      {def.hasHierarchy && (
        <SelectField
          spec={{ key: 'segv-parent', label: 'Parent value (optional)', dataType: 'select', options: { kind: 'static', options: def.values.map((v) => ({ code: v.id, label: v.label })) } }}
          value={parentId} onChange={(v) => setParentId(String(v ?? ''))} />
      )}
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

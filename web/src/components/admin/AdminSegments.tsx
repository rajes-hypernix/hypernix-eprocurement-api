import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getSegmentDefs, createSegmentDef, updateSegmentDef, addSegmentValue, applySegment, unapplySegment,
  updateSegmentValue, deleteSegmentValue, setSegmentDefActive, deleteSegmentDef,
  getSegmentReferences, purgeSegment, getSegmentValueReferences, purgeSegmentValue,
  type SegmentDefDto, type SegmentValueDto,
} from '../../api/client'
import { ImpactReportDialog } from './ImpactReportDialog'
import { getEntryForms } from '../../api/client'
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
  const [editingValue, setEditingValue] = useState<SegmentValueDto | null>(null)
  // CF-FIX4-T6: destructive verbs open the CF-FIX-3 impact dialog — the admin SEES what
  // blocks (forms, views, live/historical assignments) and only eligible tiers are offered.
  const [impactDef, setImpactDef] = useState(false)
  const [impactValue, setImpactValue] = useState<SegmentValueDto | null>(null)
  // CF-FIX4-T7: a HEADER apply runs the form+group cascade (the field-creation flow);
  // a LINE apply stays flat (L4 — no groups on lines).
  const [applying, setApplying] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const byId = new Map(def.values.map((v) => [v.id, v]))

  const apply = useMutation({
    mutationFn: (req: { recordType: string; lineLevel: boolean; placements?: { formId: string; groupId?: string | null }[] }) =>
      applySegment(def.id, req),
    onSuccess: () => { setError(null); onChanged() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not apply the segment.'),
  })
  const unapply = useMutation({
    mutationFn: (recordType: string) => unapplySegment(def.id, recordType),
    onSuccess: () => { setError(null); onChanged() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not remove the application.'),
  })
  // CF2-T6: the uniform lifecycle — value edit/delete, def deactivate/delete, all guarded server-side.
  const toggleDef = useMutation({
    mutationFn: () => setSegmentDefActive(def.id, !def.active),
    onSuccess: () => { setError(null); onChanged() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not update the segment.'),
  })

  return (
    <div>
      <div className="chead" style={{ paddingLeft: 0 }}>
        <h3>{def.name} <span className="mono hint" style={{ fontWeight: 400 }}>{def.code}</span></h3>
        <div className="spacer" />
        {def.isSystem
          ? <span className="badge b-blue">System — mirrors the PR's dimension columns</span>
          : (
            <>
              <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>Edit</Button>
              <Button variant="ghost" size="sm" onClick={() => toggleDef.mutate()} ariaLabel={def.active ? 'Deactivate segment' : 'Reactivate segment'}>
                {def.active ? 'Deactivate' : 'Reactivate'}
              </Button>
              <Button variant="ghost" size="sm" red onClick={() => setImpactDef(true)} ariaLabel="Delete segment">Delete</Button>
            </>
          )}
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
        <thead><tr><th>Code</th><th>Label</th>{def.hasHierarchy && <th>Parent</th>}<th>Status</th><th /></tr></thead>
        <tbody>
          {def.values.map((v) => (
            <tr key={v.id}>
              <td className="mono">{v.code}</td>
              <td>{v.label}</td>
              {def.hasHierarchy && <td>{v.parentValueId ? byId.get(v.parentValueId)?.label ?? '—' : '—'}</td>}
              <td><span className={`badge ${v.active ? 'b-green' : 'b-grey'}`}>{v.active ? 'Active' : 'Inactive'}</span></td>
              <td className="amt">
                {!def.isSystem && (
                  <>
                    <Button variant="ghost" size="sm" onClick={() => setEditingValue(v)} ariaLabel={`Edit value ${v.label}`}>Edit</Button>
                    <Button variant="ghost" size="sm" red onClick={() => setImpactValue(v)} ariaLabel={`Delete value ${v.label}`}>Delete</Button>
                  </>
                )}
              </td>
            </tr>
          ))}
          {def.values.length === 0 && <tr><td colSpan={def.hasHierarchy ? 5 : 4} className="hint">No values yet — add the first dimension key.</td></tr>}
        </tbody>
      </table>
      {!def.isSystem && <StagedValues def={def} onChanged={onChanged} onError={setError} />}

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
                      : <Button variant="ghost" size="sm" onClick={() => setApplying(rt)} ariaLabel={`Apply ${def.name} to ${rt}`}>Apply</Button>}
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
      {editingValue && (
        <EditValueModal def={def} value={editingValue} onClose={() => setEditingValue(null)} onSaved={() => { setEditingValue(null); onChanged() }} />
      )}
      {applying && (
        <ApplyCascadeModal def={def} recordType={applying}
          onClose={() => setApplying(null)}
          onApply={(placements) => { apply.mutate({ recordType: applying, lineLevel: false, ...(placements ? { placements } : {}) }); setApplying(null) }} />
      )}
      {impactDef && (
        <ImpactReportDialog
          title={`Delete segment — ${def.name}`}
          kind="segment"
          load={() => getSegmentReferences(def.id)}
          onDeactivate={def.active ? () => setSegmentDefActive(def.id, false) : undefined}
          onDelete={() => deleteSegmentDef(def.id)}
          onPurge={() => purgeSegment(def.id)}
          onClose={(changed) => { setImpactDef(false); if (changed) onChanged() }}
        />
      )}
      {impactValue && (
        <ImpactReportDialog
          title={`Delete value — ${impactValue.label} (${impactValue.code})`}
          kind="segment-value"
          load={() => getSegmentValueReferences(impactValue.id)}
          onDeactivate={impactValue.active
            ? () => updateSegmentValue(def.id, impactValue.id, { label: impactValue.label, parentValueId: impactValue.parentValueId ?? null, sort: impactValue.sort, active: false })
            : undefined}
          onDelete={() => deleteSegmentValue(def.id, impactValue.id)}
          onPurge={() => purgeSegmentValue(impactValue.id)}
          onClose={(changed) => { setImpactValue(null); if (changed) onChanged() }}
        />
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

/**
 * CF-FIX4-T7: the header-apply cascade — form(s) with the STANDARD form pre-selected
 * (option c, the T4 convention) and a group per chosen form (Header default). Types
 * without forms apply plainly (nothing to place).
 */
function ApplyCascadeModal({ def, recordType, onClose, onApply }: {
  def: SegmentDefDto; recordType: string
  onClose: () => void
  onApply: (placements: { formId: string; groupId?: string | null }[] | null) => void
}) {
  const { data: allForms = [] } = useQuery({ queryKey: ['entry-form-defs'], queryFn: () => getEntryForms() })
  const forms = allForms.filter((f) => f.recordType === recordType && f.active)
  const [chosen, setChosen] = useState<string | undefined>(undefined)
  const [groups, setGroups] = useState<Record<string, string>>({})
  const selected = chosen !== undefined
    ? (chosen ? chosen.split('|') : [])
    : (forms.find((f) => f.isSystem) ? [forms.find((f) => f.isSystem)!.id] : [])

  return (
    <Modal title={`Apply ${def.name} to ${recordType}`} icon="edit"
      footer={<>
        <Button variant="outline" onClick={onClose}>Cancel</Button>
        <Button variant="primary" icon="check" disabled={forms.length > 0 && selected.length === 0}
          onClick={() => onApply(forms.length === 0 ? null : selected.map((fid) => ({ formId: fid, groupId: groups[fid] || null })))}
          ariaLabel="Apply segment">Apply</Button>
      </>}>
      {forms.length === 0
        ? <p className="hint">{recordType} has no entry forms yet — the segment applies without a form placement.</p>
        : (
          <>
            <SelectField
              spec={{ key: 'seg-apply-forms', label: `${recordType} — form(s)`, dataType: 'multiSelect', searchable: true,
                help: 'The segment appears on these forms — the standard form is pre-selected; the group defaults to Header.',
                options: { kind: 'static', options: forms.map((f) => ({ code: f.id, label: f.name })) } }}
              value={selected.join('|')}
              onChange={(v) => setChosen(String(v ?? ''))} />
            {selected.map((fid) => {
              const form = forms.find((f) => f.id === fid)
              if (!form) return null
              return (
                <SelectField key={fid}
                  spec={{ key: `seg-apply-group-${fid}`, label: `${form.name} — field group`, dataType: 'select', searchable: true,
                    options: { kind: 'static', options: (form.groups ?? []).map((g) => ({ code: g.id, label: g.title })) } }}
                  value={groups[fid] ?? (form.groups ?? []).find((g) => g.isHeader)?.id ?? ''}
                  onChange={(v) => setGroups((cur) => ({ ...cur, [fid]: String(v ?? '') }))} />
              )
            })}
          </>
        )}
    </Modal>
  )
}

/**
 * CF-FIX4-T6: values STAGE locally — add several, then ONE Save commits them all (the
 * lists CF-FIX2-T5 convention; no auto-save). A staged value may parent another staged
 * value (staged:<idx>, resolved to the real id on save). Pickers are the standardized
 * searchable select.
 */
function StagedValues({ def, onChanged, onError }: {
  def: SegmentDefDto; onChanged: () => void; onError: (msg: string | null) => void
}) {
  const [label, setLabel] = useState('')
  const [parent, setParent] = useState('')
  const [staged, setStaged] = useState<{ label: string; parent: string }[]>([])

  const save = useMutation({
    mutationFn: async () => {
      const ids: string[] = []
      for (const [i, s] of staged.entries()) {
        const parentId = s.parent.startsWith('staged:') ? ids[Number(s.parent.slice(7))] : (s.parent || null)
        const d = await addSegmentValue(def.id, { label: s.label, parentValueId: parentId, sort: def.values.length + i })
        const created = d.values.find((v) => v.label === s.label)
        ids.push(created?.id ?? '')
      }
    },
    onSuccess: () => { setStaged([]); onError(null); onChanged() },
    onError: (e) => onError(e instanceof Error ? e.message : 'Could not save the values.'),
  })
  const stage = () => {
    if (!label.trim()) { onError('Enter a label.'); return }
    setStaged((s) => [...s, { label: label.trim(), parent }])
    setLabel(''); setParent('')
  }
  const stagedLabel = (p: string) =>
    p.startsWith('staged:') ? `${staged[Number(p.slice(7))]?.label} (unsaved)` : def.values.find((v) => v.id === p)?.label ?? '—'

  return (
    <div style={{ marginTop: 8 }}>
      {staged.length > 0 && (
        <table>
          <tbody>
            {staged.map((s, i) => (
              <tr key={`staged-${i}`} style={{ background: 'var(--soft)' }}>
                <td style={{ fontWeight: 600 }}>{s.label} <span className="badge b-grey">unsaved</span></td>
                {def.hasHierarchy && <td className="hint">{s.parent ? stagedLabel(s.parent) : '—'}</td>}
                <td className="amt">
                  <Button variant="ghost" size="sm" icon="x" onClick={() => setStaged((x) => x.filter((_, j) => j !== i))} ariaLabel={`Discard staged ${s.label}`} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="grid g3" style={{ marginTop: 8 }}>
        <TextField spec={{ key: 'segv-label', label: 'Label', dataType: 'text', help: 'The dimension CODE derives from it, like every sourcing code.' }}
          value={label} onChange={(v) => setLabel(String(v ?? ''))} />
        {def.hasHierarchy && (def.values.length > 0 || staged.length > 0) && (
          <SelectField
            spec={{ key: 'segv-parent', label: 'Parent (optional)', dataType: 'select', searchable: true, placeholder: 'None — top level',
              options: { kind: 'static', options: [
                ...def.values.map((v) => ({ code: v.id, label: v.label })),
                ...staged.map((s, i) => ({ code: `staged:${i}`, label: `${s.label} (unsaved)` })),
              ] } }}
            value={parent} onChange={(v) => setParent(String(v ?? ''))} />
        )}
      </div>
      <div style={{ marginTop: 10, display: 'flex', gap: 8, alignItems: 'center' }}>
        <Button variant="outline" size="sm" icon="plus" onClick={stage}>Add value</Button>
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
  )
}

function EditValueModal({ def, value, onClose, onSaved }: {
  def: SegmentDefDto; value: SegmentValueDto; onClose: () => void; onSaved: () => void
}) {
  const [label, setLabel] = useState(value.label)
  const [parentId, setParentId] = useState(value.parentValueId ?? '')
  const [active, setActive] = useState(value.active)
  const [error, setError] = useState<string | null>(null)
  const save = useMutation({
    mutationFn: () => updateSegmentValue(def.id, value.id, { label, parentValueId: parentId || null, sort: value.sort, active }),
    onSuccess: onSaved,
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the value.'),
  })
  return (
    <Modal
      title={`Edit value — ${value.label}`} icon="edit"
      footer={<>
        <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
        <button type="button" className="btn btn-pri" disabled={!label.trim() || save.isPending} onClick={() => save.mutate()}>Save value</button>
      </>}
    >
      <TextField spec={{ key: 'ev-label', label: 'Label', dataType: 'text' }} value={label} onChange={(v) => setLabel(String(v ?? ''))} />
      {def.hasHierarchy && (
        <SelectField
          spec={{ key: 'ev-parent', label: 'Parent value (optional)', dataType: 'select', options: { kind: 'static', options: def.values.filter((x) => x.id !== value.id).map((x) => ({ code: x.id, label: x.label })) } }}
          value={parentId} onChange={(v) => setParentId(String(v ?? ''))} />
      )}
      <CheckboxField spec={{ key: 'ev-active', label: 'Active (offered for new assignment)', dataType: 'boolean' }} value={active} onChange={(v) => setActive(v === true)} />
      <p className="hint">The code (<span className="mono">{value.code}</span>) is the stored dimension key — it never changes.</p>
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

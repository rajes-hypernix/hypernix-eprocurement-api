import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getViewFields, createView, updateView, shareView,
  type SavedViewDto, type SavedViewFilterDto, type SavedViewColumnDto, type ViewFieldDto,
} from '../../api/client'
import { useIdentity } from '../../identity'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { DateField } from '../../ui/DateField'
import { MoneyField } from '../../ui/MoneyField'
import { NumberField } from '../../ui/NumberField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { CheckboxField } from '../../ui/CheckboxField'
import { Modal, Notice } from '../ui'

/**
 * Saved-views UI (D3 Phase 3). ViewPicker: system/shared/mine, default per screen.
 * ViewBuilder: criteria rows (field → operator → value; value inputs rendered by the
 * registry DataType through the D1 primitives), ordered columns, name, save, share
 * (gated by ManageSharedViews from the server-derived permission list).
 */

/** Operators valid per registry DataType — mirrors SavedViewService.ValidateDefinitionAsync. */
const OPERATORS_FOR: Record<string, string[]> = {
  Code: ['Eq', 'In', 'Contains'],
  Text: ['Eq', 'In', 'Contains'],
  Enum: ['Eq', 'In', 'Contains'],
  Tags: ['Eq', 'In', 'Contains'],
  Date: ['Eq', 'Between', 'Gte', 'Lte'],
  Instant: ['Eq', 'Between', 'Gte', 'Lte'],
  Money: ['Eq', 'Between', 'Gte', 'Lte'],
  Number: ['Eq', 'Between', 'Gte', 'Lte'],
  Bool: ['Eq'],
}
/** The ruled relative tokens (@today/@startOfMonth/@endOfMonth) + the D5-ruled @today±Nd
 *  token FORM ('days from today…' renders a number input, no new token names). */
const DATE_TOKENS = ['@today', '@startOfMonth', '@endOfMonth']
const CUSTOM_DATE = 'on date…'
const OFFSET_DAYS = 'days from today…'
const offsetOf = (v: string): number | null => {
  const m = /^@today([+-]\d{1,4})d$/.exec(v)
  return m ? Number(m[1]) : null
}

export function ViewPicker({ views, selectedId, onSelect, onNew, onEdit }: {
  views: SavedViewDto[]
  selectedId: string | null
  onSelect: (id: string) => void
  onNew: () => void
  onEdit: (view: SavedViewDto) => void
}) {
  const { code: me } = useIdentity()
  const selected = views.find((v) => v.id === selectedId)
  const asOptions = (vs: SavedViewDto[]) => vs.map((v) => ({ code: v.id, label: v.name }))
  const groups = [
    { label: 'System', options: asOptions(views.filter((v) => v.isSystem)) },
    { label: 'Shared', options: asOptions(views.filter((v) => !v.isSystem && v.isShared && v.ownerUserId !== me)) },
    { label: 'My views', options: asOptions(views.filter((v) => !v.isSystem && v.ownerUserId === me)) },
  ].filter((g) => g.options.length > 0)
  return (
    <div style={{ display: 'flex', gap: 6, alignItems: 'flex-end' }}>
      <SelectField
        spec={{ key: 'saved-view', label: 'Saved view', dataType: 'select', options: { kind: 'static', options: groups } }}
        value={selectedId ?? ''}
        onChange={(v) => { if (v) onSelect(String(v)) }}
      />
      <Button variant="ghost" size="sm" icon="plus" onClick={onNew} ariaLabel="New saved view">View…</Button>
      {selected && !selected.isSystem && selected.ownerUserId === me && (
        <Button variant="ghost" size="sm" icon="edit" onClick={() => onEdit(selected)} ariaLabel="Edit this view">Edit</Button>
      )}
    </div>
  )
}

interface CriterionRow { fieldKey: string; operator: string; value: string; value2: string }

const spec = (key: string, label: string, dataType: FieldSpec['dataType'], options?: string[] | null): FieldSpec => ({
  key, label, dataType,
  ...(options ? { options: { kind: 'static', options: options.map((o) => ({ code: o, label: o })) } } : {}),
})

/** The value input for one criterion — rendered by the registry DataType through D1 primitives. */
function CriterionValue({ field, value, onChange, idx }: {
  field: ViewFieldDto; value: string; onChange: (v: string) => void; idx: string
}) {
  const key = `crit-${idx}`
  switch (field.dataType) {
    case 'Date':
    case 'Instant': {
      const offset = offsetOf(value)
      const isOffset = offset !== null
      const isToken = (value.startsWith('@') && !isOffset) || value === ''
      const mode = isOffset ? OFFSET_DAYS : isToken ? value : CUSTOM_DATE
      return (
        <span style={{ display: 'inline-flex', gap: 4 }}>
          <SelectField
            spec={spec(`${key}-tok`, 'Value', 'select', [...DATE_TOKENS, OFFSET_DAYS, CUSTOM_DATE])}
            value={mode}
            onChange={(v) => onChange(
              v === CUSTOM_DATE ? new Date().toISOString().slice(0, 10)
              : v === OFFSET_DAYS ? '@today+90d'
              : String(v ?? ''))}
          />
          {isOffset && (
            <NumberField
              spec={spec(`${key}-off`, 'Days', 'number')}
              value={String(offset)}
              onChange={(v) => {
                const n = Number(v ?? 0)
                onChange(`@today${n >= 0 ? '+' : ''}${n}d`)
              }}
            />
          )}
          {!isToken && !isOffset && <DateField spec={spec(key, field.label, 'date')} value={value} onChange={(v) => onChange(String(v ?? ''))} />}
        </span>
      )
    }
    case 'Money':
      return <MoneyField spec={spec(key, field.label, 'money')} value={value} onChange={(v) => onChange(String(v ?? ''))} />
    case 'Number':
      return <NumberField spec={spec(key, field.label, 'number')} value={value} onChange={(v) => onChange(String(v ?? ''))} />
    case 'Bool':
      return <SelectField spec={spec(key, field.label, 'select', ['true', 'false'])} value={value} onChange={(v) => onChange(String(v ?? ''))} />
    case 'Enum':
      return <SelectField spec={spec(key, field.label, 'select', field.options ?? [])} value={value} onChange={(v) => onChange(String(v ?? ''))} />
    default:
      return <TextField spec={spec(key, field.label, 'text')} value={value} onChange={(v) => onChange(String(v ?? ''))} />
  }
}

export function ViewBuilder({ recordType, existing, defaultColumns, onClose, onSaved }: {
  recordType: string
  existing: SavedViewDto | null
  /** Column seed for a NEW view — the proof screen passes its system view's columns. */
  defaultColumns: string[]
  onClose: () => void
  onSaved: (view: SavedViewDto) => void
}) {
  const qc = useQueryClient()
  const { permissions } = useIdentity()
  const canShare = permissions.includes('ManageSharedViews')
  const { data: fields = [] } = useQuery({ queryKey: ['view-fields', recordType], queryFn: () => getViewFields(recordType), staleTime: Infinity })
  const byKey = useMemo(() => new Map(fields.map((f) => [f.fieldKey, f])), [fields])

  const [name, setName] = useState(existing?.name ?? '')
  const [shared, setShared] = useState(existing?.isShared ?? false)
  const [criteria, setCriteria] = useState<CriterionRow[]>(
    existing?.filters.map((f) => ({ fieldKey: f.fieldKey, operator: f.operator, value: f.value, value2: f.value2 ?? '' })) ?? [],
  )
  const [columns, setColumns] = useState<string[]>(existing?.columns.map((c) => c.fieldKey) ?? defaultColumns)
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: async () => {
      const filters: SavedViewFilterDto[] = criteria.map((c) => ({
        fieldKey: c.fieldKey, operator: c.operator, value: c.value, value2: c.operator === 'Between' ? c.value2 : null,
      }))
      const cols: SavedViewColumnDto[] = columns.map((k) => ({ fieldKey: k }))
      const req = { name, recordType, filters, columns: cols }
      const saved = existing ? await updateView(existing.id, req) : await createView(req)
      if (canShare && shared !== saved.isShared) return shareView(saved.id, shared)
      return saved
    },
    onSuccess: (view) => {
      void qc.invalidateQueries({ queryKey: ['views', recordType] })
      onSaved(view)
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the view.'),
  })

  const setRow = (i: number, patch: Partial<CriterionRow>) =>
    setCriteria((rows) => rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))
  const moveColumn = (i: number, d: -1 | 1) =>
    setColumns((cols) => {
      const next = [...cols]
      const j = i + d
      if (j < 0 || j >= next.length) return cols
      ;[next[i], next[j]] = [next[j], next[i]]
      return next
    })

  const firstField = fields[0]?.fieldKey ?? ''
  return (
    <Modal
      title={existing ? `Edit view — ${existing.name}` : 'New saved view'}
      icon="eye"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={save.isPending || !name.trim()} onClick={() => save.mutate()}>
            {existing ? 'Save changes' : 'Save view'}
          </button>
        </>
      }
    >
      <TextField spec={spec('view-name', 'View name', 'text')} value={name} onChange={(v) => setName(String(v ?? ''))} />

      <h4>Criteria</h4>
      {criteria.map((c, i) => {
        const field = byKey.get(c.fieldKey)
        const ops = OPERATORS_FOR[field?.dataType ?? 'Text'] ?? ['Eq']
        return (
          <div key={i} style={{ display: 'flex', gap: 6, alignItems: 'flex-end', marginBottom: 6, flexWrap: 'wrap' }}>
            <SelectField
              spec={{
                key: `crit-${i}-field`, label: 'Field', dataType: 'select',
                options: {
                  kind: 'static',
                  options: [
                    { label: 'Fields', options: fields.filter((f) => f.kind !== 'Custom' && f.kind !== 'Segment').map((f) => ({ code: f.fieldKey, label: f.label })) },
                    ...(fields.some((f) => f.kind === 'Custom')
                      ? [{ label: 'Custom fields', options: fields.filter((f) => f.kind === 'Custom').map((f) => ({ code: f.fieldKey, label: f.label })) }]
                      : []),
                    ...(fields.some((f) => f.kind === 'Segment')
                      ? [{ label: 'Segments', options: fields.filter((f) => f.kind === 'Segment').map((f) => ({ code: f.fieldKey, label: f.label })) }]
                      : []),
                  ],
                },
              }}
              value={c.fieldKey}
              onChange={(v) => setRow(i, { fieldKey: String(v ?? ''), operator: 'Eq', value: '', value2: '' })}
            />
            <SelectField
              spec={spec(`crit-${i}-op`, 'Operator', 'select', ops)}
              value={c.operator}
              onChange={(v) => setRow(i, { operator: String(v ?? 'Eq') })}
            />
            {field && <CriterionValue field={field} value={c.value} onChange={(v) => setRow(i, { value: v })} idx={`${i}-v1`} />}
            {field && c.operator === 'Between' && (
              <CriterionValue field={field} value={c.value2} onChange={(v) => setRow(i, { value2: v })} idx={`${i}-v2`} />
            )}
            <Button variant="ghost" size="sm" icon="x" onClick={() => setCriteria((rows) => rows.filter((_, j) => j !== i))} ariaLabel={`Remove criterion ${i + 1}`} />
          </div>
        )
      })}
      <Button variant="ghost" size="sm" icon="plus" ariaLabel="Add criterion"
        onClick={() => setCriteria((rows) => [...rows, { fieldKey: firstField, operator: 'Eq', value: '', value2: '' }])}>
        Add criterion
      </Button>

      <h4>Columns</h4>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {columns.map((k, i) => (
          <div key={k} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
            <span style={{ minWidth: 160 }}>{byKey.get(k)?.label ?? k}</span>
            <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${k} up`} onClick={() => moveColumn(i, -1)}>↑</button>
            <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${k} down`} onClick={() => moveColumn(i, 1)}>↓</button>
            <button type="button" className="btn btn-sm btn-out" aria-label={`Remove column ${k}`} onClick={() => setColumns((cols) => cols.filter((c) => c !== k))}>✕</button>
          </div>
        ))}
      </div>
      <SelectField
        spec={spec('add-col', 'Add column', 'select', fields.map((f) => f.fieldKey).filter((k) => !columns.includes(k)))}
        value=""
        onChange={(v) => { const k = String(v ?? ''); if (k) setColumns((cols) => [...cols, k]) }}
      />

      {canShare && (
        <CheckboxField
          spec={spec('view-shared', 'Shared (visible to everyone — publication)', 'boolean')}
          value={shared}
          onChange={(v) => setShared(v === true)}
        />
      )}

      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

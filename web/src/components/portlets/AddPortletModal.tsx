import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getMetricCatalog, getViews, type PortletUpsert } from '../../api/client'
import { Modal } from '../ui'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'
import { NumberField } from '../../ui/NumberField'
import { CheckboxField } from '../../ui/CheckboxField'
import { TILE_COLORS } from './ShortcutsPortlet'
import { recordTypeOptions } from '../../lib/recordTypeLabel'

/**
 * CF3-T9: the NetSuite "personalize bucket" — every portlet type addable from ONE dropdown,
 * each with its minimal honest config (the server validates: Shortcuts/Reminders/Scorecard/
 * Chart all need ≥1 item, so the bucket chains straight into each type's authoring).
 * KpiMeter and Reminders route to their EXISTING richer modals (passed in by the dashboard).
 */
const TYPES = [
  { code: 'KpiMeter', label: 'KPI meter (metric or saved view)' },
  { code: 'KpiScorecard', label: 'KPI scorecard (several metrics)' },
  { code: 'Reminders', label: 'Reminders (counts from saved views)' },
  { code: 'SavedViewList', label: 'Saved-view list (top-N rows)' },
  { code: 'Shortcuts', label: 'Shortcuts (tiles)' },
  { code: 'RecentRecords', label: 'Recent records' },
  { code: 'Chart', label: 'Chart (metric series by month)' },
]
const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']

const at = (portletType: string, title: string, configJson: string, savedViewId: string | null = null, width = 1): PortletUpsert =>
  ({ id: null, portletType, title, col: 0, row: 99, width, savedViewId, configJson })

export function AddPortletModal({ onClose, onAdd, onKpi, onReminder }: {
  onClose: () => void
  onAdd: (p: PortletUpsert) => void
  /** route to the existing richer modals */
  onKpi: () => void
  onReminder: () => void
}) {
  const [type, setType] = useState('KpiMeter')
  const [title, setTitle] = useState('')
  // per-type state
  const [metricIds, setMetricIds] = useState<string[]>([])
  const [recordType, setRecordType] = useState('PurchaseOrder')
  const [viewId, setViewId] = useState('')
  const [topN, setTopN] = useState('5')
  const [tileLabel, setTileLabel] = useState('')
  const [tileRoute, setTileRoute] = useState('dashboard')
  const [tileColor, setTileColor] = useState('')
  const [months, setMonths] = useState('12')

  const { data: catalog = [] } = useQuery({ queryKey: ['metric-catalog'], queryFn: getMetricCatalog, staleTime: Infinity })
  const { data: views = [] } = useQuery({ queryKey: ['views', recordType], queryFn: () => getViews(recordType), enabled: type === 'SavedViewList' })
  const valueMetrics = catalog.filter((m) => !m.isSeries)
  const seriesMetrics = catalog.filter((m) => m.isSeries)

  const build = (): PortletUpsert | null => {
    const t = title.trim()
    switch (type) {
      case 'KpiScorecard':
        return metricIds.length ? at(type, t || 'Scorecard', JSON.stringify({ items: metricIds.map((m) => ({ metricId: m, link: null })) }), null, 2) : null
      case 'SavedViewList':
        return viewId ? at(type, t || (views.find((v) => v.id === viewId)?.name ?? 'View'), JSON.stringify({ topN: Number(topN) || 5, route: null }), viewId, 2) : null
      case 'Shortcuts':
        return tileLabel.trim() ? at(type, t || 'Shortcuts', JSON.stringify({ items: [{ label: tileLabel.trim(), route: tileRoute, color: tileColor || null }] })) : null
      case 'RecentRecords':
        return at(type, t || 'Recent records', '{}')
      case 'Chart':
        return metricIds.length ? at(type, t || 'Trend', JSON.stringify({ seriesIds: metricIds, months: Number(months) || 12, minMonths: 3 }), null, 2) : null
      default:
        return null
    }
  }
  const ready = type === 'KpiMeter' || type === 'Reminders' || build() !== null

  const toggleMetric = (id: string, on: boolean) =>
    setMetricIds((cur) => (on ? [...cur, id] : cur.filter((x) => x !== id)))

  return (
    <Modal
      title="Add portlet" icon="dashboard"
      footer={<>
        <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
        <button type="button" className="btn btn-pri" disabled={!ready}
          onClick={() => {
            if (type === 'KpiMeter') { onClose(); onKpi(); return }
            if (type === 'Reminders') { onClose(); onReminder(); return }
            const p = build()
            if (p) onAdd(p)
          }}>
          {type === 'KpiMeter' || type === 'Reminders' ? 'Continue…' : 'Add portlet'}
        </button>
      </>}
    >
      <SelectField spec={{ key: 'ap-type', searchable: true, label: 'Portlet type', dataType: 'select', options: { kind: 'static', options: TYPES } }}
        value={type} onChange={(v) => { setType(String(v ?? 'KpiMeter')); setMetricIds([]) }} />
      {type !== 'KpiMeter' && type !== 'Reminders' && (
        <TextField spec={{ key: 'ap-title', label: 'Title (optional)', dataType: 'text' }} value={title} onChange={(v) => setTitle(String(v ?? ''))} />
      )}
      {(type === 'KpiScorecard' || type === 'Chart') && (
        <div style={{ maxHeight: 180, overflowY: 'auto' }}>
          {(type === 'Chart' ? seriesMetrics : valueMetrics).map((m) => (
            <CheckboxField key={m.id} spec={{ key: `ap-m-${m.id}`, label: m.label, dataType: 'boolean' }}
              value={metricIds.includes(m.id)} onChange={(v) => toggleMetric(m.id, v === true)} />
          ))}
        </div>
      )}
      {type === 'Chart' && (
        <NumberField spec={{ key: 'ap-months', label: 'Months', dataType: 'number', validation: { min: 3, max: 36 } }} value={months} onChange={(v) => setMonths(String(v ?? '12'))} />
      )}
      {type === 'SavedViewList' && (
        <>
          <SelectField spec={{ key: 'ap-rt', searchable: true, label: 'Record type', dataType: 'select', options: { kind: 'static', options: recordTypeOptions(RECORD_TYPES) } }}
            value={recordType} onChange={(v) => { setRecordType(String(v ?? 'PurchaseOrder')); setViewId('') }} />
          <SelectField spec={{ key: 'ap-view', searchable: true, label: 'Saved view', dataType: 'select', options: { kind: 'static', options: views.map((v) => ({ code: v.id, label: v.name })) } }}
            value={viewId} onChange={(v) => setViewId(String(v ?? ''))} />
          <NumberField spec={{ key: 'ap-topn', label: 'Rows (top N)', dataType: 'number', validation: { min: 1, max: 50 } }} value={topN} onChange={(v) => setTopN(String(v ?? '5'))} />
        </>
      )}
      {type === 'Shortcuts' && (
        <>
          <TextField spec={{ key: 'ap-tl', label: 'First tile label', dataType: 'text' }} value={tileLabel} onChange={(v) => setTileLabel(String(v ?? ''))} />
          <SelectField spec={{ key: 'ap-tr', searchable: true, label: 'Tile target page', dataType: 'select', options: { kind: 'static', options: [
            { code: 'dashboard', label: 'Dashboard' }, { code: 'views', label: 'Saved Views' }, { code: 'reqs', label: 'Requisitions' },
            { code: 'rfqs', label: 'RFQs' }, { code: 'pos', label: 'Purchase Orders' }, { code: 'invoices', label: 'Invoices' },
          ] } }} value={tileRoute} onChange={(v) => setTileRoute(String(v ?? 'dashboard'))} />
          <SelectField spec={{ key: 'ap-tc', searchable: true, label: 'Tile colour', dataType: 'select', options: { kind: 'static', options: TILE_COLORS.map((c) => ({ code: c.code, label: c.label })) } }}
            value={tileColor} onChange={(v) => setTileColor(String(v ?? ''))} />
        </>
      )}
      {type === 'RecentRecords' && <p className="hint">Shows your recently opened records — no configuration.</p>}
    </Modal>
  )
}

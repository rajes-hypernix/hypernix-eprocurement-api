import { useQuery } from '@tanstack/react-query'
import { getMetricValue, aggregateView, type PortletDto } from '../../api/client'
import { parseConfig, fmtMetric, type KpiMeterConfig } from './portletConfig'

/**
 * One KPI: backed by a SYSTEM METRIC or by a SAVED-VIEW AGGREGATE (count|sum|avg) — the
 * D4 gate's user-built KPI is the latter. Honest-null: "not yet available" is a state,
 * never a fabricated zero; target renders a threshold bar when configured.
 */
export function KpiMeterPortlet({ portlet, onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const cfg = parseConfig<KpiMeterConfig>(portlet.configJson, {})
  const metricBacked = !!cfg.metricId
  const { data: metric } = useQuery({
    queryKey: ['metric', cfg.metricId],
    queryFn: () => getMetricValue(cfg.metricId!),
    enabled: metricBacked,
  })
  const { data: agg } = useQuery({
    queryKey: ['view-agg', portlet.savedViewId, cfg.fn, cfg.fieldKey, cfg.groupBy],
    queryFn: () => aggregateView(portlet.savedViewId!, cfg.fn ?? 'count', cfg.fieldKey, cfg.groupBy),
    enabled: !metricBacked && !!portlet.savedViewId,
  })

  const value = metricBacked ? metric?.value : agg?.value
  const unit = metricBacked ? (metric?.unit ?? '') : (cfg.fn === 'count' ? '' : 'RM')
  const notYet = metricBacked ? metric?.notYetAvailable === true : agg != null && agg.value == null
  const loaded = metricBacked ? metric != null : agg != null

  return (
    <div
      className={`stat${cfg.link ? ' linkable' : ''}`}
      data-testid="kpi-meter"
      onClick={() => cfg.link && onNavigate(cfg.link)}
      style={{ cursor: cfg.link ? 'pointer' : undefined }}
    >
      {!loaded && <div className="num hint">…</div>}
      {loaded && notYet && (
        <>
          <div className="num hint" aria-label={`${portlet.title}: not yet available`}>—</div>
          <div className="sub">Not yet available — fills in as real data accrues.</div>
        </>
      )}
      {loaded && !notYet && value != null && (
        <>
          <div className="num">{fmtMetric(value, unit)}</div>
          {cfg.target != null && (
            <div className="sub">
              target {fmtMetric(cfg.target, unit)} ·{' '}
              <span className={`badge ${value >= cfg.target ? 'b-green' : 'b-amber'}`}>
                {value >= cfg.target ? 'met' : `${fmtMetric(cfg.target - value, unit)} to go`}
              </span>
            </div>
          )}
          {/* D6 sliced KPI: one row per segment value, Unassigned included — honest-null on dimensions. */}
          {agg?.groups && agg.groups.length > 0 && (
            <div className="sub" data-testid="kpi-slices" style={{ marginTop: 6, display: 'flex', flexDirection: 'column', gap: 2 }}>
              {agg.groups.map((g) => (
                <div key={g.key} style={{ display: 'flex', justifyContent: 'space-between', gap: 10 }}>
                  <span className={g.key === '__unassigned' ? 'hint' : undefined}>{g.label}</span>
                  <span>{g.value == null ? '—' : fmtMetric(g.value, unit)}</span>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}

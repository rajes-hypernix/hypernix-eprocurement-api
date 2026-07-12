import { useQueries } from '@tanstack/react-query'
import { getMetricSeries, type PortletDto } from '../../api/client'
import { parseConfig, fmtMetric, type ChartConfig } from './portletConfig'

// Series colours ride the existing demo-chart custom properties (tokens.css).
const COLOR_VARS = ['--da-materials', '--da-services', '--da-mro']

/**
 * Metric-over-months stacked bars — hand-rolled SVG per the house precedent, ACTUALS ONLY
 * (the mock's dashed forecast is not rebuilt — BACKLOG product-roadmap row). Below the
 * config's minMonths of populated history it renders the honest empty state instead of a
 * chart that implies a trend; unbucketed rows are surfaced, never silently dropped.
 */
export function ChartPortlet({ portlet }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const cfg = parseConfig<ChartConfig>(portlet.configJson, { seriesIds: [], months: 12, minMonths: 3 })
  const results = useQueries({
    queries: cfg.seriesIds.map((id) => ({ queryKey: ['metric-series', id, cfg.months], queryFn: () => getMetricSeries(id, cfg.months) })),
  })
  const series = results.map((r) => r.data).filter((s): s is NonNullable<typeof s> => s != null)
  if (series.length !== cfg.seriesIds.length) return <div className="hint">…</div>

  const buckets = series[0].buckets.map((b) => b.bucket)
  const totals = buckets.map((_, i) => series.reduce((sum, s) => sum + (s.buckets[i]?.value ?? 0), 0))
  const populated = totals.filter((t) => t > 0).length
  const unbucketed = series.reduce((n, s) => n + s.unbucketedCount, 0)

  if (populated < cfg.minMonths) {
    return (
      <div data-testid="chart-empty" style={{ padding: '18px 4px' }}>
        <p className="hint">
          Not enough history yet ({populated} of {cfg.minMonths} months with data) — this chart fills in
          as real records accrue. Nothing here is illustrative.
        </p>
      </div>
    )
  }

  const W = 560; const H = 160; const PAD = 28
  const max = Math.max(...totals, 1)
  const bw = (W - PAD) / buckets.length
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H + 22}`} role="img" aria-label={portlet.title} style={{ width: '100%', height: 'auto' }}>
        {buckets.map((b, i) => {
          let y = H
          return (
            <g key={b}>
              {series.map((s, si) => {
                const v = s.buckets[i]?.value ?? 0
                const h = (v / max) * (H - 10)
                y -= h
                return <rect key={s.id} x={PAD + i * bw + 2} y={y} width={bw - 4} height={h} fill={`var(${COLOR_VARS[si % COLOR_VARS.length]}, var(--teal))`} />
              })}
              {(i === 0 || i === buckets.length - 1 || i % 3 === 0) && (
                <text x={PAD + i * bw + bw / 2} y={H + 14} textAnchor="middle" fontSize={9} fill="var(--ink-soft, #666)">{b.slice(2)}</text>
              )}
            </g>
          )
        })}
        <text x={0} y={12} fontSize={9} fill="var(--ink-soft, #666)">{fmtMetric(max, series[0].unit === 'count' ? '' : series[0].unit)}</text>
      </svg>
      {series.length > 1 && (
        <div style={{ display: 'flex', gap: 12, marginTop: 4 }}>
          {series.map((s, si) => (
            <span key={s.id} className="hint" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
              <span style={{ width: 10, height: 10, background: `var(${COLOR_VARS[si % COLOR_VARS.length]}, var(--teal))`, display: 'inline-block' }} />
              {s.label}
            </span>
          ))}
        </div>
      )}
      {unbucketed > 0 && <p className="hint">{unbucketed} record(s) not yet dated — excluded from the chart.</p>}
    </div>
  )
}

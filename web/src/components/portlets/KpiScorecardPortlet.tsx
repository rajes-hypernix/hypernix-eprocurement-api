import { useQueries } from '@tanstack/react-query'
import { getMetricValue, type PortletDto } from '../../api/client'
import { parseConfig, fmtMetric, type KpiScorecardConfig } from './portletConfig'

/** A grid of system-metric stat cards — the migrated role stat cards live here. */
export function KpiScorecardPortlet({ portlet, onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const cfg = parseConfig<KpiScorecardConfig>(portlet.configJson, { items: [] })
  const results = useQueries({
    queries: cfg.items.map((i) => ({ queryKey: ['metric', i.metricId], queryFn: () => getMetricValue(i.metricId) })),
  })
  return (
    <div className="grid g4">
      {cfg.items.map((item, i) => {
        const m = results[i]?.data
        return (
          <div
            key={item.metricId}
            className={`card stat tone-teal${item.link ? ' linkable' : ''}`}
            onClick={() => item.link && onNavigate(item.link)}
          >
            <div className="lbl">{m?.label ?? '…'}</div>
            <div className="num">{m == null ? '…' : m.notYetAvailable || m.value == null ? '—' : fmtMetric(m.value, m.unit === 'count' ? '' : m.unit)}</div>
            {m?.notYetAvailable && <div className="sub">not yet available</div>}
          </div>
        )
      })}
    </div>
  )
}

import { useQueries } from '@tanstack/react-query'
import { aggregateView, type PortletDto } from '../../api/client'
import { parseConfig, type RemindersConfig } from './portletConfig'

/**
 * Live counts over saved views; clicking opens the list screen WITH that view picked
 * (the D3 integration — route/view/{id}).
 */
export function RemindersPortlet({ portlet, onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const cfg = parseConfig<RemindersConfig>(portlet.configJson, { items: [] })
  const results = useQueries({
    queries: cfg.items.map((i) => ({ queryKey: ['view-agg', i.savedViewId, 'count'], queryFn: () => aggregateView(i.savedViewId, 'count') })),
  })
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {cfg.items.map((item, i) => (
        <button
          key={item.savedViewId}
          type="button"
          className="btn btn-out"
          style={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}
          onClick={() => onNavigate(`${item.route}/view/${item.savedViewId}`)}
        >
          <span>{item.label}</span>
          <span className="badge b-teal">{results[i]?.data?.value ?? '…'}</span>
        </button>
      ))}
    </div>
  )
}

import { useQuery } from '@tanstack/react-query'
import { runView, type PortletDto } from '../../api/client'
import { EmptyState } from '../ui'
import { parseConfig, fmtMetric, type SavedViewListConfig } from './portletConfig'
import { LIST_ROUTE } from '../../lib/listRoutes'
import { fmtDay } from '../../lib/format'

/** Top-N rows of a saved view (e.g. the seeded "Recent purchase orders") — the honest
 *  replacement for the mock RECENT_POS table. Cells format by the run's column DataType. */
export function SavedViewListPortlet({ portlet, onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const cfg = parseConfig<SavedViewListConfig>(portlet.configJson, { topN: 5 })
  // D7.5: top-N goes SERVER-side (size=topN — the client-side over-fetch dies, ruled).
  const { data: run } = useQuery({
    queryKey: ['view-run', portlet.savedViewId, cfg.topN],
    queryFn: () => runView(portlet.savedViewId!, 1, cfg.topN),
    enabled: !!portlet.savedViewId,
  })
  if (!run) return <div className="hint">…</div>
  const rows = run.rows
  const cell = (v: unknown, dataType: string) => {
    if (v == null) return '—'
    if (dataType === 'Money') return fmtMetric(Number(v), 'RM')
    if (dataType === 'Instant' || dataType === 'Date') return fmtDay(String(v))
    return String(v)
  }
  return (
    <div>
      <table>
        <thead><tr>{run.columns.map((c) => <th key={c.fieldKey}>{c.label}</th>)}</tr></thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={String(r.Id ?? i)}>
              {run.columns.map((c, j) => (
                <td key={c.fieldKey} style={j === 0 ? { fontWeight: 700, color: 'var(--teal)' } : undefined}>
                  {cell(r[c.fieldKey], c.dataType)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan={run.columns.length}><EmptyState>Nothing yet.</EmptyState></td></tr>}
        </tbody>
      </table>
      {/* D7.5 (the NetSuite pattern, ruled): >N rows → the record type's list screen
          opens WITH this portlet's view selected — same rows, now paged. */}
      {run.total > cfg.topN && portlet.savedViewId && LIST_ROUTE[run.recordType] && (
        <button
          type="button" className="btn btn-sm btn-ghost" data-testid="view-all"
          onClick={() => onNavigate(`${LIST_ROUTE[run.recordType]}/view/${portlet.savedViewId}`)}
        >
          View all ({run.total}) →
        </button>
      )}
      {run.total <= cfg.topN && cfg.route && (
        <button type="button" className="btn btn-sm btn-ghost" onClick={() => onNavigate(cfg.route!)}>View all</button>
      )}
    </div>
  )
}

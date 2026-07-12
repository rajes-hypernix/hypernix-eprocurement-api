import { Button } from '../../ui/Button'

/**
 * The one pager (D7.5): "x–y of total" + Prev/Next, shared by every view-driven list
 * (RfqList and the rollout mounts). Renders nothing when everything fits on one page —
 * screens with small data look exactly as they did before paging existed.
 */
export function Pager({ page, size, total, onPage }: {
  page: number; size: number; total: number; onPage: (page: number) => void
}) {
  if (total <= size && page === 1) return null
  const from = (page - 1) * size + 1
  const to = Math.min(page * size, total)
  const last = Math.max(1, Math.ceil(total / size))
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 2px' }} data-testid="pager">
      <span className="hint">{from}–{to} of {total}</span>
      <div className="spacer" />
      <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)} ariaLabel="Previous page">‹ Prev</Button>
      <Button variant="outline" size="sm" disabled={page >= last} onClick={() => onPage(page + 1)} ariaLabel="Next page">Next ›</Button>
    </div>
  )
}

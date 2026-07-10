import type { RequisitionDto } from '../api/client'

type PrLine = NonNullable<RequisitionDto['lines']>[number]

// PR header lifecycle → existing dot-badge tone + label (mirrors the mockup's prStatusBadge,
// but uses the app's own design tokens).
const HEADER: Record<string, [string, string]> = {
  Draft: ['b-grey', 'Draft'],
  Submitted: ['b-blue', 'Submitted'],
  PartiallySourced: ['b-amber', 'Partially sourced'],
  Sourced: ['b-teal', 'Sourced'],
  Cancelled: ['b-red', 'Cancelled'],
}

export function PrHeaderBadge({ status }: { status?: string | null }) {
  const [tone, label] = HEADER[status ?? ''] ?? ['b-grey', status ?? '—']
  return <span className={`badge ${tone}`}>{label}</span>
}

// ---- Source eligibility (single predicate reused by Consolidate AND Confirm-lines) ----
// A line is source-eligible only when it is Open AND its PR header is Submitted or
// PartiallySourced. Draft and Cancelled PRs are never sourceable (PR-MODULE-SPEC §4.2, D1).
export const isSourceablePr = (headerStatus?: string | null): boolean =>
  headerStatus === 'Submitted' || headerStatus === 'PartiallySourced'

export const isLineSourceable = (pr: { headerStatus?: string | null }, line: PrLine): boolean =>
  line.lifecycleStatus === 'Open' && isSourceablePr(pr.headerStatus)

export const PR_KANBAN_COLUMNS: { key: string; label: string }[] = [
  { key: 'Draft', label: 'Draft' },
  { key: 'Submitted', label: 'Submitted' },
  { key: 'PartiallySourced', label: 'Partially sourced' },
  { key: 'Sourced', label: 'Sourced' },
  { key: 'Cancelled', label: 'Cancelled' },
]

// Per-line state chip. "No quotes" is the derived state (Open + last sourcing link Returned).
export function lineChip(l: PrLine): { cls: string; label: string } {
  const s = l.lifecycleStatus
  const ref = l.ref ? ` · ${l.ref}` : ''
  if (s === 'Open' && l.noQuotes) return { cls: 'w', label: `No quotes${ref}` }
  if (s === 'Open') return { cls: 'a', label: 'Open' }
  if (s === 'InDraftRfq') return { cls: 'r', label: 'In draft RFQ' }
  if (s === 'InRfq') return { cls: 'r', label: `In ${l.ref ?? 'RFQ'}` }
  if (s === 'Awarded') return { cls: 'aw', label: `Awarded${ref}` }
  if (s === 'Cancelled') return { cls: 'x', label: 'Cancelled' }
  return { cls: 'x', label: 'Closed' }
}

export function LineChip({ line }: { line: PrLine }) {
  const { cls, label } = lineChip(line)
  return <span className={`lst ${cls}`}><span className="d" />{label}</span>
}

import { Icon } from '../components/Icon'

// Mirrors the prototype's statusBadge() — dot-badge tone + exact label per status.
const STATUS: Record<string, [string, string]> = {
  Draft: ['b-grey', 'Draft'],
  Open: ['b-amber', 'Open · Awaiting bids'],
  Closed: ['b-blue', 'Closed · Ready to open'],
  Evaluation: ['b-teal', 'Under evaluation'],
  Awarded: ['b-green', 'Awarded'],
  Cancelled: ['b-red', 'Cancelled'],
}

// Bid-progress sub-status for Open RFQs: awaiting → partial → all in.
export function bidProgress(bidCount?: number, invitedCount?: number): { tone: string; label: string } | null {
  const inv = invitedCount ?? 0
  const bids = bidCount ?? 0
  if (inv === 0) return null
  if (bids === 0) return { tone: 'b-grey', label: 'Awaiting bids' }
  if (bids >= inv) return { tone: 'b-green', label: `All bids in · ${bids}/${inv}` }
  return { tone: 'b-amber', label: `Partially bid · ${bids}/${inv}` }
}

export function BidProgressBadge({ bidCount, invitedCount }: { bidCount?: number; invitedCount?: number }) {
  const p = bidProgress(bidCount, invitedCount)
  return p ? <span className={`badge ${p.tone}`}>{p.label}</span> : null
}

export function RfqStatusBadge({ status }: { status: string | null | undefined }) {
  const [tone, label] = STATUS[status ?? ''] ?? ['b-grey', status ?? '']
  return <span className={`badge ${tone}`}>{label}</span>
}

// Mirrors envTag() — "🔒 Dual" (muted env-tag) or "Single".
export function EnvTag({ envelope }: { envelope: string | null | undefined }) {
  return envelope === 'Dual' ? (
    <span className="env-tag"><Icon name="lock" size={12} /> Dual</span>
  ) : (
    <span className="env-tag">Single</span>
  )
}

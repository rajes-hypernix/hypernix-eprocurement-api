import { useQuery } from '@tanstack/react-query'
import { getMyInvitations, type InvitationDto, type PortletDto } from '../../api/client'
import { Icon } from '../Icon'
import { EmptyState } from '../ui'
import { EnvTag } from '../../lib/rfqStatus'
import { useIdentity } from '../../identity'
import { fmtDay } from '../../lib/format'

function invOutcome(inv: InvitationDto): { tone: string; label: string } {
  if (inv.status === 'Awarded') return { tone: 'b-green', label: 'Closed · Awarded' }
  if (inv.bidSubmitted) return { tone: 'b-green', label: 'Bid submitted' }
  if (inv.bidDraft) return { tone: 'b-amber', label: 'Draft saved' }
  if (inv.status === 'Open') return { tone: 'b-blue', label: 'Action needed' }
  return { tone: 'b-grey', label: 'Closed' }
}

/**
 * The vendor's primary work surface (OD-D4-2: the ruled eighth portlet type) — the RFQ
 * invitations table with bid-status badges and Start-bid/Continue-draft actions, lifted
 * verbatim from the retiring Dashboard component. Computed, real, vendor-scoped.
 */
export function MyInvitationsPortlet({ onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const { persona } = useIdentity()
  const { data: invitations = [] } = useQuery({ queryKey: ['my-invitations'], queryFn: getMyInvitations })
  return (
    <div>
      <div className="chead"><div className="spacer" /><span className="hint">{persona?.vendorName ?? 'your company'}</span></div>
      <table>
        <thead><tr><th>RFQ</th><th>Title</th><th>Envelope</th><th>Closes</th><th>Status</th><th /></tr></thead>
        <tbody>
          {invitations.map((inv) => {
            const o = invOutcome(inv)
            const canBid = inv.status === 'Open'
            const label = inv.bidSubmitted ? 'Edit bid' : inv.bidDraft ? 'Continue draft' : canBid ? 'Start bid' : 'View'
            return (
              <tr key={inv.rfqId} className="rowlink" onClick={() => inv.rfqId && onNavigate(`bid/${inv.rfqId}`)}>
                <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{inv.code}</td>
                <td>{inv.title}</td>
                <td><EnvTag envelope={inv.envelope} /></td>
                <td>{fmtDay(inv.closesUtc)}</td>
                <td><span className={`badge ${o.tone}`}>{o.label}</span></td>
                <td className="amt"><span className={`btn btn-sm ${canBid && !inv.bidSubmitted ? 'btn-pri' : 'btn-ghost'}`}>{label} <Icon name="chev" size={13} /></span></td>
              </tr>
            )
          })}
          {invitations.length === 0 && <tr><td colSpan={6}><EmptyState>No invitations yet.</EmptyState></td></tr>}
        </tbody>
      </table>
    </div>
  )
}

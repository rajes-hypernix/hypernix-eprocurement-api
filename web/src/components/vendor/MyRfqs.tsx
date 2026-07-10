import { useQuery } from '@tanstack/react-query'
import { getMyInvitations, type InvitationDto } from '../../api/client'
import { Icon } from '../Icon'
import { useIdentity } from '../../identity'
import { fmtDay } from '../../lib/format'

function outcome(inv: InvitationDto): { tone: string; label: string } {
  if (inv.status === 'Awarded') return { tone: 'b-green', label: 'Closed · Awarded' }
  if (inv.bidSubmitted) return { tone: 'b-green', label: 'Bid submitted' }
  // A declined invitation is NOT "action needed" — the vendor opted out (they can still reverse
  // from the bid page). Show the declined state, not a bid prompt (Slice K / K3c).
  if (inv.invitationStatus === 'Declined') return { tone: 'b-amber', label: 'Declined' }
  if (inv.bidDraft) return { tone: 'b-amber', label: 'Draft saved' }
  if (inv.status === 'Open') return { tone: 'b-blue', label: 'Action needed' }
  return { tone: 'b-grey', label: 'Closed' }
}

export function MyRfqs({ onOpen, mode = 'invitations' }: { onOpen: (id: string) => void; mode?: 'invitations' | 'bids' }) {
  const { persona } = useIdentity()
  const { data: invitations = [] } = useQuery({ queryKey: ['my-invitations'], queryFn: getMyInvitations })

  const rows = mode === 'bids' ? invitations.filter((i) => i.bidSubmitted || i.bidDraft) : invitations

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>{mode === 'bids' ? 'My Bids' : 'RFQ Invitations'}</h1>
          <p>
            {mode === 'bids'
              ? 'Bids you have submitted or saved as draft.'
              : `Sourcing events ${persona?.vendorName ?? 'your company'} has been invited to bid on.`}
          </p>
        </div>
      </div>
      <div className="card">
        <table>
          <thead>
            <tr>
              <th>RFQ</th>
              <th>Title</th>
              <th>Buyer</th>
              <th>Envelope</th>
              <th>Closes</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((inv) => {
              const o = outcome(inv)
              const declined = inv.invitationStatus === 'Declined'
              const canBid = inv.status === 'Open' && !declined
              // A declined vendor opens the RFQ to reconsider, not to "Start bid".
              const label = inv.bidSubmitted ? 'Edit bid' : inv.bidDraft ? 'Continue draft' : declined ? 'Reconsider' : canBid ? 'Start bid' : 'View'
              return (
                <tr key={inv.rfqId} className="rowlink" onClick={() => inv.rfqId && onOpen(inv.rfqId)}>
                  <td style={{ fontWeight: 700, color: 'var(--teal)' }}>{inv.code}</td>
                  <td>{inv.title}</td>
                  <td>Sarawak Petchem</td>
                  <td>
                    {inv.envelope === 'Dual' ? (
                      <span className="badge b-blue"><Icon name="lock" size={12} /> Dual</span>
                    ) : (
                      <span className="badge b-grey">Single</span>
                    )}
                  </td>
                  <td>{fmtDay(inv.closesUtc)}</td>
                  <td>
                    <span className={`badge ${o.tone}`}>{o.label}</span>
                    {/* IntendToBid isn't captured by the bid badges or the primary outcome, so surface it
                        as a secondary chip. (Declined is now the primary outcome badge — K3c.) */}
                    {inv.invitationStatus === 'IntendToBid' && !inv.bidSubmitted && <span className="badge b-teal" style={{ marginLeft: 6 }}>Intending</span>}
                  </td>
                  <td className="amt">
                    <span className={`btn btn-sm ${canBid && !inv.bidSubmitted ? 'btn-pri' : 'btn-ghost'}`}>
                      {label} <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              )
            })}
            {rows.length === 0 && (
              <tr><td colSpan={7}><div className="empty">No {mode === 'bids' ? 'bids' : 'invitations'} yet.</div></td></tr>
            )}
          </tbody>
        </table>
      </div>
    </>
  )
}

import { useQuery } from '@tanstack/react-query'
import { getDashboard, getMyInvitations, type InvitationDto } from '../api/client'
import { Icon } from './Icon'
import { EmptyState, Spinner } from './ui'
import { EnvTag } from '../lib/rfqStatus'
import { useIdentity } from '../identity'
import { fmtDay } from '../lib/format'
import { DashboardAnalyticsPanel } from './dashboard/DashboardAnalyticsPanel'

function invOutcome(inv: InvitationDto): { tone: string; label: string } {
  if (inv.status === 'Awarded') return { tone: 'b-green', label: 'Closed · Awarded' }
  if (inv.bidSubmitted) return { tone: 'b-green', label: 'Bid submitted' }
  if (inv.bidDraft) return { tone: 'b-amber', label: 'Draft saved' }
  if (inv.status === 'Open') return { tone: 'b-blue', label: 'Action needed' }
  return { tone: 'b-grey', label: 'Closed' }
}

export function Dashboard({ onNavigate }: { onNavigate: (key: string) => void }) {
  const { isVendor, roles, persona } = useIdentity()
  const { data: dash, isPending } = useQuery({ queryKey: ['dashboard'], queryFn: getDashboard })
  const isBuyer = roles.includes('Buyer') || roles.includes('Admin')
  const { data: invitations = [] } = useQuery({ queryKey: ['my-invitations'], queryFn: getMyInvitations, enabled: isVendor })

  if (isPending || !dash) return <Spinner />

  return (
    <>
      <div className="pagehead">
        <div><h1>{dash.title}</h1><p>{dash.subtitle}</p></div>
      </div>

      <div className="grid g4" style={{ marginBottom: 18 }}>
        {(dash.cards ?? []).map((c, i) => (
          <div
            key={i}
            className={`card stat tone-${c.tone ?? 'teal'}${c.link ? ' linkable' : ''}`}
            onClick={() => c.link && onNavigate(c.link)}
          >
            <div className="lbl">{c.label}</div>
            <div className="num">{c.value}</div>
            <div className="sub">{c.sub}</div>
          </div>
        ))}
      </div>

      {/* DEMO: analytics panel replaces the old Active RFQs table (buyer/admin only). Remove after demo → src/mock + src/components/dashboard. */}
      {isBuyer && <DashboardAnalyticsPanel onNavigate={onNavigate} />}

      {isVendor && (
        <div className="card">
          <div className="chead"><h3>RFQ invitations</h3><div className="spacer" /><span className="hint">{persona?.vendorName ?? 'your company'}</span></div>
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
      )}
    </>
  )
}

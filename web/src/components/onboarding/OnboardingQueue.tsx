import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getOnboardingApplications, resendOnboardingInvitation, revokeOnboardingInvitation, type OnboardingInvitation } from '../../api/client'
import { Icon } from '../Icon'
import { Spinner, EmptyState, Modal, ConfirmModal } from '../ui'

// Where the vendor still needs the link (hasn't submitted) — resend / revoke are offered while the
// magic link is still live. Revoking kills the link (it stops resolving).
const RESENDABLE = new Set(['Invited', 'InProgress', 'Expired'])

const STATUS_CLS: Record<string, string> = {
  Invited: 'b-grey', InProgress: 'b-blue', Submitted: 'b-amber', UnderReview: 'b-teal',
  ClarificationRequested: 'b-amber', Resubmitted: 'b-blue', Approved: 'b-green', Rejected: 'b-red',
  Expired: 'b-grey', Revoked: 'b-grey', Withdrawn: 'b-grey',
}
const label = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2')

/** Buyer's onboarding queue — applications by status with a clarification-round count (SPEC §7). */
export function OnboardingQueue({ onNavigate }: { onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const { data: apps = [], isPending } = useQuery({ queryKey: ['onboarding-apps'], queryFn: getOnboardingApplications })
  const [resent, setResent] = useState<OnboardingInvitation | null>(null)
  const [confirmRevoke, setConfirmRevoke] = useState<{ invitationId: string; code: string } | null>(null)
  const resend = useMutation({ mutationFn: (invitationId: string) => resendOnboardingInvitation(invitationId), onSuccess: setResent })
  const revoke = useMutation({
    mutationFn: (invitationId: string) => revokeOnboardingInvitation(invitationId),
    onSuccess: () => { void qc.invalidateQueries({ queryKey: ['onboarding-apps'] }); setConfirmRevoke(null) },
  })

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Onboarding</h1>
          <p>Applications in progress. Approve to promote into the Vendor Master.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri btn-sm" onClick={() => onNavigate('vendors/new')}>
          <Icon name="plus" size={15} /> New vendor
        </button>
      </div>

      {isPending ? <Spinner /> : apps.length === 0 ? (
        <EmptyState icon="vendor">No onboarding applications yet. Invite a vendor to get started.</EmptyState>
      ) : (
        <div className="card">
          <table>
            <thead><tr><th>Application</th><th>Vendor</th><th>Type</th><th>Received</th><th>Status</th><th /></tr></thead>
            <tbody>
              {apps.map((a) => (
                <tr key={a.id} className="rowlink" onClick={() => onNavigate(`onboarding/${a.id}`)}>
                  <td className="mono" style={{ fontWeight: 600 }}>{a.code}</td>
                  <td style={{ fontWeight: 600 }}>{a.name || '—'}</td>
                  <td><span className={`badge ${a.type === 'SWEC' ? 'b-teal' : 'b-amber'}`}>{a.type}</span></td>
                  <td className="hint">{a.submittedUtc ? a.submittedUtc.slice(0, 10) : a.createdUtc.slice(0, 10)}</td>
                  <td>
                    <span className={`badge ${STATUS_CLS[a.status] ?? 'b-grey'}`}>{label(a.status)}</span>
                    {a.openRoundNo != null && <span className="hint"> · rd {a.openRoundNo}</span>}
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div className="rowactions">
                      {RESENDABLE.has(a.status) && a.invitationId && (
                        <button type="button" className="btn btn-ghost btn-sm" disabled={resend.isPending}
                          onClick={(e) => { e.stopPropagation(); resend.mutate(a.invitationId!) }}>
                          <Icon name="send" size={13} /> Resend link
                        </button>
                      )}
                      {RESENDABLE.has(a.status) && a.invitationId && (
                        <button type="button" className="btn btn-ghost btn-sm" style={{ color: 'var(--red)' }}
                          onClick={(e) => { e.stopPropagation(); setConfirmRevoke({ invitationId: a.invitationId!, code: a.code }) }}>
                          <Icon name="x" size={13} /> Revoke
                        </button>
                      )}
                      <button type="button" className="btn btn-out btn-sm" onClick={(e) => { e.stopPropagation(); onNavigate(`onboarding/${a.id}`) }}>
                        Review <Icon name="chev" size={13} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {confirmRevoke && (
        <ConfirmModal
          icon="x" title={`Revoke the invitation for ${confirmRevoke.code}?`}
          body="This permanently disables the vendor's magic link — it will stop resolving and they can no longer open or submit their application. The row stays visible as Revoked. You can re-invite later if needed."
          cancelLabel="Keep active" confirmLabel="Revoke link" danger busy={revoke.isPending}
          onCancel={() => setConfirmRevoke(null)} onConfirm={() => revoke.mutate(confirmRevoke.invitationId)}
        />
      )}

      {resent && (
        <Modal title="Link resent" icon="send" footer={<button type="button" className="btn btn-pri" onClick={() => setResent(null)}>Done</button>}>
          <p style={{ marginTop: 0 }}>A fresh secure link for <b>{resent.applicationCode}</b> has been emailed to <b>{resent.email}</b> (expires in 14 days).</p>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>Magic link (demo)</label>
            <input readOnly value={resent.magicLink} aria-label="Magic link" onFocus={(e) => e.target.select()} />
          </div>
        </Modal>
      )}
    </>
  )
}

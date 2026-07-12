import { useMemo, useState, type ReactNode } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  closeRfq, cancelRfq, inviteVendorToRfq, rescindInvitation, extendRfq, type RfqDetail,
} from '../../api/client'
import { Icon } from '../Icon'
import { ConfirmModal, EmptyState } from '../ui'
import { fmt, fmtDay } from '../../lib/format'
import { RfqStatusBadge } from '../../lib/rfqStatus'
import {
  InvitationStatusBadge, ReasonModal, ExtendModal, AddVendorModal, ActivityTimeline, useReasonLabeler,
} from './RfqGovernance'
import { CustomFieldsSection } from '../customfields/CustomFieldsSection'
import { SegmentsSection } from '../segments/SegmentsSection'

// Pre-bid invitation statuses a buyer may still rescind (never once a bid is submitted — G3).
const RESCINDABLE = new Set(['Invited', 'Viewed', 'IntendToBid', 'Declined'])

// Adaptive RFQ detail hub: stat tiles, status action, invitation lifecycle table + governance
// (rescind / add vendor / extend), the activity timeline, and line items.
export function RfqDetailHub({ rfq, onBack, onNavigate }: { rfq: RfqDetail; onBack: () => void; onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const dual = rfq.envelope === 'Dual'
  const label = useReasonLabeler()

  const invitations = rfq.invitations ?? []
  const live = rfq.invitedVendors ?? []                       // excludes Rescinded (API-derived)
  const received = live.filter((v) => v.submitted).length     // Bids received denominator = live invitations
  const canGovern = rfq.status === 'Draft' || rfq.status === 'Open'   // invite/rescind window
  const categoryOf = useMemo(() => new Map(live.map((v) => [v.vendorId, v.category])), [live])
  // Non-Rescinded invitations block a re-invite; Rescinded vendors are re-invitable (T8).
  const invitedStatus = useMemo(
    () => new Map(invitations.filter((i) => i.status !== 'Rescinded').map((i) => [i.vendorId ?? '', i.status ?? ''])),
    [invitations])

  const [confirm, setConfirm] = useState<'close' | 'cancel' | null>(null)
  const [rescindVendor, setRescindVendor] = useState<string | null>(null)
  const [showAdd, setShowAdd] = useState(false)
  const [showExtend, setShowExtend] = useState(false)
  const [addErr, setAddErr] = useState<string | null>(null)

  const inval = () => { void qc.invalidateQueries({ queryKey: ['rfq', rfq.id] }); void qc.invalidateQueries({ queryKey: ['rfqs'] }) }
  const close = useMutation({ mutationFn: () => closeRfq(rfq.id!), onSuccess: () => { inval(); setConfirm(null) } })
  const cancel = useMutation({ mutationFn: () => cancelRfq(rfq.id!), onSuccess: () => { inval(); setConfirm(null) } })
  const rescind = useMutation({
    mutationFn: (v: { vendorId: string; code: string; note: string | null }) => rescindInvitation(rfq.id!, v.vendorId, v.code, v.note),
    onSuccess: () => { inval(); setRescindVendor(null) },
  })
  const invite = useMutation({
    mutationFn: (vendorId: string) => inviteVendorToRfq(rfq.id!, vendorId),
    onSuccess: () => { setAddErr(null); inval() },
    onError: (e: Error) => setAddErr(e.message),
  })
  const extend = useMutation({
    mutationFn: (v: { iso: string; code: string; note: string | null }) => extendRfq(rfq.id!, v.iso, v.code, v.note),
    onSuccess: () => { inval(); setShowExtend(false) },
  })
  const g2Blocked = addErr !== null && /extend the deadline/i.test(addErr)

  let action: ReactNode = null
  if (rfq.status === 'Closed')
    action = <button type="button" className="btn btn-pri" onClick={() => onNavigate(`openings/${rfq.id}`)}><Icon name="unlock" size={15} /> Open bids</button>
  else if (rfq.status === 'Evaluation')
    action = <button type="button" className="btn btn-pri" onClick={() => onNavigate(`awards/${rfq.id}`)}><Icon name="award" size={15} /> Proceed to award</button>
  else if (rfq.status === 'Awarded')
    action = <button type="button" className="btn btn-out" onClick={() => onNavigate(`awards/${rfq.id}`)}><Icon name="award" size={15} /> View award</button>

  return (
    <>
      <div className="crumb">
        <a onClick={onBack}>RFQs</a> <Icon name="chev" size={13} /> <span>{rfq.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>{rfq.title || rfq.code}</h1>
          <p>{rfq.code} · from {(rfq.prRefs ?? []).join(', ') || '—'}</p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          {rfq.status === 'Open' && <button type="button" className="btn btn-out" onClick={() => setShowExtend(true)}><Icon name="clock" size={15} /> Extend deadline</button>}
          {rfq.status === 'Open' && <button type="button" className="btn btn-out" onClick={() => setConfirm('close')}><Icon name="clock" size={15} /> Close bids</button>}
          {rfq.status === 'Open' && <button type="button" className="btn btn-ghost" style={{ color: 'var(--red)' }} onClick={() => setConfirm('cancel')}>Cancel</button>}
          {action}
        </div>
      </div>

      {confirm === 'close' && (
        <ConfirmModal
          icon="clock" title={`Close bids for ${rfq.code}?`}
          body={`This closes the bid window now (ahead of ${rfq.closesUtc ? fmtDay(rfq.closesUtc) : 'the deadline'}). The RFQ moves to ready to open.`}
          cancelLabel="Not yet" confirmLabel="Close bids now" confirmIcon="clock" busy={close.isPending}
          onCancel={() => setConfirm(null)} onConfirm={() => close.mutate()}
        />
      )}
      {confirm === 'cancel' && (
        <ConfirmModal
          icon="x" title={`Cancel ${rfq.code}?`}
          body="This cancels the RFQ. Invited vendors can no longer bid and it won't proceed to evaluation. This cannot be undone."
          cancelLabel="Keep RFQ" confirmLabel="Cancel RFQ" danger busy={cancel.isPending}
          onCancel={() => setConfirm(null)} onConfirm={() => cancel.mutate()}
        />
      )}
      {rescindVendor && (
        <ReasonModal
          title="Rescind invitation" icon="x" listCode="RFQ_RESCIND_REASON" confirmLabel="Rescind" confirmIcon="x"
          busy={rescind.isPending} error={rescind.error ? (rescind.error as Error).message : null}
          onCancel={() => { setRescindVendor(null); rescind.reset() }}
          onSubmit={(code, note) => rescind.mutate({ vendorId: rescindVendor, code, note })}
        />
      )}
      {showAdd && (
        <AddVendorModal
          invitedStatusByVendor={invitedStatus} busy={invite.isPending} error={addErr} extendHint={g2Blocked}
          onCancel={() => { setShowAdd(false); setAddErr(null) }}
          onPick={(vendorId) => invite.mutate(vendorId)}
          onExtend={() => { setShowAdd(false); setAddErr(null); setShowExtend(true) }}
        />
      )}
      {showExtend && (
        <ExtendModal
          currentClosesUtc={rfq.closesUtc} originalClosesUtc={rfq.originalClosesUtc} extensionCount={rfq.extensionCount ?? 0} maxExtensions={rfq.maxExtensions}
          busy={extend.isPending} error={extend.error ? (extend.error as Error).message : null}
          onCancel={() => { setShowExtend(false); extend.reset() }}
          onSubmit={(iso, code, note) => extend.mutate({ iso, code, note })}
        />
      )}

      <div className="grid g4" style={{ marginBottom: 18 }}>
        <div className="card stat"><div className="lbl">Status</div><div style={{ marginTop: 8 }}><RfqStatusBadge status={rfq.status} /></div></div>
        <div className="card stat"><div className="lbl">Envelope</div><div className="num" style={{ fontSize: 18, marginTop: 8 }}>{dual ? 'Dual (sealed)' : 'Single'}</div></div>
        <div className="card stat"><div className="lbl">Bids received</div><div className="num">{received}/{live.length}</div><div className="sub">of invited vendors</div></div>
        <div className="card stat"><div className="lbl">Closes</div><div className="num" style={{ fontSize: 18, marginTop: 8 }}>{fmtDay(rfq.closesUtc)}</div>{(rfq.extensionCount ?? 0) > 0 && <div className="sub">extended {rfq.extensionCount}×</div>}</div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Invited Vendors</h3>
          <div className="spacer" />
          {dual && rfq.status !== 'Evaluation' && rfq.status !== 'Awarded' && <span className="badge b-blue" style={{ marginRight: 8 }}>Bids sealed until opened</span>}
          {canGovern && <button type="button" className="btn btn-out btn-sm" onClick={() => { setShowAdd(true); setAddErr(null) }}><Icon name="plus" size={14} /> Add vendor</button>}
        </div>
        <table>
          <thead><tr><th>Vendor</th><th>Category</th><th>Status</th><th>Reason</th><th /></tr></thead>
          <tbody>
            {invitations.map((inv) => {
              const rescinded = inv.status === 'Rescinded'
              const reason = rescinded ? inv.rescindReasonCode : inv.status === 'Declined' ? inv.declineReasonCode : null
              const note = rescinded ? inv.rescindNote : inv.declineNote
              return (
                <tr key={inv.vendorId} style={rescinded ? { opacity: 0.55 } : undefined}>
                  <td style={{ fontWeight: 600, textDecoration: rescinded ? 'line-through' : undefined }}>{inv.vendorName}</td>
                  <td>{categoryOf.get(inv.vendorId ?? '') ?? '—'}</td>
                  <td><InvitationStatusBadge status={inv.status} /></td>
                  <td>{reason ? <span title={note ?? undefined}>{label(reason)}{note ? ' ⓘ' : ''}</span> : <span className="hint">—</span>}</td>
                  <td className="amt">
                    {canGovern && RESCINDABLE.has(inv.status ?? '') && (
                      <button type="button" className="btn btn-ghost btn-sm" onClick={() => { rescind.reset(); setRescindVendor(inv.vendorId ?? null) }}>Rescind</button>
                    )}
                  </td>
                </tr>
              )
            })}
            {invitations.length === 0 && <tr><td colSpan={5}><EmptyState>No vendors invited.</EmptyState></td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="chead"><h3>Activity</h3></div>
        <div className="cbody"><ActivityTimeline events={rfq.events ?? []} /></div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="chead"><h3>Line Items</h3></div>
        <table>
          <thead><tr><th>Code</th><th>Item</th><th className="amt">Qty</th><th>UoM</th></tr></thead>
          <tbody>
            {(rfq.lines ?? []).map((l, i) => (
              <tr key={i}><td>{l.itemCode}</td><td>{l.description}</td><td className="amt">{fmt(l.qty)}</td><td>{l.uom}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <CustomFieldsSection recordType="Rfq" recordId={rfq.id ?? ''} />
      <SegmentsSection recordType="Rfq" recordId={rfq.id ?? ''} />
    </>
  )
}

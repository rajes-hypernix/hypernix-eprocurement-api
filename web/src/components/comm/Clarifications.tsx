import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getClarificationThreads, getClarificationThread, sendClarification,
  getVendors, getRfqs, getMyInvitations,
} from '../../api/client'
import { Icon } from '../Icon'
import { MessageInput } from './MessageInput'
import { useIdentity } from '../../identity'

const initials = (name: string) =>
  name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]?.toUpperCase()).join('') || '?'

type Sel = { scope: string; vendorId: string } | null

export function Clarifications() {
  const qc = useQueryClient()
  const { isVendor, persona } = useIdentity()
  const myVendorId = persona?.vendorId ?? undefined

  const { data: threads = [] } = useQuery({ queryKey: ['clarifications'], queryFn: getClarificationThreads })
  const [sel, setSel] = useState<Sel>(null)
  const [text, setText] = useState('')
  const [broadcast, setBroadcast] = useState(false)
  const [showNew, setShowNew] = useState(false)

  // Default selection = first thread.
  const active = sel ?? (threads[0] ? { scope: threads[0].scope!, vendorId: threads[0].vendorId! } : null)

  const { data: detail } = useQuery({
    queryKey: ['clarification', active?.scope, active?.vendorId],
    queryFn: () => getClarificationThread(active!.scope, active!.vendorId).catch(() => null),
    enabled: !!active,
  })
  // Opening a thread marks it read server-side; refresh the unread badges.
  const onOpen = (t: { scope: string; vendorId: string }) => {
    setSel(t); setBroadcast(false)
    void qc.invalidateQueries({ queryKey: ['clarifications'] })
  }

  const send = useMutation({
    mutationFn: () => sendClarification({ scope: active!.scope, vendorId: active!.vendorId, body: text.trim(), published: broadcast }),
    onSuccess: () => {
      setText('')
      void qc.invalidateQueries({ queryKey: ['clarification', active?.scope, active?.vendorId] })
      void qc.invalidateQueries({ queryKey: ['clarifications'] })
    },
  })

  const isRfq = active && active.scope !== 'general'

  return (
    <>
      <div className="pagehead">
        <div><h1>Clarifications</h1><p>General and RFQ-specific conversations with {isVendor ? 'procurement' : 'vendors'}.</p></div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" onClick={() => setShowNew(true)}><Icon name="plus" size={15} /> New clarification</button>
      </div>

      <div className="cp-wrap">
        <div className="cp-list">
          {threads.length === 0 && <div className="chat-empty" style={{ padding: 30 }}>No threads yet.<br />Start a new clarification.</div>}
          {threads.map((t) => {
            const on = active?.scope === t.scope && active?.vendorId === t.vendorId
            const name = isVendor ? 'Procurement' : (t.vendorName ?? 'Vendor')
            return (
              <div key={`${t.scope}:${t.vendorId}`} className={`cp-row${on ? ' on' : ''}`} onClick={() => onOpen({ scope: t.scope!, vendorId: t.vendorId! })}>
                <span className="cav">{initials(name)}</span>
                <div className="crmeta">
                  <div className="crname">{name}</div>
                  <div className="crsub">{t.general ? <><Icon name="msg" size={10} /> General inquiry</> : t.scope}</div>
                  <div className="crlast">{t.lastMessage}</div>
                </div>
                {(t.unread ?? 0) > 0 && <span className="chat-badge">{t.unread}</span>}
              </div>
            )
          })}
        </div>

        <div className="cp-main">
          {!active ? (
            <div className="cp-empty"><Icon name="msg" size={34} /><div>Select a conversation or start a new one.</div></div>
          ) : (
            <div className="cp-conv">
              <div className="cp-hd">
                <span className="cav">{initials(isVendor ? 'Procurement' : (detail?.vendorName ?? 'Vendor'))}</span>
                <div>
                  <div className="chname" style={{ fontSize: 15 }}>{isVendor ? 'Procurement' : (detail?.vendorName ?? 'Vendor')}</div>
                  <div className="chsub" style={{ color: 'var(--muted)' }}>{active.scope === 'general' ? 'General inquiry' : (detail?.scopeLabel ?? active.scope)}{isRfq ? ` · ${active.scope}` : ''}</div>
                </div>
              </div>
              <div className="cp-body">
                {(detail?.messages ?? []).length === 0 && <div className="chat-empty" style={{ margin: 'auto' }}>No messages yet.</div>}
                {(detail?.messages ?? []).map((m) => (
                  <div key={m.id} className={`cmsg ${m.mine ? 'me' : 'them'}`}>
                    {m.published && <div className="cpub"><Icon name="users" size={10} /> Published to all bidders</div>}
                    {m.body}
                    <div className="ct">{(m.createdUtc ?? '').slice(0, 16).replace('T', ' ')}</div>
                  </div>
                ))}
              </div>
              <div className="cp-ft">
                {!isVendor && isRfq && (
                  <label className="cbcast"><input type="checkbox" checked={broadcast} onChange={(e) => setBroadcast(e.target.checked)} /> <Icon name="users" size={13} /> Share answer with all bidders (anonymised)</label>
                )}
                <MessageInput value={text} onChange={setText} onSend={() => text.trim() && send.mutate()} busy={send.isPending} />
              </div>
            </div>
          )}
        </div>
      </div>

      {showNew && (
        <NewClarification
          isVendor={isVendor} myVendorId={myVendorId}
          onClose={() => setShowNew(false)}
          onStart={(scope, vendorId) => { setShowNew(false); onOpen({ scope, vendorId }) }}
        />
      )}
    </>
  )
}

function NewClarification({ isVendor, myVendorId, onClose, onStart }: {
  isVendor: boolean; myVendorId?: string; onClose: () => void; onStart: (scope: string, vendorId: string) => void
}) {
  const { data: vendors = [] } = useQuery({ queryKey: ['vendors', '', 'all', 'all'], queryFn: () => getVendors(), enabled: !isVendor })
  // A vendor scopes only to THEIR invited RFQs (getMyInvitations), never the full RFQ list — the
  // buyer picks from all sourcing RFQs. (getRfqs is not vendor-scoped server-side; see PERMISSIONS-REGISTER.)
  const { data: allRfqs = [] } = useQuery({ queryKey: ['rfqs'], queryFn: getRfqs, enabled: !isVendor })
  const { data: myInvitations = [] } = useQuery({ queryKey: ['my-invitations'], queryFn: getMyInvitations, enabled: isVendor })
  const scopeRfqs = (isVendor
    ? myInvitations.map((i) => ({ code: i.code, title: i.title, status: i.status }))
    : allRfqs.map((r) => ({ code: r.code, title: r.title, status: r.status })))
    .filter((r) => ['Open', 'Closed', 'Evaluation'].includes(r.status ?? ''))
  const [vendorId, setVendorId] = useState('')
  const [scope, setScope] = useState('general')

  const effectiveVendor = isVendor ? (myVendorId ?? '') : (vendorId || vendors[0]?.id || '')

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal">
        <div className="mhead"><Icon name="msg" size={20} /><h3>New Clarification</h3></div>
        <div className="mbody">
          <p className="hint" style={{ marginTop: 0 }}>Start a general inquiry, or pick an RFQ to open a thread specific to that tender.</p>
          {!isVendor && (
            <div className="field"><label>Vendor</label>
              <select value={vendorId} onChange={(e) => setVendorId(e.target.value)}>
                {vendors.map((v) => <option key={v.id} value={v.id!}>{v.name}</option>)}
              </select>
            </div>
          )}
          <div className="field"><label>Topic</label>
            <select value={scope} onChange={(e) => setScope(e.target.value)}>
              <option value="general">General inquiry (not tied to an RFQ)</option>
              {scopeRfqs.length > 0 && (
                <optgroup label="Specific RFQ">
                  {scopeRfqs.map((r) => <option key={r.code} value={r.code!}>{r.code} — {r.title}</option>)}
                </optgroup>
              )}
            </select>
          </div>
        </div>
        <div className="mfoot">
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={!effectiveVendor} onClick={() => onStart(scope, effectiveVendor)}><Icon name="msg" size={15} /> Start conversation</button>
        </div>
      </div>
    </div>
  )
}

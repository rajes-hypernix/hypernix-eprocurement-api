import { useEffect, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getClarificationThreads, getClarificationThread, sendClarification,
  getMyInvitations, getRfqs, getVendors,
} from '../../api/client'
import { Icon } from '../Icon'
import { useIdentity } from '../../identity'

const initials = (name: string) =>
  name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]?.toUpperCase()).join('') || '?'

type Active = { scope: string; vendorId: string }
const RFQ_ROUTES = ['bid', 'rfqs', 'awards', 'openings', 'eval']
const POS_KEY = 'chatdock.pos'

// Position is stored as distance from the viewport's bottom-right corner, so the
// launcher stays put when the panel expands/collapses (the panel opens up/down,
// out of flow) — matching how Messenger/Intercom-style docks behave.
function loadPos(): { right: number; bottom: number } | null {
  try {
    const v = localStorage.getItem(POS_KEY); if (!v) return null
    const p = JSON.parse(v)
    return typeof p?.right === 'number' && typeof p?.bottom === 'number' ? p : null
  } catch { return null }
}

// Floating, draggable clarifications dock shown on every page (mirrors the prototype's
// renderChat dock). Compose + reply happen inside the dock — it never navigates away.
export function ChatDock({ route }: { route: string; onNavigate?: (key: string) => void }) {
  const qc = useQueryClient()
  const { isVendor, persona, personas } = useIdentity()
  const myVendorId = persona?.vendorId ?? ''

  const [open, setOpen] = useState(false)
  const [view, setView] = useState<'list' | 'thread' | 'compose'>('list')
  const [active, setActive] = useState<Active | null>(null)
  const [text, setText] = useState('')           // reply draft (kept while dock is mounted)
  // Compose draft — preserved across open/close so a half-typed message is never lost.
  const [cScope, setCScope] = useState('')
  const [cVendor, setCVendor] = useState('')      // buyer-side: which vendor to message
  const [cBuyer, setCBuyer] = useState('')        // vendor-side: which buyer to address
  const [cText, setCText] = useState('')

  // ---- data ----
  const { data: threads = [] } = useQuery({ queryKey: ['clarifications'], queryFn: getClarificationThreads, refetchInterval: 15000 })
  // Fetched eagerly (not gated on `open`) so the current-RFQ context is ready the
  // instant the launcher is clicked — otherwise the prefill misses on a cold cache.
  const { data: invitations = [] } = useQuery({ queryKey: ['my-invitations'], queryFn: getMyInvitations, enabled: isVendor })
  const { data: rfqs = [] } = useQuery({ queryKey: ['rfqs'], queryFn: getRfqs, enabled: !isVendor })
  const { data: vendors = [] } = useQuery({ queryKey: ['vendors', '', 'all', 'all'], queryFn: () => getVendors(), enabled: open && !isVendor })
  const buyers = personas.filter((p) => p.kind === 'internal' && (p.roles ?? []).includes('Buyer'))

  const { data: detail } = useQuery({
    queryKey: ['clarification', active?.scope, active?.vendorId],
    queryFn: () => getClarificationThread(active!.scope, active!.vendorId).catch(() => null),
    enabled: !!active,
  })

  // ---- current-RFQ context (for prefill) ----
  const base = route.split('/')[0]
  const routeRfqId = RFQ_ROUTES.includes(base) ? route.split('/')[1] : undefined
  const ctxInv = routeRfqId ? invitations.find((i) => i.rfqId === routeRfqId) : undefined
  const ctxRfq = routeRfqId ? rfqs.find((r) => r.id === routeRfqId) : undefined
  const ctxCode = ctxInv?.code ?? ctxRfq?.code ?? ''
  const ctxOwner = ctxInv?.ownerUserId ?? ''

  // ---- drag (anchored bottom-right) ----
  const ref = useRef<HTMLDivElement>(null)
  const [pos, setPos] = useState<{ right: number; bottom: number } | null>(loadPos())
  const posRef = useRef(pos)
  const drag = useRef<{ sx: number; sy: number; or: number; ob: number } | null>(null)
  const movedRef = useRef(false)
  useEffect(() => { posRef.current = pos }, [pos])

  // Re-clamp into view on resize/zoom so the launcher never strands off-screen.
  const [, force] = useState(0)
  useEffect(() => {
    const onResize = () => {
      force((n) => n + 1)
      if (posRef.current) setPos((p) => p && ({
        right: Math.min(Math.max(8, p.right), window.innerWidth - 356),
        bottom: Math.min(Math.max(8, p.bottom), window.innerHeight - 60),
      }))
    }
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  const onMove = (e: PointerEvent) => {
    const d = drag.current; if (!d) return
    const dx = e.clientX - d.sx, dy = e.clientY - d.sy
    if (Math.abs(dx) + Math.abs(dy) > 4) movedRef.current = true
    const right = Math.min(Math.max(8, d.or - dx), window.innerWidth - 356)
    const bottom = Math.min(Math.max(8, d.ob - dy), window.innerHeight - 60)
    setPos({ right, bottom })
  }
  const onUp = () => {
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
    drag.current = null
    if (posRef.current) localStorage.setItem(POS_KEY, JSON.stringify(posRef.current))
  }
  const startDrag = (e: React.PointerEvent) => {
    const el = ref.current; if (!el) return
    const r = el.getBoundingClientRect()
    drag.current = { sx: e.clientX, sy: e.clientY, or: window.innerWidth - r.right, ob: window.innerHeight - r.bottom }
    movedRef.current = false
    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
  }

  // ---- actions ----
  const openThread = (t: Active) => {
    setActive(t); setView('thread')
    void qc.invalidateQueries({ queryKey: ['clarifications'] })
  }
  const openCompose = (prefillScope?: string, prefillBuyer?: string) => {
    if (!cScope) setCScope(prefillScope ?? 'general')
    if (isVendor && !cBuyer && prefillBuyer) setCBuyer(prefillBuyer)
    setView('compose')
  }
  const onLauncher = () => {
    if (movedRef.current) { movedRef.current = false; return }   // was a drag, not a click
    if (open) { setOpen(false); return }
    setOpen(true)
    // Messenger-style: on an RFQ, jump straight to its thread or a prefilled compose.
    if (view === 'list' && !active && ctxCode) {
      const t = threads.find((th) => th.scope === ctxCode && (!isVendor || th.vendorId === myVendorId))
      if (t) openThread({ scope: t.scope!, vendorId: t.vendorId! })
      else openCompose(ctxCode, ctxOwner)
    }
  }

  const send = useMutation({
    mutationFn: () => sendClarification({ scope: active!.scope, vendorId: active!.vendorId, body: text.trim(), published: false }),
    onSuccess: () => {
      setText('')
      void qc.invalidateQueries({ queryKey: ['clarification', active?.scope, active?.vendorId] })
      void qc.invalidateQueries({ queryKey: ['clarifications'] })
    },
  })

  const startConversation = useMutation({
    mutationFn: () => {
      const vendorId = isVendor ? myVendorId : cVendor
      return sendClarification({ scope: cScope || 'general', vendorId, body: cText.trim(), published: false, recipientUserId: isVendor ? (cBuyer || undefined) : undefined })
    },
    onSuccess: (_d, _v, _c) => {
      const vendorId = isVendor ? myVendorId : cVendor
      const scope = cScope || 'general'
      setCText('')
      void qc.invalidateQueries({ queryKey: ['clarifications'] })
      openThread({ scope, vendorId })
    },
  })

  if (base === 'chats') return null   // the full Clarifications page is open; hide the dock

  const composeVendorId = isVendor ? myVendorId : cVendor
  const composeRfqs = isVendor
    ? invitations.map((i) => ({ code: i.code!, title: i.title! }))
    : rfqs.filter((r) => ['Open', 'Closed', 'Evaluation'].includes(r.status ?? '')).map((r) => ({ code: r.code!, title: r.title! }))
  const totalUnread = threads.reduce((n, t) => n + (t.unread ?? 0), 0)
  const headName = isVendor ? 'Procurement' : (detail?.vendorName ?? 'Vendor')
  // Open upward when the launcher sits in the lower half, downward when near the top.
  const openUp = (pos?.bottom ?? 22) < window.innerHeight / 2

  return (
    <div className="chatdock" ref={ref} style={pos ? { right: pos.right, bottom: pos.bottom, left: 'auto', top: 'auto' } : undefined}>
      {open && (
        <div className={`cd-panel ${openUp ? 'cd-up' : 'cd-down'}`}>
          <div className="cd-bar" onPointerDown={startDrag}>
            {view !== 'list' && <button type="button" className="cd-ic" onClick={() => { setActive(null); setView('list') }} title="Back"><Icon name="chev" size={15} /></button>}
            <span className="cd-title">
              {view === 'thread' ? headName : view === 'compose' ? 'New clarification' : 'Clarifications'}
            </span>
            <span className="cd-grip" title="Drag to move"><Icon name="grip" size={14} /></span>
            <button type="button" className="cd-ic" onClick={() => setOpen(false)} title="Close"><Icon name="x" size={15} /></button>
          </div>

          {view === 'list' && (
            <>
              <div className="cd-body list">
                {threads.length === 0 && <div className="chat-empty" style={{ padding: 24 }}>No threads yet.<br />Start a new clarification.</div>}
                {threads.map((t) => {
                  const name = isVendor ? 'Procurement' : (t.vendorName ?? 'Vendor')
                  return (
                    <div key={`${t.scope}:${t.vendorId}`} className="cd-row" onClick={() => openThread({ scope: t.scope!, vendorId: t.vendorId! })}>
                      <span className="cav">{initials(name)}</span>
                      <div className="crmeta">
                        <div className="crname">{name}</div>
                        <div className="crsub">{t.general ? 'General inquiry' : t.scope}</div>
                        <div className="crlast">{t.lastMessage}</div>
                      </div>
                      {(t.unread ?? 0) > 0 && <span className="chat-badge">{t.unread}</span>}
                    </div>
                  )
                })}
              </div>
              <div className="cd-ft">
                <button type="button" className="btn btn-pri" style={{ width: '100%' }} onClick={() => openCompose(ctxCode, ctxOwner)}><Icon name="plus" size={14} /> New clarification</button>
              </div>
            </>
          )}

          {view === 'thread' && active && (
            <>
              <div className="cd-sub">{active.scope === 'general' ? 'General inquiry' : (detail?.scopeLabel ?? active.scope)}{active.scope !== 'general' ? ` · ${active.scope}` : ''}</div>
              <div className="cd-body">
                {(detail?.messages ?? []).length === 0 && <div className="chat-empty" style={{ margin: 'auto' }}>No messages yet.</div>}
                {(detail?.messages ?? []).map((m) => (
                  <div key={m.id} className={`cmsg ${m.mine ? 'me' : 'them'}`}>
                    {m.published && <div className="cpub"><Icon name="users" size={10} /> Published to all bidders</div>}
                    {!m.mine && m.recipientName && <div className="crcpt">to {m.recipientName}</div>}
                    {m.body}
                    <div className="ct">{(m.createdUtc ?? '').slice(0, 16).replace('T', ' ')}{m.mine && m.recipientName ? ` · to ${m.recipientName}` : ''}</div>
                  </div>
                ))}
              </div>
              <div className="cd-ft cinrow">
                <input type="text" value={text} placeholder="Type a message…" onChange={(e) => setText(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter' && text.trim()) { e.preventDefault(); send.mutate() } }} />
                <button type="button" className="cibtn" disabled={!text.trim() || send.isPending} onClick={() => send.mutate()}><Icon name="send" size={15} /></button>
              </div>
            </>
          )}

          {view === 'compose' && (
            <>
              <div className="cd-body compose">
                <div className="field"><label>Topic</label>
                  <select value={cScope} onChange={(e) => setCScope(e.target.value)}>
                    <option value="general">General inquiry (not tied to an RFQ)</option>
                    {composeRfqs.length > 0 && (
                      <optgroup label="Specific RFQ">
                        {composeRfqs.map((r) => <option key={r.code} value={r.code}>{r.code} — {r.title}</option>)}
                      </optgroup>
                    )}
                  </select>
                </div>
                {isVendor ? (
                  <div className="field"><label>To (Buyer in charge)</label>
                    <select value={cBuyer} onChange={(e) => setCBuyer(e.target.value)}>
                      <option value="">Procurement team</option>
                      {buyers.map((b) => <option key={b.code} value={b.code!}>{b.name}{b.code === ctxOwner ? ' — in charge of this RFQ' : ''}</option>)}
                    </select>
                  </div>
                ) : (
                  <div className="field"><label>Vendor</label>
                    <select value={cVendor} onChange={(e) => setCVendor(e.target.value)}>
                      <option value="">Select a vendor…</option>
                      {vendors.map((v) => <option key={v.id} value={v.id!}>{v.name}</option>)}
                    </select>
                  </div>
                )}
                <div className="field"><label>Message</label>
                  <textarea rows={4} value={cText} placeholder="Type your clarification…" onChange={(e) => setCText(e.target.value)} />
                </div>
              </div>
              <div className="cd-ft">
                <button type="button" className="btn btn-pri" style={{ width: '100%' }}
                  disabled={!cText.trim() || !composeVendorId || startConversation.isPending}
                  onClick={() => startConversation.mutate()}>
                  <Icon name="send" size={14} /> Send
                </button>
              </div>
            </>
          )}
        </div>
      )}

      <button type="button" className="chat-launcher" onPointerDown={startDrag} onClick={onLauncher}>
        <Icon name="msg" size={18} /> <span>Clarifications</span>
        {totalUnread > 0 && <span className="chat-badge launch">{totalUnread}</span>}
      </button>
    </div>
  )
}

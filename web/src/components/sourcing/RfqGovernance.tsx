import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getCustomLists, getVendors, type RfqEventDto, type VendorListItem } from '../../api/client'
import { useSwec } from '../../api/swec'
import { Icon } from '../Icon'
import { Modal, Notice } from '../ui'
import { fmtDay } from '../../lib/format'

// ---- Invitation status pill (reuses the app's .badge b-* tones) ----
const INV_TONE: Record<string, [string, string]> = {
  Invited: ['b-grey', 'Invited'],
  Viewed: ['b-grey', 'Viewed'],
  IntendToBid: ['b-teal', 'Intends to bid'],
  Declined: ['b-amber', 'Declined'],
  BidSubmitted: ['b-green', 'Bid submitted'],
  Rescinded: ['b-grey', 'Rescinded'],
}
export function InvitationStatusBadge({ status }: { status?: string | null }) {
  const [tone, label] = INV_TONE[status ?? ''] ?? ['b-grey', status ?? '—']
  return <span className={`badge ${tone}`}>{label}</span>
}

// Reason-code + note picker from a seeded SYSTEM Custom List (RFQ_DECLINE/RESCIND/EXTENSION_REASON).
function useReasonList(listCode: string) {
  const { data: lists = [] } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })
  return useMemo(() => (lists.find((l) => l.code === listCode)?.values ?? []).filter((v) => v.active), [lists, listCode])
}

/** Resolve a reason CODE to its human LABEL across the RFQ reason lists (for display). */
export function useReasonLabeler() {
  const { data: lists = [] } = useQuery({ queryKey: ['custom-lists'], queryFn: getCustomLists })
  return useMemo(() => {
    const map = new Map<string, string>()
    for (const l of lists) if (l.code.startsWith('RFQ_')) for (const v of l.values) map.set(v.code, v.label)
    return (code?: string | null) => (code ? map.get(code) ?? code : '')
  }, [lists])
}

// Datetime helpers: <input type=datetime-local> works in LOCAL time; the API wants UTC ISO.
const toLocalInput = (iso: string | null | undefined): string => {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
const localInputToIso = (local: string): string => new Date(local).toISOString()

/**
 * Shared reason modal (decline / rescind). Reason code is required (mirrors the domain G4 guard);
 * the note is optional colour. Surfaces the API's 409 rule message inline.
 */
export function ReasonModal({
  title, icon, listCode, confirmLabel, confirmIcon, busy, error, onCancel, onSubmit,
}: {
  title: string; icon?: string; listCode: string; confirmLabel: string; confirmIcon?: string
  busy?: boolean; error?: string | null; onCancel: () => void; onSubmit: (reasonCode: string, note: string | null) => void
}) {
  const reasons = useReasonList(listCode)
  const [code, setCode] = useState('')
  const [note, setNote] = useState('')
  const submit = () => { if (code) onSubmit(code, note.trim() || null) }

  return (
    <Modal title={title} icon={icon ?? 'flag'}
      footer={<>
        <button type="button" className="btn btn-out" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="button" className="btn btn-pri" onClick={submit} disabled={busy || !code}>
          {confirmIcon && <Icon name={confirmIcon} size={15} />} {confirmLabel}
        </button>
      </>}>
      {error && <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>{error}</Notice>}
      <div className="field">
        <label>Reason <span className="req">*</span></label>
        <select value={code} aria-label="Reason code" onChange={(e) => setCode(e.target.value)}>
          <option value="">Select a reason…</option>
          {reasons.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
        </select>
      </div>
      <div className="field" style={{ marginBottom: 0 }}>
        <label>Note (optional)</label>
        <textarea rows={2} value={note} aria-label="Reason note" placeholder="Add context…" onChange={(e) => setNote(e.target.value)} />
      </div>
    </Modal>
  )
}

/**
 * Extend-deadline modal. New close must be after the current one (min set on the picker); shows
 * "Extension n of max · Original close …"; disables submit at the cap; still handles the 409.
 */
export function ExtendModal({
  currentClosesUtc, originalClosesUtc, extensionCount, maxExtensions, busy, error, onCancel, onSubmit,
}: {
  currentClosesUtc?: string | null; originalClosesUtc?: string | null; extensionCount: number
  // The server's extension cap (RfqDetail.maxExtensions). When absent, the client shows no cap and
  // leaves enforcement to the server — it never invents a limit of its own.
  maxExtensions?: number
  busy?: boolean; error?: string | null; onCancel: () => void; onSubmit: (newClosesIso: string, reasonCode: string, note: string | null) => void
}) {
  const reasons = useReasonList('RFQ_EXTENSION_REASON')
  const [when, setWhen] = useState('')
  const [code, setCode] = useState('')
  const [note, setNote] = useState('')
  const atCap = maxExtensions != null && extensionCount >= maxExtensions
  const minLocal = toLocalInput(currentClosesUtc)
  const valid = !!when && !!code && (!minLocal || when > minLocal)
  const submit = () => { if (valid) onSubmit(localInputToIso(when), code, note.trim() || null) }

  return (
    <Modal title="Extend deadline" icon="clock"
      footer={<>
        <button type="button" className="btn btn-out" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="button" className="btn btn-pri" onClick={submit} disabled={busy || atCap || !valid}>
          <Icon name="clock" size={15} /> Extend
        </button>
      </>}>
      {error && <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>{error}</Notice>}
      <p className="hint" style={{ marginTop: 0 }}>
        {maxExtensions != null
          ? `Extension ${Math.min(extensionCount + 1, maxExtensions)} of ${maxExtensions}`
          : `Extension ${extensionCount + 1}`} · Original close {fmtDay(originalClosesUtc)}
      </p>
      {atCap && <Notice tone="warn" icon="flag" style={{ marginBottom: 12 }}>This RFQ has reached the maximum of {maxExtensions} extension(s).</Notice>}
      <div className="field">
        <label>New close date &amp; time <span className="req">*</span></label>
        <input type="datetime-local" value={when} min={minLocal || undefined} aria-label="New close date and time"
          disabled={atCap} onChange={(e) => setWhen(e.target.value)} />
        {when && minLocal && when <= minLocal && <span className="hint" style={{ color: 'var(--red)' }}>Must be later than the current close.</span>}
      </div>
      <div className="field">
        <label>Reason <span className="req">*</span></label>
        <select value={code} aria-label="Extension reason" disabled={atCap} onChange={(e) => setCode(e.target.value)}>
          <option value="">Select a reason…</option>
          {reasons.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
        </select>
      </div>
      <div className="field" style={{ marginBottom: 0 }}>
        <label>Note (optional)</label>
        <textarea rows={2} value={note} aria-label="Extension note" disabled={atCap} onChange={(e) => setNote(e.target.value)} />
      </div>
    </Modal>
  )
}

/**
 * Add-vendor picker modal. Deliberately duplicates the RFQ Builder picker's BEHAVIOUR (search, type +
 * region filters, SWEC category match) rather than extracting a shared component mid-slice from a
 * protected flow. Already-invited vendors appear disabled with their current status pill.
 */
export function AddVendorModal({
  invitedStatusByVendor, busy, error, extendHint, onCancel, onPick, onExtend,
}: {
  invitedStatusByVendor: Map<string, string>   // vendorId → invitation status (excludes Rescinded = re-invitable)
  busy?: boolean; error?: string | null; extendHint?: boolean
  onCancel: () => void; onPick: (vendorId: string) => void; onExtend: () => void
}) {
  const { data: vendors = [] } = useQuery({ queryKey: ['vendors'], queryFn: () => getVendors() })
  const { data: swec } = useSwec()
  const [q, setQ] = useState('')
  const [type, setType] = useState('all')
  const [region, setRegion] = useState('all')

  const regions = useMemo(() => [...new Set(vendors.map((v) => v.region).filter(Boolean) as string[])].sort(), [vendors])
  const shown = vendors.filter((v: VendorListItem) => {
    const kw = q.trim().toLowerCase()
    const matchKw = !kw || [v.name, v.code, ...(v.categories ?? []).map((c) => swec?.label(c) ?? c)]
      .some((s) => (s ?? '').toLowerCase().includes(kw))
    return matchKw && (type === 'all' || v.type === type) && (region === 'all' || v.region === region)
  })

  return (
    <Modal title="Add vendor" icon="vendor"
      footer={<button type="button" className="btn btn-out" onClick={onCancel} disabled={busy}>Done</button>}>
      {error && (
        <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>
          {error}
          {extendHint && <button type="button" className="btn btn-out btn-sm" style={{ marginLeft: 10 }} onClick={onExtend}><Icon name="clock" size={13} /> Extend deadline</button>}
        </Notice>
      )}
      <div className="filterbar2" style={{ marginBottom: 10 }}>
        <div className="msel" style={{ flex: 1, minWidth: 180 }}>
          <label>Search</label>
          <input type="text" value={q} placeholder="name, code or category…" aria-label="Search vendors" onChange={(e) => setQ(e.target.value)} />
        </div>
        <div className="msel"><label>Type</label>
          <select value={type} onChange={(e) => setType(e.target.value)}><option value="all">All</option><option value="SWEC">SWEC</option><option value="Non-SWEC">Non-SWEC</option></select>
        </div>
        <div className="msel"><label>Region</label>
          <select value={region} onChange={(e) => setRegion(e.target.value)}><option value="all">All</option>{regions.map((r) => <option key={r} value={r}>{r}</option>)}</select>
        </div>
      </div>
      <div className="swtree" style={{ maxHeight: 320 }}>
        {shown.length === 0 && <div className="hint" style={{ padding: 10 }}>No vendors match.</div>}
        {shown.map((v) => {
          const status = invitedStatusByVendor.get(v.id!)
          const invited = !!status
          return (
            <div className="pickrow" key={v.id}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 600 }}>{v.name}</div>
                <div className="hint">{v.type} · {v.region || '—'}{(v.categories ?? []).length ? ` · ${(v.categories ?? []).map((c) => swec?.label(c) ?? c).slice(0, 2).join(', ')}` : ''}</div>
              </div>
              {invited
                ? <InvitationStatusBadge status={status} />
                : <button type="button" className="btn btn-out btn-sm" disabled={busy} onClick={() => onPick(v.id!)}><Icon name="plus" size={13} /> Invite</button>}
            </div>
          )
        })}
      </div>
    </Modal>
  )
}

// ---- Activity timeline (events newest-first) ----
const EVENT_LABEL: Record<string, string> = {
  Released: 'RFQ released', Extended: 'Deadline extended', VendorInvited: 'Vendor invited',
  InvitationRescinded: 'Invitation rescinded', VendorDeclined: 'Vendor declined',
  DeclineReversed: 'Decline reversed', BidWithdrawn: 'Bid withdrawn', Closed: 'Bids closed', Cancelled: 'RFQ cancelled',
}
export function ActivityTimeline({ events }: { events: RfqEventDto[] }) {
  if (events.length === 0) return <p className="hint" style={{ margin: 0 }}>No activity yet.</p>
  return (
    <div className="timeline">
      {events.map((e, i) => {
        const actor = e.vendorName || e.actorVendorUserId || e.actorUserId || 'System'
        return (
          <div className="tl-row" key={i}>
            <span className="tl-dot" />
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600, fontSize: 13 }}>
                {EVENT_LABEL[e.eventType ?? ''] ?? e.eventType}
                {e.reasonCode && <span className="badge b-grey" style={{ marginLeft: 8 }}>{e.reasonCode}</span>}
              </div>
              <div className="hint">
                {actor}
                {e.eventType === 'Extended' && e.oldClosesUtc && e.newClosesUtc && <> · {fmtDay(e.oldClosesUtc)} → {fmtDay(e.newClosesUtc)}</>}
                {e.reasonNote && <> · “{e.reasonNote}”</>}
              </div>
            </div>
            <span className="hint" style={{ whiteSpace: 'nowrap' }}>{fmtDay(e.occurredUtc)}</span>
          </div>
        )
      })}
    </div>
  )
}

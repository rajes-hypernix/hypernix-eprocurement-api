import { useEffect, useMemo, useRef, useState } from 'react'
import { searchGlobal, getVendor, getRfq, type SearchHit } from '../api/client'
import { useIdentity } from '../identity'
import { Icon } from './Icon'
import { TextField } from '../ui/TextField'
import { QuickView } from '../ui/QuickView'
import { Gated, type GatedAction } from '../ui/gating'
import { useRecents, recordRecent, type RecentRecord } from '../lib/recents'
import { fmt, fmtDay } from '../lib/format'

/** Record-type presentation shared by search results and recents. */
const TYPE_META: Record<string, { label: string; icon: string }> = {
  Vendor: { label: 'Vendors', icon: 'vendor' },
  Requisition: { label: 'Requisitions', icon: 'doc' },
  Rfq: { label: 'RFQs', icon: 'rfq' },
  PurchaseOrder: { label: 'Purchase Orders', icon: 'box' },
  Invoice: { label: 'Invoices', icon: 'doc' },
  Asn: { label: 'Deliveries', icon: 'send' },
  Statement: { label: 'Statements', icon: 'clip' },
}

/** Route for a hit — vendor principals use the vendor-portal routes. */
const hitRoute = (h: SearchHit, isVendor: boolean): string | null => {
  switch (h.type) {
    case 'Vendor': return isVendor ? null : `vendors/${h.id}`
    case 'Requisition': return 'reqs'          // PR editing is reached from the list (no deep link today)
    case 'Rfq': return isVendor ? `bid/${h.id}` : `rfqs/${h.id}`
    case 'PurchaseOrder': return `pos/${h.id}`
    case 'Invoice': return `invoices/${h.id}`
    default: return null
  }
}

export function GlobalSearch({ onNavigate }: { onNavigate: (hash: string) => void }) {
  const { code: principal, isVendor } = useIdentity()
  const [q, setQ] = useState('')
  const [hits, setHits] = useState<SearchHit[] | null>(null)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const seq = useRef(0)

  // Debounced fetch against /api/search (server-side, perimeter-scoped).
  useEffect(() => {
    const query = q.trim()
    if (query.length < 2) { setHits(null); setOpen(false); return }
    const mine = ++seq.current
    const t = setTimeout(() => {
      searchGlobal(query)
        .then((r) => { if (seq.current === mine) { setHits(r); setOpen(true); setActive(0) } })
        .catch(() => { if (seq.current === mine) { setHits([]); setOpen(true) } })
    }, 250)
    return () => clearTimeout(t)
  }, [q])

  const groups = useMemo(() => {
    const out: { type: string; items: SearchHit[] }[] = []
    for (const h of hits ?? []) {
      const g = out.find((x) => x.type === h.type)
      if (g) g.items.push(h); else out.push({ type: h.type, items: [h] })
    }
    return out
  }, [hits])
  const flat = useMemo(() => groups.flatMap((g) => g.items), [groups])

  const select = (h: SearchHit) => {
    const route = hitRoute(h, isVendor)
    setOpen(false); setQ('')
    if (!route) return
    recordRecent(principal, { type: h.type, code: h.code, hash: route })
    onNavigate(route)
  }

  const quickViewFetcher = (h: SearchHit) =>
    h.type === 'Vendor'
      ? async () => {
          const v = await getVendor(h.id)
          return {
            title: v.registeredName ?? h.code,
            fields: [
              { label: 'Code', value: v.code },
              { label: 'Type', value: v.type },
              { label: 'Status', value: v.status },
              { label: 'Payment terms', value: v.paymentTerms },
              { label: 'Credit limit', value: `RM ${fmt(v.creditLimit)}` },
            ],
          }
        }
      : async () => {
          const r = await getRfq(h.id)
          return {
            title: r.title || r.code || h.code,
            fields: [
              { label: 'Code', value: r.code },
              { label: 'Status', value: r.status },
              { label: 'Envelope', value: r.envelope },
              { label: 'Closes', value: fmtDay(r.closesUtc) },
              { label: 'Invited', value: `${(r.invitations ?? []).length} vendor(s)` },
            ],
          }
        }

  return (
    <div className="gsearch">
      <Icon name="rfq" size={14} />
      <div
        style={{ flex: 1 }}
        onKeyDown={(e) => {
          if (!open || flat.length === 0) return
          if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, flat.length - 1)) }
          else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)) }
          else if (e.key === 'Enter') { e.preventDefault(); select(flat[active]) }
          else if (e.key === 'Escape') setOpen(false)
        }}
      >
        <TextField
          chrome="bare"
          spec={{ key: 'gsearch', label: 'Global search', dataType: 'text', placeholder: 'Search vendors, PRs, RFQs, POs, invoices…' }}
          value={q}
          onChange={setQ}
        />
      </div>
      {open && (
        <>
          <div className="mscrim" onClick={() => setOpen(false)} />
          <div className="gsr" role="listbox" aria-label="Search results">
            {flat.length === 0 && <div className="gsr-empty hint">No matches.</div>}
            {groups.map((g) => (
              <div key={g.type}>
                <div className="gsr-group">{TYPE_META[g.type]?.label ?? g.type}</div>
                {g.items.map((h) => {
                  const idx = flat.indexOf(h)
                  const row = (
                    <button
                      type="button"
                      key={h.id}
                      role="option"
                      aria-selected={idx === active}
                      className={`gsr-item${idx === active ? ' on' : ''}`}
                      onMouseEnter={() => setActive(idx)}
                      onClick={() => select(h)}
                    >
                      <Icon name={TYPE_META[h.type]?.icon ?? 'doc'} size={13} />
                      <span className="mono">{h.code}</span>
                      <span className="gsr-title">{h.title}</span>
                    </button>
                  )
                  return (h.type === 'Vendor' || h.type === 'Rfq')
                    ? <QuickView key={h.id} fetcher={quickViewFetcher(h)} title={h.code}>{row}</QuickView>
                    : row
                })}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  )
}

const NEW_ITEMS: { action: GatedAction; label: string; icon: string; route: string }[] = [
  { action: 'createPr', label: 'Purchase Requisition', icon: 'doc', route: 'reqs' },
  { action: 'createRfq', label: 'RFQ', icon: 'rfq', route: 'consolidate' },
  { action: 'createVendor', label: 'Vendor', icon: 'vendor', route: 'vendors/new' },
  { action: 'inviteOnboarding', label: 'Vendor invitation', icon: 'clip', route: 'onboarding/invite' },
  { action: 'createForm', label: 'Form', icon: 'edit', route: 'forms' },
  { action: 'createUser', label: 'User', icon: 'vendor', route: 'admin' },
  { action: 'createCustomList', label: 'Custom list', icon: 'clip', route: 'lists' },
]

/** Quick-create — role-aware via <Gated>, routes to EXISTING create screens only. */
export function NewMenu({ onNavigate }: { onNavigate: (hash: string) => void }) {
  const { isVendor } = useIdentity()
  const [open, setOpen] = useState(false)
  if (isVendor) return null   // every quick-create gate is buyer/admin-side today
  return (
    <div className="tb-menuwrap">
      <button type="button" className="tb-btn" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <Icon name="plus" size={14} /> New
      </button>
      {open && (
        <>
          <div className="mscrim" onClick={() => setOpen(false)} />
          <div className="tb-menu" role="menu" aria-label="Quick create">
            {NEW_ITEMS.map((it) => (
              <Gated action={it.action} key={it.action}>
                <button type="button" role="menuitem" className="tb-item" onClick={() => { setOpen(false); onNavigate(it.route) }}>
                  <Icon name={it.icon} size={13} /> {it.label}
                </button>
              </Gated>
            ))}
          </div>
        </>
      )}
    </div>
  )
}

/** Recent records — localStorage, per principal, capped (server persistence rides D4). */
export function RecentsMenu({ onNavigate }: { onNavigate: (hash: string) => void }) {
  const { code: principal } = useIdentity()
  const recents = useRecents(principal)
  const [open, setOpen] = useState(false)
  const openRec = (r: RecentRecord) => { setOpen(false); onNavigate(r.hash) }
  return (
    <div className="tb-menuwrap">
      <button type="button" className="tb-btn" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <Icon name="clock" size={14} /> Recent
      </button>
      {open && (
        <>
          <div className="mscrim" onClick={() => setOpen(false)} />
          <div className="tb-menu" role="menu" aria-label="Recent records">
            {recents.length === 0 && <div className="gsr-empty hint">Nothing visited yet.</div>}
            {recents.map((r) => (
              <button type="button" role="menuitem" className="tb-item" key={r.hash} onClick={() => openRec(r)}>
                <Icon name={TYPE_META[r.type]?.icon ?? 'doc'} size={13} /> <span className="mono">{r.code}</span>
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  )
}

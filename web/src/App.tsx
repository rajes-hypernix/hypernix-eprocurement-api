import { lazy, Suspense, useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { TopBar } from './components/TopBar'
import { recordRecent, type RecentRecord } from './lib/recents'
import { Sidebar } from './components/Sidebar'
import { Dashboard } from './components/Dashboard'
import { VendorsPage } from './components/vendors/VendorsPage'
import { AdminUsers } from './components/admin/AdminUsers'
import { AdminCustomLists } from './components/admin/AdminCustomLists'
import { AdminCustomFields } from './components/admin/AdminCustomFields'
import { Requisitions } from './components/sourcing/Requisitions'
import { Consolidate } from './components/sourcing/Consolidate'
import { RfqList } from './components/sourcing/RfqList'
import { RfqBuilder } from './components/sourcing/RfqBuilder'
import { Forms } from './components/sourcing/Forms'
import { EvalPage } from './components/eval/EvalScreens'
import { AwardsPage } from './components/award/AwardScreens'
import { PoPage } from './components/po/PoScreens'
import { DeliveryPage } from './components/delivery/DeliveryScreens'
import { InvoicePage } from './components/invoice/InvoiceScreens'
import { StatementPage } from './components/statement/StatementScreens'
import { Clarifications } from './components/comm/Clarifications'
import { ChatDock } from './components/comm/ChatDock'
import { VendorPortal } from './components/vendor/VendorPortal'
import { OnboardingPage } from './components/onboarding/OnboardingPage'
import { OnboardingPortal } from './components/onboarding/OnboardingPortal'
import { BUYER_NAV, gateNav } from './nav'
import { VENDOR_NAV } from './vendorNav'
import { useIdentity } from './identity'

const LABELS: Record<string, string> = Object.fromEntries(
  BUYER_NAV.flatMap((g) => g.items.map((i) => [i.key, i.label])),
)

// /design/gallery — dev-only living catalogue of the ui/ primitives, gated the
// same way demo identity is (Development only). The dynamic import behind the
// DEV check keeps the gallery module tree-shaken OUT of production bundles.
const Gallery = import.meta.env.DEV ? lazy(() => import('./ui/gallery/Gallery')) : null
const DEV_BUYER_NAV = import.meta.env.DEV
  ? [...BUYER_NAV, { title: 'Design (dev)', items: [{ key: 'design', icon: 'edit', label: 'Gallery' }] }]
  : BUYER_NAV

function PvPlaceholder() {
  return (
    <>
      <div className="pagehead"><div><h1>Payment Vouchers</h1><p>Supplier remittance and payment release.</p></div></div>
      <div className="card"><div className="cbody" style={{ textAlign: 'center', padding: '40px 20px' }}>
        <p className="muted">No payment vouchers — coming soon (deferred in the current scope).</p>
      </div></div>
    </>
  )
}

function Placeholder({ label }: { label: string }) {
  return (
    <>
      <div className="pagehead">
        <div><h1>{label}</h1><p>Coming in a later slice.</p></div>
      </div>
      <div className="card"><div className="cbody"><p className="muted">This screen is part of the build plan and is not implemented yet.</p></div></div>
    </>
  )
}

// Detail routes worth remembering, with the query key their screen already
// populates — the recents watcher reads the CODE from the warm cache.
const RECENT_ROUTES: { re: RegExp; type: RecentRecord['type']; key: (id: string) => unknown[] }[] = [
  { re: /^vendors\/([0-9a-f-]{36})$/, type: 'Vendor', key: (id) => ['vendor', id] },
  { re: /^rfqs\/([0-9a-f-]{36})$/, type: 'Rfq', key: (id) => ['rfq', id] },
  { re: /^bid\/([0-9a-f-]{36})$/, type: 'Rfq', key: (id) => ['rfq', id] },
  { re: /^pos\/([0-9a-f-]{36})$/, type: 'PurchaseOrder', key: (id) => ['po', id] },
  { re: /^invoices\/([0-9a-f-]{36})$/, type: 'Invoice', key: (id) => ['invoice', id] },
]

export default function App() {
  const { isVendor, code: principal, permissions } = useIdentity()
  const qc = useQueryClient()
  const [active, setActive] = useState(() => window.location.hash.slice(1) || 'dashboard')

  useEffect(() => {
    const onHash = () => setActive(window.location.hash.slice(1) || 'dashboard')
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [])

  // Recents (D2): after a detail screen has loaded (its query is warm), record
  // the visit with its business code. localStorage, per principal, capped.
  useEffect(() => {
    const match = RECENT_ROUTES.map((r) => ({ r, m: active.match(r.re) })).find((x) => x.m)
    if (!match?.m) return
    const id = match.m[1]
    const t = setTimeout(() => {
      const data = qc.getQueryData<{ code?: string | null }>(match.r.key(id))
      if (data?.code) recordRecent(principal, { type: match.r.type, code: data.code, hash: active })
    }, 1500)
    return () => clearTimeout(t)
  }, [active, principal, qc])

  const go = (key: string) => {
    window.location.hash = key
    setActive(key)
  }

  const base = active.split('/')[0]

  // The vendor magic-link onboarding is a public, no-login, token-scoped flow — rendered full-bleed,
  // outside the buyer/vendor shell.
  if (base === 'onboard') return <OnboardingPortal />

  return (
    <>
      <TopBar onNavigate={go} />
      <div className="shell">
        {/* RM-P1: internal nav filtered by the server-derived permission list (same source as
            <Gated>); the vendor portal nav is already role-built and stays as-is. */}
        <Sidebar nav={isVendor ? VENDOR_NAV : gateNav(DEV_BUYER_NAV, permissions)} active={base} onSelect={go} />
        <main className="main">
          {isVendor ? (
            <VendorPortal route={active} onNavigate={go} />
          ) : (
            <>
              {base === 'dashboard' && <Dashboard onNavigate={go} />}
              {base === 'vendors' && <VendorsPage route={active} onNavigate={go} />}
              {base === 'onboarding' && <OnboardingPage route={active} onNavigate={go} />}
              {base === 'admin' && <AdminUsers />}
              {base === 'lists' && <AdminCustomLists />}
              {base === 'customfields' && <AdminCustomFields />}
              {base === 'reqs' && <Requisitions onOpenRfq={(id) => go(`rfqs/${id}`)} onConsolidate={() => go('consolidate')} />}
              {base === 'consolidate' && <Consolidate onOpenRfq={(id) => go(`rfqs/${id}`)} onBack={() => go('reqs')} />}
              {base === 'rfqs' &&
                (active.startsWith('rfqs/view/') ? (
                  // Reminders click-through (D4): open the list WITH that saved view picked.
                  <RfqList onOpen={(id) => go(`rfqs/${id}`)} onNew={() => go('consolidate')} initialViewId={active.slice('rfqs/view/'.length)} />
                ) : active.startsWith('rfqs/') ? (
                  <RfqBuilder id={active.slice('rfqs/'.length)} onBack={() => go('rfqs')} onNavigate={go} />
                ) : (
                  <RfqList onOpen={(id) => go(`rfqs/${id}`)} onNew={() => go('consolidate')} />
                ))}
              {base === 'forms' && <Forms route={active} onNavigate={go} />}
              {base === 'openings' && <EvalPage route={active} onNavigate={go} />}
              {base === 'awards' && <AwardsPage route={active} onNavigate={go} />}
              {base === 'pos' && <PoPage route={active} onNavigate={go} />}
              {base === 'deliveries' && <DeliveryPage route={active} onNavigate={go} />}
              {base === 'invoices' && <InvoicePage route={active} onNavigate={go} />}
              {base === 'statements' && <StatementPage route={active} onNavigate={go} />}
              {base === 'chats' && <Clarifications />}
              {base === 'payments' && <PvPlaceholder />}
              {base === 'design' && Gallery && <Suspense fallback={null}><Gallery /></Suspense>}
              {!['dashboard', 'vendors', 'onboarding', 'admin', 'lists', 'customfields', 'reqs', 'consolidate', 'rfqs', 'forms', 'openings', 'awards', 'pos', 'deliveries', 'invoices', 'statements', 'chats', 'payments', 'design'].includes(base) && (
                <Placeholder label={LABELS[base] ?? base} />
              )}
            </>
          )}
        </main>
      </div>
      <ChatDock route={active} onNavigate={go} />
    </>
  )
}

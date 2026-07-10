import { useEffect, useState } from 'react'
import { TopBar } from './components/TopBar'
import { Sidebar } from './components/Sidebar'
import { Dashboard } from './components/Dashboard'
import { VendorsPage } from './components/vendors/VendorsPage'
import { AdminUsers } from './components/admin/AdminUsers'
import { AdminCustomLists } from './components/admin/AdminCustomLists'
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
import { BUYER_NAV } from './nav'
import { VENDOR_NAV } from './vendorNav'
import { useIdentity } from './identity'

const LABELS: Record<string, string> = Object.fromEntries(
  BUYER_NAV.flatMap((g) => g.items.map((i) => [i.key, i.label])),
)

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

export default function App() {
  const { isVendor } = useIdentity()
  const [active, setActive] = useState(() => window.location.hash.slice(1) || 'dashboard')

  useEffect(() => {
    const onHash = () => setActive(window.location.hash.slice(1) || 'dashboard')
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [])

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
      <TopBar />
      <div className="shell">
        <Sidebar nav={isVendor ? VENDOR_NAV : BUYER_NAV} active={base} onSelect={go} />
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
              {base === 'reqs' && <Requisitions onOpenRfq={(id) => go(`rfqs/${id}`)} onConsolidate={() => go('consolidate')} />}
              {base === 'consolidate' && <Consolidate onOpenRfq={(id) => go(`rfqs/${id}`)} onBack={() => go('reqs')} />}
              {base === 'rfqs' &&
                (active.startsWith('rfqs/') ? (
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
              {!['dashboard', 'vendors', 'onboarding', 'admin', 'lists', 'reqs', 'consolidate', 'rfqs', 'forms', 'openings', 'awards', 'pos', 'deliveries', 'invoices', 'statements', 'chats', 'payments'].includes(base) && (
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

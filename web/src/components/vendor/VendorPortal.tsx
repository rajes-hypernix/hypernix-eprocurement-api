import { MyRfqs } from './MyRfqs'
import { BidForm } from './BidForm'
import { PoPage } from '../po/PoScreens'
import { DeliveryPage } from '../delivery/DeliveryScreens'
import { InvoicePage } from '../invoice/InvoiceScreens'
import { StatementPage } from '../statement/StatementScreens'
import { Clarifications } from '../comm/Clarifications'
import { Dashboard } from '../Dashboard'

export function VendorPortal({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  const base = route.split('/')[0]

  if (base === 'bid') {
    const id = route.slice('bid/'.length)
    return <BidForm rfqId={id} onBack={() => onNavigate('dashboard')} />
  }
  if (base === 'bids') return <MyRfqs mode="bids" onOpen={(id) => onNavigate(`bid/${id}`)} />
  if (base === 'pos') return <PoPage route={route} onNavigate={onNavigate} />
  if (base === 'deliveries') return <DeliveryPage route={route} onNavigate={onNavigate} />
  if (base === 'invoices') return <InvoicePage route={route} onNavigate={onNavigate} />
  if (base === 'statement') return <StatementPage route={route} onNavigate={onNavigate} />
  if (base === 'chats') return <Clarifications />
  if (base === 'dashboard' || base === '') return <Dashboard onNavigate={onNavigate} />

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>{base}</h1>
          <p>Coming in a later slice.</p>
        </div>
      </div>
      <div className="card"><div className="cbody"><p className="muted">This vendor-portal screen is part of the build plan and is not implemented yet.</p></div></div>
    </>
  )
}

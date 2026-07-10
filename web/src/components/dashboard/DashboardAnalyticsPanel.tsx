import { useState } from 'react'
import { SpendTrendChart } from './SpendTrendChart'
import { VendorsOnboardedChart } from './VendorsOnboardedChart'
import { RecentPosCard } from './RecentPosCard'
import { SPEND_SERIES, SPEND_HEADLINE, VENDOR_SEGMENTS } from '../../mock/dashboardAnalytics'

// Buyer dashboard analytics panel (DEMO — hardcoded data via src/mock/dashboardAnalytics).
// Replaces the old "Active RFQs" table. Composes the spend trend, vendor onboarding, and recent-PO
// widgets. All "View all/details" links route to real existing pages via onNavigate.
export function DashboardAnalyticsPanel({ onNavigate }: { onNavigate: (key: string) => void }) {
  const [showForecast, setShowForecast] = useState(true)

  return (
    <div className="da-panel">
      <div className="da-head">
        <span className="da-head-t">Sourcing analytics</span>
        <span className="da-head-meta">Updated just now · FY to date</span>
        <div className="spacer" style={{ flex: 1 }} />
        <button
          type="button"
          className={`da-toggle ${showForecast ? 'on' : ''}`}
          aria-pressed={showForecast}
          onClick={() => setShowForecast((v) => !v)}
        >
          <span className="da-tk" /><span>Show forecast</span>
        </button>
      </div>

      <div className="da-grid">
        {/* LEFT — editorial headline + spend trend */}
        <div className="da-colL">
          <div className="da-hlrow">
            <h2 className="da-headline">{SPEND_HEADLINE.lead} <span className="da-up">{SPEND_HEADLINE.deltaLabel}</span></h2>
            <button type="button" className="lnk da-viewdetails" onClick={() => onNavigate('rfqs')}>View details</button>
          </div>
          <p className="da-hlsub">{SPEND_HEADLINE.subtitle}</p>

          <SpendTrendChart showForecast={showForecast} />

          <div className="da-legend">
            {SPEND_SERIES.map((sr) => (
              <span className="da-li" key={sr.key}><span className="da-sw" style={{ background: `var(${sr.colorVar})` }} />{sr.key}</span>
            ))}
            {showForecast && SPEND_SERIES.map((sr) => (
              <span className="da-li" key={`${sr.key}-fc`} style={{ color: `var(${sr.colorVar})` }}>
                <span className="da-sw da-dash" /><span style={{ color: 'var(--muted)' }}>{sr.key} forecast</span>
              </span>
            ))}
          </div>
        </div>

        {/* RIGHT — vendor onboarding + recent POs */}
        <div className="da-colR">
          <div className="da-rblock">
            <div className="da-rhead"><span className="da-rhead-t">New vendors onboarded</span><button type="button" className="lnk" onClick={() => onNavigate('vendors')}>View all</button></div>
            <div className="da-rsub">Vendors approved into the master each month over the past year.</div>
            <VendorsOnboardedChart />
            <div className="da-legend" style={{ marginTop: 8 }}>
              {VENDOR_SEGMENTS.map((seg) => (
                <span className="da-li" key={seg.key}><span className="da-sw" style={{ background: `var(${seg.colorVar})` }} />{seg.key}</span>
              ))}
            </div>
          </div>

          <div className="da-rblock">
            <div className="da-rhead"><span className="da-rhead-t">Top recent purchase orders</span><button type="button" className="lnk" onClick={() => onNavigate('pos')}>View all</button></div>
            <div className="da-rsub">Based on POs issued in the last 30 days.</div>
            <RecentPosCard />
          </div>
        </div>
      </div>

      <div className="da-note">Prototype — figures are illustrative demo data pending the analytics layer.</div>
    </div>
  )
}

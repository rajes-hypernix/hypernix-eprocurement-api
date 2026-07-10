import { RECENT_POS } from '../../mock/dashboardAnalytics'
import { fmt } from '../../lib/format'

// Compact top-5 recent POs. Codes are plain mono TEXT (mock IDs — must not link to dead detail routes).
export function RecentPosCard() {
  return (
    <table className="da-pos">
      <thead>
        <tr><th>PO</th><th>Vendor</th><th>Status</th><th>Total</th></tr>
      </thead>
      <tbody>
        {RECENT_POS.map((po) => (
          <tr key={po.code}>
            <td><span className="mono">{po.code}</span></td>
            <td>{po.vendor}</td>
            <td><span className={`pill ${po.tone}`}>{po.status}</span></td>
            <td>RM {fmt(po.total)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

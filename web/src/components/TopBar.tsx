import { useIdentity } from '../identity'
import { initials, roleLabel } from '../lib/format'
import { GlobalSearch, NewMenu, RecentsMenu } from './TopBarNav'

export function TopBar({ onNavigate }: { onNavigate: (hash: string) => void }) {
  const { code, persona, personas, switchTo } = useIdentity()
  const name = persona?.name ?? 'Faridah Yusof'
  const sub = persona?.kind === 'vendor' ? 'Vendor' : (persona?.roles ?? []).map((r) => roleLabel(r ?? '')).join(' · ') || 'Buyer'

  return (
    <div className="topbar">
      <div className="brand">
        <span className="logo">
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth={2.1} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M3 7l9-4 9 4-9 4-9-4z" />
            <path d="M3 7v10l9 4 9-4V7" />
            <path d="M12 11v10" />
          </svg>
        </span>
        <span>
          <span className="brand-title">Hypernix eProcure</span>
          <small>Sourcing &amp; Vendor Portal</small>
        </span>
      </div>
      <div className="tb-center">
        <GlobalSearch onNavigate={onNavigate} />
        <NewMenu onNavigate={onNavigate} />
        <RecentsMenu onNavigate={onNavigate} />
      </div>
      <div className="spacer" />
      <div className="whoami">
        <label className="hint" style={{ color: 'rgba(255,255,255,.7)', marginRight: 4 }}>Act as</label>
        <select
          aria-label="Act as"
          value={code}
          onChange={(e) => switchTo(e.target.value)}
          style={{ width: 'auto', background: 'rgba(255,255,255,.13)', color: '#fff', border: '1px solid rgba(255,255,255,.2)' }}
        >
          <optgroup label="Internal users">
            {personas.filter((p) => p.kind === 'internal').map((p) => (
              <option key={p.code} value={p.code!}>{p.name}</option>
            ))}
          </optgroup>
          <optgroup label="Vendor logins">
            {personas.filter((p) => p.kind === 'vendor').map((p) => (
              <option key={p.code} value={p.code!}>{p.vendorName}</option>
            ))}
          </optgroup>
        </select>
        <div className="prof-chip" style={{ marginLeft: 4 }}>
          <span className="av">{initials(name)}</span>
          <div className="ptext">
            <div className="pname">{name}</div>
            <div className="prole">{sub}</div>
          </div>
        </div>
      </div>
    </div>
  )
}

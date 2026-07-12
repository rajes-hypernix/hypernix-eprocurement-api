import { useIdentity } from '../../identity'
import type { PortletDto } from '../../api/client'
import { getRecents } from '../../lib/recents'
import { Icon } from '../Icon'

/** The D2 localStorage recents, portlet-ized — per principal, capped, record-type icons. */
export function RecentRecordsPortlet({ onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const { code } = useIdentity()
  const recents = getRecents(code)
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      {recents.map((r) => (
        <button key={r.hash} type="button" className="btn btn-sm btn-ghost"
          style={{ display: 'flex', gap: 8, justifyContent: 'flex-start' }}
          onClick={() => onNavigate(r.hash)}>
          <Icon name={r.type === 'Vendor' ? 'vendor' : r.type === 'Rfq' ? 'rfq' : 'doc'} size={13} />
          <span className="mono">{r.code}</span>
        </button>
      ))}
      {recents.length === 0 && <span className="hint">No recent records in this browser yet.</span>}
    </div>
  )
}

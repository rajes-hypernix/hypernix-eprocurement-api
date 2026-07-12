import { useIdentity } from '../../identity'
import type { PortletDto } from '../../api/client'
import { parseConfig, type ShortcutsConfig } from './portletConfig'
import { Icon } from '../Icon'

/** Role-aware shortcuts — gated by the SAME server permission list as the New menu. */
export function ShortcutsPortlet({ portlet, onNavigate }: { portlet: PortletDto; onNavigate: (key: string) => void }) {
  const { permissions } = useIdentity()
  const cfg = parseConfig<ShortcutsConfig>(portlet.configJson, { items: [] })
  const visible = cfg.items.filter((i) => !i.action || permissions.includes(i.action))
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {visible.map((item) => (
        <button key={item.route + item.label} type="button" className="btn btn-out"
          style={{ display: 'flex', justifyContent: 'space-between', width: '100%' }}
          onClick={() => onNavigate(item.route)}>
          <span>{item.label}</span>
          <Icon name="chev" size={13} />
        </button>
      ))}
      {visible.length === 0 && <span className="hint">No shortcuts for your role.</span>}
    </div>
  )
}

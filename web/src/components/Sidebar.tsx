import { BUYER_NAV, type NavGroup } from '../nav'
import { Icon } from './Icon'

export function Sidebar({
  nav = BUYER_NAV,
  active,
  onSelect,
}: {
  nav?: NavGroup[]
  active: string
  onSelect: (key: string) => void
}) {
  return (
    <aside className="side">
      {nav.map((group) => (
        <div key={group.title}>
          <div className="grp">{group.title}</div>
          {group.items.map((item) => (
            <div
              key={item.key}
              className={`nav${active === item.key ? ' on' : ''}`}
              role="button"
              tabIndex={0}
              aria-current={active === item.key ? 'page' : undefined}
              onClick={() => onSelect(item.key)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') onSelect(item.key)
              }}
            >
              <span className="ic">
                <Icon name={item.icon} />
              </span>
              {item.label}
            </div>
          ))}
        </div>
      ))}
    </aside>
  )
}

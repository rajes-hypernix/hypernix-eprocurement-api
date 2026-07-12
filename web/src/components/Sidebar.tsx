import { useState } from 'react'
import { BUYER_NAV, type NavGroup } from '../nav'
import { Icon } from './Icon'

// CF1-T4: collapse state persists per browser (localStorage) so the choice survives reloads.
const LS_KEY = 'sidebarCollapsed'
const initialCollapsed = () => {
  try { return localStorage.getItem(LS_KEY) === '1' } catch { return false }
}

export function Sidebar({
  nav = BUYER_NAV,
  active,
  onSelect,
}: {
  nav?: NavGroup[]
  active: string
  onSelect: (key: string) => void
}) {
  const [collapsed, setCollapsed] = useState(initialCollapsed)
  const toggle = () => {
    setCollapsed((c) => {
      try { localStorage.setItem(LS_KEY, c ? '0' : '1') } catch { /* non-browser env */ }
      return !c
    })
  }

  return (
    <aside className={`side${collapsed ? ' collapsed' : ''}`}>
      <button
        type="button"
        className="side-toggle"
        aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        aria-expanded={!collapsed}
        title={collapsed ? 'Expand' : 'Collapse'}
        onClick={toggle}
      >
        <Icon name={collapsed ? 'chev' : 'back'} size={15} />
      </button>
      {nav.map((group) => (
        <div key={group.title}>
          <div className="grp">{collapsed ? '\u00a0' : group.title}</div>
          {group.items.map((item) => (
            <div
              key={item.key}
              className={`nav${active === item.key ? ' on' : ''}`}
              role="button"
              tabIndex={0}
              aria-current={active === item.key ? 'page' : undefined}
              // Collapsed rail is icon-only, so the label must be explicit; expanded items
              // are named by their visible text (and an aria-label here would collide with
              // same-named form fields for assistive tech and tests alike).
              aria-label={collapsed ? item.label : undefined}
              title={collapsed ? item.label : undefined}
              onClick={() => onSelect(item.key)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') onSelect(item.key)
              }}
            >
              <span className="ic">
                <Icon name={item.icon} />
              </span>
              {!collapsed && item.label}
            </div>
          ))}
        </div>
      ))}
    </aside>
  )
}

import type { ReactNode } from 'react'
import type { PortletDto } from '../../api/client'

/**
 * The fifth archetype (charter rule 4): a dashboard is a grid of portlet instances read
 * from the server (role default or the user's personalized copy — D4). Pure layout: a
 * two-column grid ordered by Row/Col, Width 2 spanning both. Arrange mode (arrow-based,
 * ruled: testable beats flashy) decorates each portlet with move/width/remove chrome; the
 * portlet CONTENT comes from the renderPortlet map the instantiating page supplies.
 */
export function DashboardPage({
  title, subtitle, toolbar, portlets, renderPortlet,
  arrangeMode = false, onMoveUp, onMoveDown, onToggleWidth, onRemove,
}: {
  title: string
  subtitle?: string
  toolbar?: ReactNode
  portlets: PortletDto[]
  renderPortlet: (p: PortletDto) => ReactNode
  arrangeMode?: boolean
  onMoveUp?: (p: PortletDto) => void
  onMoveDown?: (p: PortletDto) => void
  onToggleWidth?: (p: PortletDto) => void
  onRemove?: (p: PortletDto) => void
}) {
  const ordered = [...portlets].sort((a, b) => a.row - b.row || a.col - b.col)
  return (
    <div>
      <div className="pagehead">
        <div>
          <h1>{title}</h1>
          {subtitle && <p className="sub">{subtitle}</p>}
        </div>
        {toolbar && <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>{toolbar}</div>}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        {ordered.map((p) => (
          <section
            key={p.id || `${p.portletType}-${p.row}-${p.col}`}
            className="card"
            style={{ gridColumn: p.width >= 2 ? '1 / -1' : undefined, padding: 14 }}
            aria-label={p.title}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 8 }}>
              <h3 style={{ margin: 0 }}>{p.title}</h3>
              {arrangeMode && (
                <span style={{ display: 'flex', gap: 4 }}>
                  <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${p.title} up`} onClick={() => onMoveUp?.(p)}>↑</button>
                  <button type="button" className="btn btn-sm btn-out" aria-label={`Move ${p.title} down`} onClick={() => onMoveDown?.(p)}>↓</button>
                  <button type="button" className="btn btn-sm btn-out" aria-label={`Toggle ${p.title} width`} onClick={() => onToggleWidth?.(p)}>⇔</button>
                  <button type="button" className="btn btn-sm btn-out" aria-label={`Remove ${p.title}`} onClick={() => onRemove?.(p)}>✕</button>
                </span>
              )}
            </div>
            {renderPortlet(p)}
          </section>
        ))}
        {ordered.length === 0 && (
          <div className="card" style={{ gridColumn: '1 / -1', padding: 24, textAlign: 'center' }}>
            <p className="hint">Nothing here yet — use “Act as” to switch personas, or personalize to add portlets.</p>
          </div>
        )}
      </div>
    </div>
  )
}

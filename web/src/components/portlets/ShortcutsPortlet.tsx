import { useState } from 'react'
import { useIdentity } from '../../identity'
import type { PortletDto } from '../../api/client'
import { parseConfig, type ShortcutsConfig, type ShortcutItem } from './portletConfig'
import { Icon } from '../Icon'
import { Modal } from '../ui'
import { TextField } from '../../ui/TextField'
import { SelectField } from '../../ui/SelectField'

// CF3-T10: the tile palette (brand-adjacent, closed set — colours are data, not free CSS).
export const TILE_COLORS: { code: string; label: string }[] = [
  { code: '', label: 'Default' },
  { code: '#336374', label: 'Teal' },
  { code: '#25586B', label: 'Deep teal' },
  { code: '#7A5C2E', label: 'Ochre' },
  { code: '#5C7A2E', label: 'Olive' },
  { code: '#7A2E45', label: 'Plum' },
]

// Target pages a tile can link to (the sidebar's route bases — kept in step by nav-coverage).
const TILE_TARGETS: { code: string; label: string }[] = [
  { code: 'dashboard', label: 'Dashboard' }, { code: 'views', label: 'Saved Views' },
  { code: 'reqs', label: 'Requisitions' }, { code: 'rfqs', label: 'RFQs' },
  { code: 'pos', label: 'Purchase Orders' }, { code: 'deliveries', label: 'Deliveries' },
  { code: 'invoices', label: 'Invoices' }, { code: 'statements', label: 'Statements' },
  { code: 'vendors', label: 'Vendor Master' }, { code: 'onboarding', label: 'Onboarding' },
  { code: 'chats', label: 'Clarifications' },
]

/** Role-aware shortcuts — gated by the SAME server permission list as the New menu.
 *  CF3-T10: on a personalized dashboard tiles are AUTHORABLE — add, colour, target. */
export function ShortcutsPortlet({ portlet, onNavigate, onUpdateConfig }: {
  portlet: PortletDto
  onNavigate: (key: string) => void
  /** present only on the personalized dashboard — persists the edited config */
  onUpdateConfig?: (portletId: string, configJson: string) => void
}) {
  const { permissions } = useIdentity()
  const cfg = parseConfig<ShortcutsConfig>(portlet.configJson, { items: [] })
  const visible = cfg.items.filter((i) => !i.action || permissions.includes(i.action))
  const [adding, setAdding] = useState(false)
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {visible.map((item) => (
        <button key={item.route + item.label} type="button" className="btn btn-out"
          style={{
            display: 'flex', justifyContent: 'space-between', width: '100%',
            ...(item.color ? { background: item.color, color: '#fff', borderColor: item.color } : {}),
          }}
          onClick={() => onNavigate(item.route)}>
          <span>{item.label}</span>
          <Icon name="chev" size={13} />
        </button>
      ))}
      {visible.length === 0 && <span className="hint">No shortcuts yet.</span>}
      {onUpdateConfig && (
        <button type="button" className="btn btn-sm btn-ghost" onClick={() => setAdding(true)} aria-label="Add tile">
          <Icon name="plus" size={13} /> Add tile
        </button>
      )}
      {adding && onUpdateConfig && (
        <AddTileModal
          onClose={() => setAdding(false)}
          onAdd={(item) => {
            onUpdateConfig(portlet.id, JSON.stringify({ items: [...cfg.items, item] }))
            setAdding(false)
          }}
        />
      )}
    </div>
  )
}

function AddTileModal({ onClose, onAdd }: { onClose: () => void; onAdd: (item: ShortcutItem) => void }) {
  const [label, setLabel] = useState('')
  const [route, setRoute] = useState('dashboard')
  const [color, setColor] = useState('')
  return (
    <Modal
      title="Add tile" icon="plus"
      footer={<>
        <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
        <button type="button" className="btn btn-pri" disabled={!label.trim()}
          onClick={() => onAdd({ label: label.trim(), route, color: color || null })}>Add tile</button>
      </>}
    >
      <TextField spec={{ key: 'tile-label', label: 'Tile label', dataType: 'text' }} value={label} onChange={(v) => setLabel(String(v ?? ''))} />
      <SelectField spec={{ key: 'tile-target', label: 'Target page', dataType: 'select', options: { kind: 'static', options: TILE_TARGETS } }}
        value={route} onChange={(v) => setRoute(String(v ?? 'dashboard'))} />
      <SelectField spec={{ key: 'tile-color', label: 'Colour', dataType: 'select', options: { kind: 'static', options: TILE_COLORS.map((c) => ({ code: c.code, label: c.label })) } }}
        value={color} onChange={(v) => setColor(String(v ?? ''))} />
    </Modal>
  )
}

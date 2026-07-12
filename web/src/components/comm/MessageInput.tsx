import { Icon } from '../Icon'

/**
 * The one message-input row (A2F-T7, audit NIT): ChatDock and Clarifications carried
 * byte-identical copies. className passes through so each host keeps its exact markup
 * (.cd-ft cinrow vs .cinrow) — extraction, not restyling.
 */
export function MessageInput({ value, onChange, onSend, busy, className = 'cinrow' }: {
  value: string
  onChange: (v: string) => void
  onSend: () => void
  busy?: boolean
  className?: string
}) {
  return (
    <div className={className}>
      <input type="text" value={value} placeholder="Type a message…" onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => { if (e.key === 'Enter' && value.trim()) { e.preventDefault(); onSend() } }} />
      <button type="button" className="cibtn" disabled={!value.trim() || busy} onClick={onSend}><Icon name="send" size={15} /></button>
    </div>
  )
}

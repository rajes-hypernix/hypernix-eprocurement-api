import type { ReactNode } from 'react'
import type { Tone } from './tokens'

/**
 * The badge/pill render layer (D0 census: 6 tones, 11 status→tone maps).
 * Domain maps (prStatus, rfqStatus, vendors/badges, screen-local maps) stay
 * at the domain edge — they are business vocabulary; these components are the
 * ONE way a tone becomes pixels.
 */

/** Dot badge — the .badge b-* idiom (status columns, headers). */
export function StatusBadge({ tone, children, title }: { tone: Tone; children: ReactNode; title?: string }) {
  return <span className={`badge b-${tone}`} title={title}>{children}</span>
}

/** Filled pill — the .pill b-* idiom. */
export function Pill({ tone, children, title }: { tone: Tone; children: ReactNode; title?: string }) {
  return <span className={`pill b-${tone}`} title={title}>{children}</span>
}

/** Category chip — the .swchip idiom; `hit` = matched state; optional remove. */
export function Chip({ hit, title, onRemove, children }: {
  hit?: boolean
  title?: string
  onRemove?: () => void
  children: ReactNode
}) {
  return (
    <span
      className={`swchip${hit ? ' hit' : ''}`}
      title={title}
      style={onRemove ? { cursor: 'pointer' } : undefined}
      onClick={onRemove}
    >
      {children}{onRemove ? ' ✕' : ''}
    </span>
  )
}

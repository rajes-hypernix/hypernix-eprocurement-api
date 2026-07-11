import type { ReactNode } from 'react'

/**
 * Read-only display primitives (D0 census: ~50 stat-card and ~50 key-value
 * usages, with Stat re-implemented locally in 3 files and the .kv/.vkv rival
 * pair). One rendering each; screens migrate at their gates.
 */

/** KPI stat card — the .card.stat idiom; tone maps to the stat wash tokens. */
export function Stat({ label, value, sub, tone }: {
  label: string
  value: ReactNode
  sub?: ReactNode
  tone?: 'teal' | 'amber' | 'red'
}) {
  return (
    <div className={`card stat${tone ? ` tone-${tone}` : ''}`}>
      <div className="lbl">{label}</div>
      <div className="num">{value}</div>
      {sub && <div className="sub">{sub}</div>}
    </div>
  )
}

/** Entity-detail key-value row — the .vkv idiom (hint + value). */
export function Kv({ k, v }: { k: string; v: ReactNode }) {
  return (
    <div className="vkv">
      <span className="hint">{k}</span>
      <span className="vv">{v}</span>
    </div>
  )
}

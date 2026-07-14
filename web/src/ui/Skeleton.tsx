import type { CSSProperties } from 'react'

/**
 * CFF-T6 — skeleton loaders that replace blank-white flashes while data loads (shimmer per
 * the UI-POLISH reference). `Skeleton` is one shimmer block; `SkeletonRows` fills a table
 * body with placeholder rows so a loading list reads as "loading", not "empty".
 */
export function Skeleton({ height = 14, width = '100%', style }: { height?: number | string; width?: number | string; style?: CSSProperties }) {
  return <div className="sk" data-testid="skeleton" style={{ height, width, ...style }} />
}

export function SkeletonRows({ rows = 5, cols }: { rows?: number; cols: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, r) => (
        <tr key={r} aria-hidden="true">
          {Array.from({ length: cols }).map((_, c) => (
            <td key={c}><Skeleton width={c === 0 ? '60%' : '80%'} /></td>
          ))}
        </tr>
      ))}
    </>
  )
}

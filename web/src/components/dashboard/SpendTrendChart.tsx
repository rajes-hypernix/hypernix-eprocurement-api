import { useRef, useState } from 'react'
import { SPEND_MONTHS, SPEND_ACTUAL_MONTHS, SPEND_SERIES, SPEND_MAX } from '../../mock/dashboardAnalytics'

// Committed-spend area chart: 12 months actual + 3 months dashed forecast, per the demo mockup.
// Geometry is computed from the data arrays (no chart library). `showForecast` hides the dashed
// segments and forecast month labels.

const W = 640, H = 320
const PAD = { l: 52, r: 14, t: 12, b: 30 }
const IW = W - PAD.l - PAD.r
const IH = H - PAD.t - PAD.b
const N = SPEND_MONTHS.length
const LAST_ACTUAL = SPEND_ACTUAL_MONTHS - 1   // index of the current (most recent actual) month

const xAt = (i: number) => PAD.l + IW * (i / (N - 1))
const yAt = (v: number) => PAD.t + IH * (1 - v / SPEND_MAX)

// "M x y L x y …" over an inclusive index range.
const linePath = (vals: number[], from: number, to: number): string => {
  let d = ''
  for (let i = from; i <= to; i++) d += `${i === from ? 'M' : 'L'}${xAt(i).toFixed(1)} ${yAt(vals[i]).toFixed(1)} `
  return d.trim()
}

// Paint filled areas back-to-front by peak value so smaller series stay visible.
const areaOrder = [...SPEND_SERIES].sort((a, b) => Math.max(...b.vals) - Math.max(...a.vals))

const monthYear = (i: number) => `${SPEND_MONTHS[i]} ${i < 6 ? '2025' : '2026'}`

export function SpendTrendChart({ showForecast }: { showForecast: boolean }) {
  const svgRef = useRef<SVGSVGElement>(null)
  const [hover, setHover] = useState<number | null>(null)

  const onMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const svg = svgRef.current
    if (!svg) return
    const rect = svg.getBoundingClientRect()
    const sx = (e.clientX - rect.left) * (W / rect.width)
    const limit = showForecast ? N - 1 : LAST_ACTUAL
    const i = Math.max(0, Math.min(limit, Math.round(((sx - PAD.l) / IW) * (N - 1))))
    setHover(i)
  }

  const gridValues = [0, 1, 2, 3, 4].map((g) => (SPEND_MAX * g) / 4)
  const dividerX = xAt(LAST_ACTUAL)

  const tip = (() => {
    if (hover === null) return null
    const rect = svgRef.current?.getBoundingClientRect()
    const width = rect?.width ?? W
    const total = SPEND_SERIES.reduce((s, sr) => s + sr.vals[hover], 0)
    const left = Math.min((xAt(hover) / W) * width + 14, width - 190)
    return { i: hover, left, total }
  })()

  return (
    <div className="da-chartwrap">
      <svg
        ref={svgRef}
        viewBox={`0 0 ${W} ${H}`}
        width="100%"
        role="img"
        aria-label="Committed spend by month with forecast"
        onMouseMove={onMove}
        onMouseLeave={() => setHover(null)}
      >
        {/* gridlines + RM 'k y-axis labels */}
        {gridValues.map((v) => {
          const yy = yAt(v)
          return (
            <g key={v}>
              <line x1={PAD.l} y1={yy} x2={W - PAD.r} y2={yy} stroke="var(--line)" strokeWidth={1} />
              <text x={PAD.l - 8} y={yy + 4} textAnchor="end" fontSize={10.5} fill="var(--muted)" fontFamily="var(--sans)">RM {v}k</text>
            </g>
          )
        })}

        {/* actual / forecast divider */}
        <line x1={dividerX} y1={PAD.t} x2={dividerX} y2={H - PAD.b} stroke="var(--line)" strokeWidth={1} strokeDasharray="3 3" />

        {/* x-axis month labels (forecast labels hidden when the toggle is off) */}
        {SPEND_MONTHS.map((m, i) => {
          if (i > LAST_ACTUAL && !showForecast) return null
          const current = i === LAST_ACTUAL
          return (
            <text key={`${m}-${i}`} x={xAt(i)} y={H - 10} textAnchor="middle" fontSize={10.5}
              fill={current ? 'var(--ink)' : 'var(--muted)'} fontWeight={current ? 600 : 400} fontFamily="var(--sans)">{m}</text>
          )
        })}

        {/* filled areas (actual portion only) */}
        {areaOrder.map((sr) => (
          <path key={sr.key}
            d={`${linePath(sr.vals, 0, LAST_ACTUAL)} L ${xAt(LAST_ACTUAL)} ${yAt(0)} L ${xAt(0)} ${yAt(0)} Z`}
            fill={`var(${sr.colorVar})`} opacity={0.13} />
        ))}

        {/* solid actual lines */}
        {SPEND_SERIES.map((sr) => (
          <path key={sr.key} d={linePath(sr.vals, 0, LAST_ACTUAL)} fill="none" stroke={`var(${sr.colorVar})`} strokeWidth={2.2} />
        ))}

        {/* dashed forecast lines */}
        {showForecast && SPEND_SERIES.map((sr) => (
          <path key={sr.key} d={linePath(sr.vals, LAST_ACTUAL, N - 1)} fill="none" stroke={`var(${sr.colorVar})`}
            strokeWidth={2} strokeDasharray="5 5" opacity={0.85} />
        ))}

        {/* current-month markers */}
        {SPEND_SERIES.map((sr) => (
          <circle key={sr.key} cx={xAt(LAST_ACTUAL)} cy={yAt(sr.vals[LAST_ACTUAL])} r={4} fill={`var(${sr.colorVar})`} />
        ))}

        {/* hover cursor */}
        {hover !== null && (
          <line x1={xAt(hover)} y1={PAD.t} x2={xAt(hover)} y2={H - PAD.b} stroke="var(--ink)" strokeWidth={1} opacity={0.4} strokeDasharray="2 3" />
        )}
      </svg>

      {tip && (
        <div className="da-tip" style={{ left: tip.left, top: 40 }}>
          <b>{monthYear(tip.i)}{tip.i > LAST_ACTUAL ? ' · forecast' : ''}</b>
          {SPEND_SERIES.map((sr) => (
            <div className="da-tip-r" key={sr.key}>
              <span className="k"><span className="da-sw" style={{ background: `var(${sr.colorVar})` }} />{sr.key}</span>
              <span className="v">RM {sr.vals[tip.i].toLocaleString()}k</span>
            </div>
          ))}
          <div className="da-tip-r da-tip-total">
            <span className="k">Total</span>
            <span className="v">RM {(tip.total / 1000).toFixed(2)}M</span>
          </div>
        </div>
      )}
    </div>
  )
}

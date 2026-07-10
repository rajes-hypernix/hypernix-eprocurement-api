import { VENDOR_BAR_MONTHS, VENDOR_SEGMENTS, VENDOR_BARS, VENDOR_BAR_MAX } from '../../mock/dashboardAnalytics'

// New-vendors-onboarded stacked bar chart: 13 months × 3 segments. Geometry from the data arrays.

const W = 340, H = 180
const PAD = { l: 26, r: 4, t: 8, b: 22 }
const IW = W - PAD.l - PAD.r
const IH = H - PAD.t - PAD.b
const GAP = 6
const COL_W = (IW - GAP * (VENDOR_BARS.length - 1)) / VENDOR_BARS.length

export function VendorsOnboardedChart() {
  const gridValues = [0, 1, 2].map((g) => (VENDOR_BAR_MAX * g) / 2)

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" role="img" aria-label="New vendors onboarded per month">
      {/* gridlines + count labels */}
      {gridValues.map((v) => {
        const yy = PAD.t + IH * (1 - v / VENDOR_BAR_MAX)
        return (
          <g key={v}>
            <line x1={PAD.l} y1={yy} x2={W - PAD.r} y2={yy} stroke="var(--line)" />
            <text x={PAD.l - 6} y={yy + 3.5} textAnchor="end" fontSize={9.5} fill="var(--muted)" fontFamily="var(--sans)">{v}</text>
          </g>
        )
      })}

      {/* stacked bars */}
      {VENDOR_BARS.map((segs, i) => {
        const bx = PAD.l + i * (COL_W + GAP)
        let cy = PAD.t + IH
        return (
          <g key={VENDOR_BAR_MONTHS[i]}>
            {segs.map((v, si) => {
              const h = (IH * v) / VENDOR_BAR_MAX
              cy -= h
              return (
                <rect key={VENDOR_SEGMENTS[si].key} x={bx.toFixed(1)} y={cy.toFixed(1)} width={COL_W.toFixed(1)} height={h.toFixed(1)} fill={`var(${VENDOR_SEGMENTS[si].colorVar})`}>
                  <title>{VENDOR_BAR_MONTHS[i]} · {VENDOR_SEGMENTS[si].key}: {v}</title>
                </rect>
              )
            })}
            <text x={(bx + COL_W / 2).toFixed(1)} y={H - 8} textAnchor="middle" fontSize={9} fill="var(--muted)" fontFamily="var(--sans)">{VENDOR_BAR_MONTHS[i]}</text>
          </g>
        )
      })}
    </svg>
  )
}

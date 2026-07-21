// Live Altman Z′ private-firm financial model for the vendor onboarding form.
// A faithful TypeScript port of prototype/vendor-onboarding-mockup.html
// (altmanZ / weighted / bandFor / scoreFor / computeFin) — same coefficients, weights, bands and
// score clamp, so the vendor sees the SAME numbers the server snapshots (parity is asserted in tests).
// The backend (eProcure.Domain.Onboarding.AltmanZModel) is the record of decision; this is display-only.

export const FIN_ITEMS: [label: string, key: FinKey][] = [
  ['Revenue', 'revenue'],
  ['Net profit', 'netProfit'],
  ['EBIT', 'ebit'],
  ['Total assets', 'totalAssets'],
  ['Current assets', 'currentAssets'],
  ['Inventory', 'inventory'],
  ['Current liabilities', 'currentLiab'],
  ['Total liabilities', 'totalLiab'],
  ['Shareholders’ equity', 'equity'],
  ['Retained earnings', 'retainedEarnings'],
  ['Fixed assets', 'fixedAssets'],
]

export type FinKey =
  | 'revenue' | 'netProfit' | 'ebit' | 'totalAssets' | 'currentAssets' | 'inventory'
  | 'currentLiab' | 'totalLiab' | 'equity' | 'retainedEarnings' | 'fixedAssets'

/** 11 line items, each a 3-year tuple [FY-2, FY-1, current] in RM'000. */
export type FinData = Record<FinKey, [number, number, number]>

// Year weights FY-2 / FY-1 / current (prototype FIN_W).
const FIN_W = [0.2, 0.3, 0.5]

export type Band = { band: 'A' | 'B' | 'C' | 'D'; risk: string; zone: string; cls: string }

export function blankFin(): FinData {
  return Object.fromEntries(FIN_ITEMS.map(([, k]) => [k, [0, 0, 0]])) as FinData
}

// A zero denominator yields 0 (not NaN/Infinity), matching the server so the vendor sees the same number.
const div = (n: number, d: number) => (d === 0 ? 0 : n / d)

/** One year's Altman Z′ — prototype altmanZ(d, i). */
export function altmanZ(d: FinData, i: number): number {
  const CA = d.currentAssets[i], CL = d.currentLiab[i], TA = d.totalAssets[i], TL = d.totalLiab[i]
  const EQ = d.equity[i], REV = d.revenue[i], EBIT = d.ebit[i], RE = d.retainedEarnings[i]
  const X1 = div(CA - CL, TA), X2 = div(RE, TA), X3 = div(EBIT, TA), X4 = div(EQ, TL), X5 = div(REV, TA)
  return 0.717 * X1 + 0.847 * X2 + 3.107 * X3 + 0.420 * X4 + 0.998 * X5
}

/** Weighted Z′ across the 3 years — prototype weighted([...]). */
export function weighted(zs: number[]): number {
  return zs.reduce((s, v, i) => s + v * FIN_W[i], 0)
}

/** Band from a (weighted) Z′ — prototype bandFor. */
export function bandFor(z: number): Band {
  if (z >= 2.9) return { band: 'A', risk: 'Low', zone: 'Safe zone', cls: 'b-green' }
  if (z >= 2.0) return { band: 'B', risk: 'Low–Medium', zone: 'Safe / grey', cls: 'b-teal' }
  if (z >= 1.23) return { band: 'C', risk: 'Medium', zone: 'Grey zone', cls: 'b-amber' }
  return { band: 'D', risk: 'High', zone: 'Distress zone', cls: 'b-red' }
}

/** Score /100 — prototype scoreFor: clamp(round(z/4.5·100), 5, 99). */
export function scoreFor(z: number): number {
  if (!Number.isFinite(z)) return 5
  return Math.max(5, Math.min(99, Math.round((z / 4.5) * 100)))
}

/** Full model output for a data set — prototype computeFin. */
export function computeFin(d: FinData): { zs: number[]; zW: number; bd: Band; score: number } {
  const zs = [0, 1, 2].map((i) => altmanZ(d, i))
  const zW = weighted(zs)
  return { zs, zW, bd: bandFor(zW), score: scoreFor(zW) }
}

import { describe, it, expect } from 'vitest'
import { altmanZ, weighted, bandFor, scoreFor, computeFin, type FinData } from './altmanZ'

// Prototype seed vectors (vendor-onboarding-mockup.html FIN_C / FIN_D).
const FIN_C: FinData = {
  revenue: [18000, 17000, 16500], netProfit: [600, 400, 350], ebit: [1100, 800, 750],
  totalAssets: [20000, 20500, 21000], currentAssets: [9000, 8800, 8600], inventory: [3500, 3800, 4000],
  currentLiab: [7500, 8200, 8800], totalLiab: [13000, 14000, 14800], equity: [7000, 6500, 6200],
  retainedEarnings: [3000, 2700, 2400], fixedAssets: [8500, 8800, 9000],
}
const FIN_D: FinData = {
  revenue: [9000, 8200, 7500], netProfit: [200, -150, -400], ebit: [450, 100, -200],
  totalAssets: [11000, 10800, 10500], currentAssets: [5000, 4600, 4200], inventory: [2200, 2400, 2500],
  currentLiab: [5500, 6000, 6500], totalLiab: [8500, 9200, 9800], equity: [2500, 1600, 700],
  retainedEarnings: [900, 500, 100], fixedAssets: [5500, 5800, 6000],
}

describe('altmanZ — parity with the prototype financial model', () => {
  it('FIN_C (healthy) → weighted Z 1.258645, band C, score 28', () => {
    expect(altmanZ(FIN_C, 0)).toBeCloseTo(1.476064, 6)
    expect(altmanZ(FIN_C, 1)).toBeCloseTo(1.276400, 6)
    expect(altmanZ(FIN_C, 2)).toBeCloseTo(1.161025, 6)
    const c = computeFin(FIN_C)
    expect(c.zW).toBeCloseTo(1.258645, 6)
    expect(c.bd.band).toBe('C')
    expect(c.score).toBe(28)
  })

  it('FIN_D (distressed) → weighted Z 0.729867, band D, score 16', () => {
    const c = computeFin(FIN_D)
    expect(c.zW).toBeCloseTo(0.729867, 6)
    expect(c.bd.band).toBe('D')
    expect(c.score).toBe(16)
  })

  it('band thresholds A ≥2.9, B ≥2.0, C ≥1.23, else D', () => {
    expect(bandFor(2.9).band).toBe('A')
    expect(bandFor(2.0).band).toBe('B')
    expect(bandFor(1.23).band).toBe('C')
    expect(bandFor(1.229).band).toBe('D')
  })

  it('score clamps to [5, 99]', () => {
    expect(scoreFor(4.5)).toBe(99)
    expect(scoreFor(-1)).toBe(5)
    expect(weighted([1, 1, 1])).toBeCloseTo(1.0, 9)   // 0.2 + 0.3 + 0.5
  })

  it('empty/zero figures produce a FINITE result (band D, score 5), matching the server', () => {
    const zero: FinData = {
      revenue: [0, 0, 0], netProfit: [0, 0, 0], ebit: [0, 0, 0], totalAssets: [0, 0, 0],
      currentAssets: [0, 0, 0], inventory: [0, 0, 0], currentLiab: [0, 0, 0], totalLiab: [0, 0, 0],
      equity: [0, 0, 0], retainedEarnings: [0, 0, 0], fixedAssets: [0, 0, 0],
    }
    const c = computeFin(zero)
    expect(Number.isFinite(c.zW)).toBe(true)
    expect(c.zW).toBe(0)
    expect(c.bd.band).toBe('D')
    expect(c.score).toBe(5)
    expect(scoreFor(NaN)).toBe(5)
    expect(scoreFor(Infinity)).toBe(5)
  })
})

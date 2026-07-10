import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '../../test/utils'
import { AwardsPage } from './AwardScreens'
import * as client from '../../api/client'

const eligibility = (revealed: boolean) => ({
  rfqId: 'r79', code: 'RFQ-2026-0079', title: 'Pumps', envelope: 'Dual', commercialRevealed: revealed, techFinalized: true,
  lines: revealed
    ? [{ lineCode: 'PUMP', description: 'Pump', requiredQty: 4, uom: 'Unit', recommendedVendorId: 'va', options: [{ vendorId: 'va', vendorName: 'Sentausa', unitPrice: 24500, offeredQty: 4 }] }]
    : [{ lineCode: 'PUMP', description: 'Pump', requiredQty: 4, uom: 'Unit', recommendedVendorId: null, options: [] }],
  ranking: revealed ? [{ vendorId: 'va', vendorName: 'Sentausa', technicalScore: 88, priceScore: 100, combined: 91.6, recommended: true }] : [],
})

describe('Award workspace', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getAwardForRfq').mockResolvedValue(null)
  })

  it('shows a sealed message when commercial is not revealed', async () => {
    vi.spyOn(client, 'getAwardEligibility').mockResolvedValue(eligibility(false))
    renderWithProviders(<AwardsPage route="awards/r79" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText(/Commercial envelope is sealed/i)).toBeInTheDocument())
  })

  it('lists only eligible (TechPass) vendors and a Submit-for-approval action', async () => {
    vi.spyOn(client, 'getAwardEligibility').mockResolvedValue(eligibility(true))
    renderWithProviders(<AwardsPage route="awards/r79" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('Award comparison')).toBeInTheDocument())
    expect(screen.getByText('Recommended')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Submit for approval/i })).toBeInTheDocument()
  })
})

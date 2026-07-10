import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '../../test/utils'
import { EvalPage } from './EvalScreens'
import * as client from '../../api/client'

const evalDto = (masked: boolean) => ({
  rfqId: 'r79', code: 'RFQ-2026-0079', title: 'Pumps', finalized: true, threshold: 70, masked,
  criteria: [{ key: 'compliance', label: 'Compliance', weight: 35 }],
  evaluators: [{ code: 'u_hafiz', name: 'Hafiz' }],
  vendors: [
    { vendorId: 'va', displayName: masked ? 'Bidder A' : 'Sentausa', scores: [{ evaluatorId: 'u_hafiz', criterion: 'compliance', score: 88 }], committee: 88, pass: true },
    { vendorId: 'vc', displayName: masked ? 'Bidder C' : 'MegaTech', scores: [{ evaluatorId: 'u_hafiz', criterion: 'compliance', score: 58 }], committee: 58, pass: false },
  ],
})

describe('TechnicalEval matrix', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
  })

  it('shows real vendor names + pass/fail for a buyer (unmasked)', async () => {
    vi.spyOn(client, 'getTechnicalEval').mockResolvedValue(evalDto(false))
    renderWithProviders(<EvalPage route="openings/r79/score" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('Sentausa')).toBeInTheDocument())
    expect(screen.getByText('MegaTech')).toBeInTheDocument()
    expect(screen.getByText('Fail')).toBeInTheDocument()
    expect(screen.queryByText(/Evaluator view/)).not.toBeInTheDocument()
  })

  it('masks identities (Bidder A/C) and shows the evaluator-view banner when masked', async () => {
    vi.spyOn(client, 'getTechnicalEval').mockResolvedValue(evalDto(true))
    renderWithProviders(<EvalPage route="openings/r79/score" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('Bidder A')).toBeInTheDocument())
    expect(screen.getByText('Bidder C')).toBeInTheDocument()
    expect(screen.queryByText('Sentausa')).not.toBeInTheDocument()
    expect(screen.getByText(/Evaluator view/)).toBeInTheDocument()
  })
})

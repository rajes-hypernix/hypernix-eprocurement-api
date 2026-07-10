import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '../../test/utils'
import { MyRfqs } from './MyRfqs'
import * as client from '../../api/client'

describe('MyRfqs — declined invitation state (Slice K / K3c)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
  })

  it('shows a Declined badge and a Reconsider CTA (not "Action needed"/"Start bid") for a declined invitation', async () => {
    vi.spyOn(client, 'getMyInvitations').mockResolvedValue([
      { rfqId: 'r1', code: 'RFQ-2026-0087', title: 'Piping', status: 'Open', envelope: 'Single', closesUtc: '2027-01-01T00:00:00Z', invitationStatus: 'Declined', bidSubmitted: false, bidDraft: false } as never,
    ])
    renderWithProviders(<MyRfqs onOpen={() => {}} />)
    await waitFor(() => expect(screen.getByText('RFQ-2026-0087')).toBeInTheDocument())
    expect(screen.getByText('Declined')).toBeInTheDocument()
    expect(screen.getByText(/Reconsider/)).toBeInTheDocument()
    // A declined vendor opted out — it must not read as an outstanding action.
    expect(screen.queryByText('Action needed')).not.toBeInTheDocument()
    expect(screen.queryByText(/Start bid/)).not.toBeInTheDocument()
  })

  it('still shows Action needed / Start bid for a live (non-declined) open invitation', async () => {
    vi.spyOn(client, 'getMyInvitations').mockResolvedValue([
      { rfqId: 'r2', code: 'RFQ-2026-0090', title: 'Valves', status: 'Open', envelope: 'Single', closesUtc: '2027-01-01T00:00:00Z', invitationStatus: 'Invited', bidSubmitted: false, bidDraft: false } as never,
    ])
    renderWithProviders(<MyRfqs onOpen={() => {}} />)
    await waitFor(() => expect(screen.getByText('RFQ-2026-0090')).toBeInTheDocument())
    expect(screen.getByText('Action needed')).toBeInTheDocument()
    expect(screen.getByText(/Start bid/)).toBeInTheDocument()
  })
})

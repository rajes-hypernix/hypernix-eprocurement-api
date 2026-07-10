import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { BidForm } from './BidForm'
import * as client from '../../api/client'

const rfqBase = {
  id: 'r1', code: 'RFQ-2026-0087', title: 'Piping', envelope: 'Single', currency: 'MYR',
  opensUtc: null, prRefs: [], invitedVendorIds: [], technicalEvaluatorIds: [], commercialEvaluatorIds: [],
  techFinalized: false, commercialOpened: false,
  lines: [{ itemCode: 'PIP-CS6-SCH40', description: 'Pipe', qty: 120, uom: 'Length', prRef: 'PR-1' }],
  formItems: [{ kind: 'question', group: 'technical', section: '', label: 'Mill certs?', type: 'yesno', required: true, config: '{}', help: '', order: 0 }],
  technicalSections: [], commercialSections: [],
}

describe('BidForm', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getMyBid').mockResolvedValue(null)
  })

  it('allows bidding (enabled Submit) when the RFQ is Open', async () => {
    vi.spyOn(client, 'getRfq').mockResolvedValue({ ...rfqBase, status: 'Open', closesUtc: '2027-01-01T00:00:00Z' })
    renderWithProviders(<BidForm rfqId="r1" onBack={() => {}} />)
    await waitFor(() => expect(screen.getByText('Line pricing')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Submit bid/i })).toBeEnabled()
  })

  it('disables Submit and shows a closed notice when the RFQ is not Open', async () => {
    vi.spyOn(client, 'getRfq').mockResolvedValue({ ...rfqBase, status: 'Awarded', closesUtc: '2026-06-14T00:00:00Z' })
    renderWithProviders(<BidForm rfqId="r1" onBack={() => {}} />)
    await waitFor(() => expect(screen.getByText('Line pricing')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Submit bid/i })).toBeDisabled()
    expect(screen.getByText(/Bidding is closed/i)).toBeInTheDocument()
  })

  it('blocks submit with a price-required message when no line is priced', async () => {
    vi.spyOn(client, 'getRfq').mockResolvedValue({ ...rfqBase, status: 'Open', closesUtc: '2027-01-01T00:00:00Z' })
    const submitSpy = vi.spyOn(client, 'submitBid')
    renderWithProviders(<BidForm rfqId="r1" onBack={() => {}} />)
    await waitFor(() => expect(screen.getByText('Line pricing')).toBeInTheDocument())
    await userEvent.click(screen.getByRole('button', { name: /Submit bid/i }))
    expect(screen.getByText(/Enter a price on at least one line/i)).toBeInTheDocument()
    expect(submitSpy).not.toHaveBeenCalled()
  })

  it('flags a missing required answer before submitting', async () => {
    vi.spyOn(client, 'getRfq').mockResolvedValue({ ...rfqBase, status: 'Open', closesUtc: '2027-01-01T00:00:00Z' })
    const submitSpy = vi.spyOn(client, 'submitBid')
    renderWithProviders(<BidForm rfqId="r1" onBack={() => {}} />)
    await waitFor(() => expect(screen.getByText('Line pricing')).toBeInTheDocument())
    // Price the single line so the price-gate passes and required-answer validation runs.
    await userEvent.type(screen.getByLabelText('Price PIP-CS6-SCH40'), '50')
    await userEvent.click(screen.getByRole('button', { name: /Submit bid/i }))
    expect(screen.getByText(/1 required answer still needed/i)).toBeInTheDocument()
    expect(submitSpy).not.toHaveBeenCalled()
  })
})

describe('BidForm — vendor invitation actions (Slice J)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    window.history.replaceState({}, '', '/?as=VU-x')                 // act as a vendor persona
    vi.spyOn(client, 'getPersonas').mockResolvedValue([{ code: 'VU-x', name: 'X Vendor', kind: 'vendor', vendorId: 'v1', vendorName: 'X', roles: ['Vendor'] } as never])
    vi.spyOn(client, 'getMyBid').mockResolvedValue(null)
    vi.spyOn(client, 'getCustomLists').mockResolvedValue([
      { id: 'l', code: 'RFQ_DECLINE_REASON', name: 'x', description: null, parentListCode: null, isSystem: true,
        values: [{ id: 'c', code: 'CAPACITY', label: 'No capacity in timeframe', parentValueCode: null, sort: 0, active: true }] },
    ])
  })

  it('offers Decline + I-intend-to-bid for an open invitation', async () => {
    vi.spyOn(client, 'getRfq').mockResolvedValue({
      ...rfqBase, status: 'Open', closesUtc: '2027-01-01T00:00:00Z',
      invitations: [{ vendorId: 'v1', vendorName: 'X', status: 'Invited', invitedUtc: '2026-07-01T00:00:00Z' }],
    } as never)
    renderWithProviders(<BidForm rfqId="r1" onBack={() => {}} />)
    await waitFor(() => expect(screen.getByText('Line pricing')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Decline invitation/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /I intend to bid/i })).toBeInTheDocument()
  })

  it('shows the declined banner with a Reconsider action (T4)', async () => {
    const intendSpy = vi.spyOn(client, 'intendRfq').mockResolvedValue(undefined as never)
    vi.spyOn(client, 'getRfq').mockResolvedValue({
      ...rfqBase, status: 'Open', closesUtc: '2027-01-01T00:00:00Z',
      invitations: [{ vendorId: 'v1', vendorName: 'X', status: 'Declined', declineReasonCode: 'CAPACITY', invitedUtc: '2026-07-01T00:00:00Z' }],
    } as never)
    renderWithProviders(<BidForm rfqId="r1" onBack={() => {}} />)
    await waitFor(() => expect(screen.getByText(/You declined this RFQ/i)).toBeInTheDocument())
    expect(screen.getByText(/No capacity in timeframe/)).toBeInTheDocument()   // reason label resolved
    await userEvent.click(screen.getByRole('button', { name: /Reconsider — I intend to bid/i }))
    expect(intendSpy).toHaveBeenCalledWith('r1')
  })
})

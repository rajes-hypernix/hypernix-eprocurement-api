import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { Clarifications } from './Clarifications'
import * as client from '../../api/client'

describe('Clarifications — vendor raises a clarification (Slice K / K2)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    window.history.replaceState({}, '', '/?as=VU-x')                 // act as a vendor persona
    vi.spyOn(client, 'getPersonas').mockResolvedValue([
      { code: 'VU-x', name: 'X Vendor', kind: 'vendor', vendorId: 'v1', vendorName: 'X', roles: ['Vendor'] } as never,
    ])
    vi.spyOn(client, 'getClarificationThreads').mockResolvedValue([])
  })

  it("scopes the RFQ picker to the vendor's invited RFQs and never fetches the full RFQ list", async () => {
    const myInv = vi.spyOn(client, 'getMyInvitations').mockResolvedValue([
      { rfqId: 'r1', code: 'RFQ-2026-0087', title: 'Piping', status: 'Open', envelope: 'Single', closesUtc: '2027-01-01T00:00:00Z' } as never,
    ])
    const allRfqs = vi.spyOn(client, 'getRfqs')
    renderWithProviders(<Clarifications />)

    await userEvent.click(await screen.findByRole('button', { name: /New clarification/i }))

    // The vendor's own invited RFQ is offered as a scope…
    expect(await screen.findByText(/RFQ-2026-0087 — Piping/)).toBeInTheDocument()
    expect(myInv).toHaveBeenCalled()
    // …and the un-scoped, buyer-only RFQ list is never hit (the K2 info-leak fix).
    expect(allRfqs).not.toHaveBeenCalled()
  })
})

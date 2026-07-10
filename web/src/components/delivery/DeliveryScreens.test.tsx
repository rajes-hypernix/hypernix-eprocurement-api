import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { DeliveryPage } from './DeliveryScreens'
import * as client from '../../api/client'

describe('Delivery screens', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([
      { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'], vendorName: null },
    ])
  })

  it('lists ASNs and offers Receive for an in-transit ASN (buyer)', async () => {
    vi.spyOn(client, 'getAsns').mockResolvedValue([
      { id: 'a1', code: 'ASN-2026-0511', poCode: 'PO-2026-1186', vendorName: 'Pantai', carrier: 'Tiong Nam', expectedDate: '29/06/2026', status: 'InTransit', grnCode: null },
    ])
    renderWithProviders(<DeliveryPage route="deliveries" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('ASN-2026-0511')).toBeInTheDocument())
    expect(screen.getByText('In transit')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Receive/i })).toBeInTheDocument()
  })

  it('blocks a duplicate ASN with a "Nothing left to ship" message', async () => {
    vi.spyOn(client, 'getShipPlan').mockResolvedValue({
      poId: 'p1', poCode: 'PO-2026-1190', anyRemaining: false,
      lines: [{ itemCode: 'MRO-LOT-01', description: 'MRO', ordered: 1, received: 0, inTransit: 1, remaining: 0, uom: 'Lot' }],
    })
    renderWithProviders(<DeliveryPage route="deliveries/new/p1" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText(/Nothing left to ship/i)).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Submit ASN/i })).toBeDisabled()
  })

  // K5b — after submit the form shows a confirmation panel instead of silently navigating away.
  it('shows a success panel with the new ASN code after submitting (no immediate navigation)', async () => {
    const onNav = vi.fn()
    vi.spyOn(client, 'getShipPlan').mockResolvedValue({
      poId: 'p1', poCode: 'PO-2026-1190', anyRemaining: true,
      lines: [{ itemCode: 'MRO-LOT-01', description: 'MRO', ordered: 2, received: 0, inTransit: 0, remaining: 2, uom: 'Lot' }],
    })
    const create = vi.spyOn(client, 'createAsn').mockResolvedValue({ id: 'a9', code: 'ASN-2026-0999', poCode: 'PO-2026-1190', status: 'InTransit' } as never)
    renderWithProviders(<DeliveryPage route="deliveries/new/p1" onNavigate={onNav} />)
    await waitFor(() => expect(screen.getByRole('button', { name: /Submit ASN/i })).toBeEnabled())

    await userEvent.click(screen.getByRole('button', { name: /Submit ASN/i }))         // header opens confirm
    const confirmBtns = await screen.findAllByRole('button', { name: /Submit ASN/i })  // modal + header
    await userEvent.click(confirmBtns[confirmBtns.length - 1])
    await waitFor(() => expect(create).toHaveBeenCalled())

    expect(await screen.findByText(/Shipping notice submitted/i)).toBeInTheDocument()
    expect(screen.getByText('ASN-2026-0999')).toBeInTheDocument()
    expect(onNav).not.toHaveBeenCalled()   // stays on the panel until the user clicks Back
  })
})

// K7 — form date defaults must be "today", not a hardcoded literal.
describe('AsnForm — shipped-date default (Slice K / K7)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.useFakeTimers({ shouldAdvanceTime: true })         // keep userEvent / waitFor working
    vi.setSystemTime(new Date('2026-09-15T08:30:00'))     // local mid-morning → 15/09/2026
    vi.spyOn(client, 'getPersonas').mockResolvedValue([
      { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'], vendorName: null },
    ])
  })
  afterEach(() => vi.useRealTimers())

  it("submits a fresh ASN with today's date, not a hardcoded literal", async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    vi.spyOn(client, 'getShipPlan').mockResolvedValue({
      poId: 'p1', poCode: 'PO-2026-1190', anyRemaining: true,
      lines: [{ itemCode: 'MRO-LOT-01', description: 'MRO', ordered: 2, received: 0, inTransit: 0, remaining: 2, uom: 'Lot' }],
    })
    const create = vi.spyOn(client, 'createAsn').mockResolvedValue({ id: 'a9', code: 'ASN-2026-0999', poCode: 'PO-2026-1190', status: 'InTransit' } as never)
    renderWithProviders(<DeliveryPage route="deliveries/new/p1" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByRole('button', { name: /Submit ASN/i })).toBeEnabled())

    await user.click(screen.getByRole('button', { name: /Submit ASN/i }))
    const confirmBtns = await screen.findAllByRole('button', { name: /Submit ASN/i })
    await user.click(confirmBtns[confirmBtns.length - 1])
    await waitFor(() => expect(create).toHaveBeenCalled())

    expect(create.mock.calls[0][1]).toMatchObject({ shippedDate: '15/09/2026' })
    expect(create.mock.calls[0][1].shippedDate).not.toBe('28/06/2026')
  })
})

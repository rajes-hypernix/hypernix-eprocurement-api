import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { Requisitions } from './Requisitions'
import * as client from '../../api/client'

// A submitted PR with one Open (cancellable) line and one InRfq (locked) line.
const PR = {
  id: 'p1', code: 'PR-2026-0412', requestor: 'Aishah Karim', department: 'Facilities',
  location: 'Bintulu Plant', memo: 'Pump set', job: 'JOB-1', category: 'Rotating',
  costCentre: 'CC-1', project: null, raisedDate: '2026-06-02', requiredDate: '2026-07-30',
  status: 'Approved', value: 46000, headerStatus: 'PartiallySourced', submitted: true,
  lines: [
    { id: 'l1', itemCode: 'PUMP-1', description: 'Pump', qty: 4, uom: 'Unit', estUnitPrice: 10000, status: 'available', ref: null, lifecycleStatus: 'Open', noQuotes: false, editable: true },
    { id: 'l2', itemCode: 'VFD-1', description: 'VFD', qty: 4, uom: 'Unit', estUnitPrice: 1500, status: 'rfq', ref: 'RFQ-2026-0079', lifecycleStatus: 'InRfq', noQuotes: false, editable: false },
  ],
}

describe('PR management (Slice B)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([PR] as never)
  })

  it('shows PR-management chrome: Create PR, Board toggle, header badge, Open action', async () => {
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0412')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Create PR/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Board/i })).toBeInTheDocument()
    expect(screen.getByText('Partially sourced')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Open/i })).toBeInTheDocument()
  })

  it('per-line Cancel: Open line is cancellable; InRfq line is locked; confirming calls the API', async () => {
    const cancel = vi.spyOn(client, 'cancelPrLine').mockResolvedValue(PR as never)
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0412')).toBeInTheDocument())

    await userEvent.click(screen.getByText('PR-2026-0412'))            // expand the PR
    expect(screen.getByText(/Locked to RFQ-2026-0079/)).toBeInTheDocument()  // InRfq line locked

    await userEvent.click(screen.getByRole('button', { name: 'Cancel line' }))   // Open-line action
    const dialog = screen.getByRole('dialog')
    await userEvent.type(within(dialog).getByPlaceholderText(/Duplicate/), 'no longer required')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel line' }))

    await waitFor(() => expect(cancel).toHaveBeenCalledWith('p1', 'l1', 'no longer required'))
  })

  it('Create PR opens the create form', async () => {
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0412')).toBeInTheDocument())
    await userEvent.click(screen.getByRole('button', { name: /Create PR/i }))
    expect(screen.getByRole('heading', { name: 'Create Purchase Requisition' })).toBeInTheDocument()
    // Actions render at top + bottom of the form (item 2).
    expect(screen.getAllByRole('button', { name: 'Save draft' })).toHaveLength(2)
    expect(screen.getAllByRole('button', { name: /Submit PR/i })).toHaveLength(2)
  })
})

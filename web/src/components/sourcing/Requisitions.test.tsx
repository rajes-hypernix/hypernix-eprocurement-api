import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { Requisitions } from './Requisitions'
import * as client from '../../api/client'

const ln = (itemCode: string, description: string, qty: number, estUnitPrice: number) => ({
  id: `l-${itemCode}`, itemCode, description, qty, uom: 'Unit', estUnitPrice,
  status: 'available', ref: null, lifecycleStatus: 'Open', noQuotes: false, editable: true,
})
const PR = {
  id: 'p1', code: 'PR-2026-0412', requestor: 'Aishah Karim', department: 'Facilities',
  location: 'Bintulu Plant', memo: '', job: 'JOB-CWS-014', category: 'Rotating Equipment',
  costCentre: 'CC-MAINT-01', project: 'TA-2026', raisedDate: '02/06/2026',
  requiredDate: '30/07/2026', status: 'Approved', value: 184000,
  headerStatus: 'Submitted', submitted: true,
  lines: [ln('MEP-PUMP-075', 'Pump', 4, 38500), ln('ELE-VFD-075', 'VFD', 4, 7500)],
}

describe('Requisitions', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([PR])
  })

  it('renders filter controls (chips + Show dropdown) and the action bar', async () => {
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0412')).toBeInTheDocument())
    // "Requestor"/"Category" appear both as a filter label and a column header.
    expect(screen.getAllByText('Requestor').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText(/With available lines/)).toBeInTheDocument()
    expect(screen.getByText(/Tick the PRs you want to source/)).toBeInTheDocument()
  })

  it('group a PR → Confirm lines opens the drawer with selectable lines and Create RFQ', async () => {
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0412')).toBeInTheDocument())

    const confirm = screen.getByRole('button', { name: /Confirm lines/i })
    expect(confirm).toBeDisabled()

    await userEvent.click(screen.getByLabelText('Group PR-2026-0412'))
    expect(screen.getByText(/PR grouped/)).toBeInTheDocument()
    expect(confirm).toBeEnabled()

    await userEvent.click(confirm)
    expect(screen.getByText('Select lines for RFQ')).toBeInTheDocument()
    expect(screen.getByLabelText('Select MEP-PUMP-075')).toBeChecked()
    expect(screen.getByRole('button', { name: /Create RFQ/i })).toBeEnabled()
  })

  it('Status filter narrows the list by PR header status', async () => {
    const draft = { ...PR, id: 'pd', code: 'PR-2026-0434', headerStatus: 'Draft', submitted: false, status: 'Draft', lines: [ln('CBL-1002', 'Cable', 300, 62)] }
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([PR, draft] as never)
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0434')).toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: 'Filter by Status' }))
    await userEvent.click(screen.getByLabelText('Submitted'))          // friendly label shown for the token

    expect(screen.getByText('PR-2026-0412')).toBeInTheDocument()       // Submitted PR stays
    expect(screen.queryByText('PR-2026-0434')).not.toBeInTheDocument() // Draft PR filtered out
  })

  it('Bug 2 — a Draft PR cannot be grouped/sourced from Confirm lines', async () => {
    const draft = { ...PR, id: 'pd', code: 'PR-2026-0434', headerStatus: 'Draft', submitted: false, status: 'Draft', lines: [ln('CBL-1002', 'Cable', 300, 62)] }
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([draft] as never)
    renderWithProviders(<Requisitions onOpenRfq={() => {}} />)
    await waitFor(() => expect(screen.getByText('PR-2026-0434')).toBeInTheDocument())
    // its group checkbox is disabled (no source-eligible lines while Draft)
    expect(screen.getByLabelText('Group PR-2026-0434')).toBeDisabled()
  })
})

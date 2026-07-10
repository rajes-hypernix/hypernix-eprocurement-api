import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '../../test/utils'
import { StatementPage } from './StatementScreens'
import * as client from '../../api/client'

describe('Statements', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([
      { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'], vendorName: null },
    ])
  })

  it('lists vendor statements with invoiced/paid/balance/GRNI', async () => {
    vi.spyOn(client, 'getStatements').mockResolvedValue([
      { vendorId: 'v1', vendorName: 'Sentausa', invoiced: 288144, paid: 288144, balance: 0, grni: 0 },
      { vendorId: 'v2', vendorName: 'Pantai', invoiced: 0, paid: 0, balance: 0, grni: 20580 },
    ])
    renderWithProviders(<StatementPage route="statements" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('Sentausa')).toBeInTheDocument())
    expect(screen.getByText('Pantai')).toBeInTheDocument()
    // GRNI 20,580 shows both in the total card and Pantai's row.
    expect(screen.getAllByText('RM 20,580.00').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('GRNI accrual')).toBeInTheDocument() // the summary stat card label
  })

  it('renders a vendor ledger detail with aging + running balance', async () => {
    vi.spyOn(client, 'getStatement').mockResolvedValue({
      vendorId: 'v1', vendorName: 'Sentausa', invoiced: 288144, paid: 288144, balance: 0, grni: 0,
      aging: { current: 0, d30: 0, d60: 0, d90: 0 },
      ledger: [
        { date: '14/06/2026', reference: 'INV-2026-0091', type: 'Invoice', memo: null, debit: 0, credit: 288144, balance: 288144 },
        { date: '14/06/2026', reference: 'INV-2026-0091', type: 'Payment', memo: null, debit: 288144, credit: 0, balance: 0 },
      ],
    })
    renderWithProviders(<StatementPage route="statements/v1" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('Ledger')).toBeInTheDocument())
    expect(screen.getByText('Aged payables')).toBeInTheDocument()
    expect(screen.getAllByText('INV-2026-0091').length).toBe(2)
  })
})

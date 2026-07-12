import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, mockSystemView, mockViewRun } from '../../test/utils'
import { InvoicePage } from './InvoiceScreens'
import * as client from '../../api/client'

describe('Invoice screens', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([
      { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'], vendorName: null },
    ])
    vi.spyOn(client, 'getViews').mockResolvedValue([mockSystemView('Invoice') as never])
    vi.spyOn(client, 'runView').mockResolvedValue(mockViewRun('Invoice', [{ Id: 'i1', Code: 'INV-2026-0094', InvoiceNo: 'MEG-7720', PoCode: 'PO-2026-1193', Total: 150336, MatchStatus: 'Variance', Status: 'Exception' }]) as never)
  })

  it('lists invoices with match + status', async () => {
    vi.spyOn(client, 'getInvoices').mockResolvedValue([
      { id: 'i1', code: 'INV-2026-0094', poCode: 'PO-2026-1193', vendorName: 'MegaTech', invoiceNo: 'MEG-7720', status: 'Exception', matchStatus: 'Variance', total: 150336, payable: false },
    ])
    renderWithProviders(<InvoicePage route="invoices" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('INV-2026-0094')).toBeInTheDocument())
    expect(screen.getByText('Exception')).toBeInTheDocument()
    expect(screen.getByText('Variance')).toBeInTheDocument()
  })

  it('shows Resolve (not Approve) for an Exception invoice and per-line match', async () => {
    vi.spyOn(client, 'getInvoice').mockResolvedValue({
      id: 'i1', code: 'INV-2026-0094', poId: 'p', poCode: 'PO-2026-1193', vendorName: 'MegaTech', invoiceNo: 'MEG-7720', date: '2026-06-24',
      status: 'Exception', matchStatus: 'Variance', exceptionReason: 'Unit price billed above PO.', nsId: null, whtRate: 0,
      subtotal: 139200, sst: 11136, wht: 0, total: 150336, payable: false,
      lines: [{ itemCode: 'ELE-MTR-200', description: 'Motor', qty: 6, receivedQty: 6, unitPrice: 23200, poUnitPrice: 22500, qtyOk: true, priceOk: false, lineTotal: 139200 }],
    })
    renderWithProviders(<InvoicePage route="invoices/i1" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText(/3-way match/i)).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Resolve exception/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Approve for payment/i })).not.toBeInTheDocument()
    expect(screen.getByText('Variance')).toBeInTheDocument()
  })

  // K5b — after submit the form shows a confirmation panel instead of silently navigating away.
  it('shows a success panel with the new invoice code after submitting', async () => {
    const onNav = vi.fn()
    vi.spyOn(client, 'getBillablePlan').mockResolvedValue({
      poId: 'p1', poCode: 'PO-2026-1193',
      lines: [{ itemCode: 'ELE-MTR-200', description: 'Motor', receivedQty: 6, alreadyInvoiced: 0, billable: 6, poUnitPrice: 22500, uom: 'Unit' }],
    })
    const submit = vi.spyOn(client, 'submitInvoice').mockResolvedValue({ id: 'i9', code: 'INV-2026-0999', poCode: 'PO-2026-1193' } as never)
    renderWithProviders(<InvoicePage route="invoices/new/p1" onNavigate={onNav} />)
    await waitFor(() => expect(screen.getByRole('button', { name: /Submit invoice/i })).toBeEnabled())

    await userEvent.click(screen.getByRole('button', { name: /Submit invoice/i }))   // header opens confirm
    await userEvent.click(await screen.findByRole('button', { name: 'Submit' }))      // modal confirm
    await waitFor(() => expect(submit).toHaveBeenCalled())

    expect(await screen.findByText(/Invoice submitted/i)).toBeInTheDocument()
    expect(screen.getByText('INV-2026-0999')).toBeInTheDocument()
    expect(onNav).not.toHaveBeenCalled()
  })
})

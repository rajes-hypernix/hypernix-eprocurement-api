import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders, mockSystemView, mockViewRun } from '../../test/utils'
import { PoPage } from './PoScreens'
import * as client from '../../api/client'

describe('PO screens', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getViews').mockResolvedValue([mockSystemView('PurchaseOrder') as never])
    vi.spyOn(client, 'runView').mockResolvedValue(mockViewRun('PurchaseOrder', [{ Id: 'p1', Code: 'PO-2026-1186', VendorName: 'Pantai', RfqCode: null, Status: 'PartiallyReceived', Total: 23520, ReceivedQty: 16, TotalQty: 24 }]) as never)
  })

  it('lists POs with status and receipt progress', async () => {
    vi.spyOn(client, 'getPos').mockResolvedValue([
      { id: 'p1', code: 'PO-2026-1186', vendorId: 'v', vendorName: 'Pantai', rfqCode: null, status: 'PartiallyReceived', total: 23520, receivedQty: 16, totalQty: 24, acknowledged: true, nsId: 'NS-PO-44813' },
    ])
    renderWithProviders(<PoPage route="pos" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('PO-2026-1186')).toBeInTheDocument())
    expect(screen.getByText('Partially received')).toBeInTheDocument()
    expect(screen.getByText('16/24 received')).toBeInTheDocument()
  })

  it('shows an Issue button for a Draft PO (buyer)', async () => {
    vi.spyOn(client, 'getPo').mockResolvedValue({
      id: 'p1', code: 'PO-2026-0074', vendorId: 'v', vendorName: 'Mutiara', rfqCode: 'RFQ-2026-0074', awardCode: 'AWD-2026-0074',
      status: 'Draft', currency: 'MYR', incoterm: 'DDP Bintulu', nsId: null, acknowledged: false, total: 59550,
      lines: [{ itemCode: 'HSE-HARN-2L', description: 'Harness', qty: 60, uom: 'Set', unitPrice: 230, receivedQty: 0, invoicedQty: 0, lineTotal: 13800 }],
    })
    renderWithProviders(<PoPage route="pos/p1" onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('PO Lines')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /Issue PO/i })).toBeInTheDocument()
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { SavedViewListPortlet } from './SavedViewListPortlet'
import { AddReminderModal } from './AddReminderModal'
import * as client from '../../api/client'

beforeEach(() => {
  vi.restoreAllMocks()
  window.history.replaceState(null, '', '?as=u_faridah')
  vi.spyOn(client, 'getPersonas').mockResolvedValue([])
})

describe('SavedViewList "View all →" (D7.5 task 4 — the NetSuite pattern)', () => {
  const portlet = {
    id: 'p1', portletType: 'SavedViewList', title: 'Recent POs', col: 0, row: 0, width: 2,
    savedViewId: 'v9', configJson: JSON.stringify({ topN: 3 }),
  }

  it('requests top-N server-side and, when the view holds more, navigates to the paged list with the view selected', async () => {
    const run = vi.spyOn(client, 'runView').mockResolvedValue({
      viewId: 'v9', name: 'Recent POs', recordType: 'PurchaseOrder',
      columns: [{ fieldKey: 'Code', label: 'PO', dataType: 'Code' }],
      rows: [{ Id: '1', Code: 'PO-1' }, { Id: '2', Code: 'PO-2' }, { Id: '3', Code: 'PO-3' }],
      page: 1, size: 3, total: 9,
    })
    const nav = vi.fn()
    renderWithProviders(<SavedViewListPortlet portlet={portlet} onNavigate={nav} />)
    const viewAll = await screen.findByTestId('view-all')
    expect(run).toHaveBeenCalledWith('v9', 1, 3)                      // the over-fetch died
    expect(viewAll).toHaveTextContent('View all (9)')
    await userEvent.click(viewAll)
    expect(nav).toHaveBeenCalledWith('pos/view/v9')                   // same rows, now paged
  })

  it('shows no View-all when everything already fits', async () => {
    vi.spyOn(client, 'runView').mockResolvedValue({
      viewId: 'v9', name: 'Recent POs', recordType: 'PurchaseOrder',
      columns: [{ fieldKey: 'Code', label: 'PO', dataType: 'Code' }],
      rows: [{ Id: '1', Code: 'PO-1' }], page: 1, size: 3, total: 1,
    })
    renderWithProviders(<SavedViewListPortlet portlet={portlet} onNavigate={() => {}} />)
    await screen.findByText('PO-1')
    expect(screen.queryByTestId('view-all')).not.toBeInTheDocument()
  })
})

describe('Add reminder from any view (D7.5 task 5 — the operator’s journey)', () => {
  it('picks record type + view and emits the existing RemindersConfig item shape', async () => {
    vi.spyOn(client, 'getViews').mockResolvedValue([
      { id: 'v5', code: 'VIEW-9', name: 'PRs pending approval', recordType: 'Requisition', ownerUserId: 'u_faridah', isShared: false, isSystem: false, filters: [], columns: [] },
    ])
    const add = vi.fn()
    renderWithProviders(<AddReminderModal onClose={() => {}} onAdd={add} />)
    await screen.findByRole('option', { name: 'PRs pending approval' })
    await userEvent.selectOptions(screen.getByLabelText('Saved view'), 'v5')
    await userEvent.click(screen.getByRole('button', { name: 'Add reminder' }))
    await waitFor(() => expect(add).toHaveBeenCalledWith({
      savedViewId: 'v5', label: 'PRs pending approval', route: 'reqs',
    }))
  })
})

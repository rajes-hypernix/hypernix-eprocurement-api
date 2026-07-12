import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { SavedViewsHome } from './SavedViewsHome'
import * as client from '../../api/client'

const view = (over: Partial<client.SavedViewDto>): client.SavedViewDto => ({
  id: 'v1', code: 'VIEW-0001', name: 'A view', recordType: 'Rfq', ownerUserId: 'u_faridah',
  isShared: false, isSystem: false, filters: [], columns: [], ...over,
})

beforeEach(() => {
  vi.restoreAllMocks()
  window.history.replaceState(null, '', '?as=u_faridah')
  vi.spyOn(client, 'getPersonas').mockResolvedValue([
    { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'] } as never,
  ])
  vi.spyOn(client, 'getPermissions').mockResolvedValue(['UseSavedViews', 'ManageOwnSavedViews'])
})

describe('SavedViewsHome — the builder’s front door (D7.5 task 1)', () => {
  it('lists mine/shared/system across record types with the existing ownership rules', async () => {
    vi.spyOn(client, 'getViews').mockResolvedValue([
      view({ id: 's1', code: 'VIEW-SYS-0001', name: 'All RFQs', isSystem: true, ownerUserId: null }),
      view({ id: 'm1', code: 'VIEW-0002', name: 'My POs', recordType: 'PurchaseOrder' }),
      view({ id: 'o1', code: 'VIEW-0003', name: 'Lim’s shared', ownerUserId: 'u_lim', isShared: true }),
    ])
    renderWithProviders(<SavedViewsHome onOpenList={() => {}} />)
    await screen.findByText('All RFQs')
    expect(screen.getByText('System')).toBeInTheDocument()
    expect(screen.getByText('Mine')).toBeInTheDocument()
    expect(screen.getByText(/Shared/)).toBeInTheDocument()
    // Edit/Delete only on MY view — never on system or someone else's.
    expect(screen.getByRole('button', { name: 'Edit My POs' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete My POs' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit All RFQs' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete Lim’s shared' })).not.toBeInTheDocument()
  })

  it('New view: pick ANY record type, the existing builder opens for it — no list screen involved', async () => {
    vi.spyOn(client, 'getViews').mockResolvedValue([])
    vi.spyOn(client, 'getViewFields').mockResolvedValue([
      { fieldKey: 'Code', label: 'PR #', dataType: 'Code', kind: 'Native', options: null },
      { fieldKey: 'Department', label: 'Department', dataType: 'Text', kind: 'Native', options: null },
    ])
    renderWithProviders(<SavedViewsHome onOpenList={() => {}} />)
    await userEvent.click(await screen.findByRole('button', { name: /New view/ }))
    await userEvent.selectOptions(screen.getByLabelText('Record type'), 'Requisition')
    await userEvent.click(screen.getByRole('button', { name: 'Choose fields…' }))
    expect(await screen.findByText('New saved view')).toBeInTheDocument()   // the D3 builder, reused
    expect(screen.getByLabelText('View name')).toBeInTheDocument()
  })

  it('Open navigates to the record type’s list with the view selected', async () => {
    vi.spyOn(client, 'getViews').mockResolvedValue([view({ id: 'm1', name: 'My POs', recordType: 'PurchaseOrder' })])
    const open = vi.fn()
    renderWithProviders(<SavedViewsHome onOpenList={open} />)
    await userEvent.click(await screen.findByRole('button', { name: 'Open list with My POs' }))
    await waitFor(() => expect(open).toHaveBeenCalledWith('PurchaseOrder', 'm1'))
  })
})

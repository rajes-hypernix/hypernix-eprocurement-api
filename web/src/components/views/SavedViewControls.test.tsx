import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { ViewPicker, ViewBuilder } from './SavedViewControls'
import * as client from '../../api/client'
import type { SavedViewDto } from '../../api/client'

const VIEWS: SavedViewDto[] = [
  { id: 'sys1', code: 'VIEW-SYS-0001', name: 'All RFQs', recordType: 'Rfq', ownerUserId: null, isSystem: true, isShared: true, filters: [], columns: [] },
  { id: 'sh1', code: 'VIEW-2026-0002', name: 'Closing soon', recordType: 'Rfq', ownerUserId: 'u_lim', isSystem: false, isShared: true, filters: [], columns: [] },
  { id: 'my1', code: 'VIEW-2026-0003', name: 'My drafts', recordType: 'Rfq', ownerUserId: 'u_faridah', isSystem: false, isShared: false, filters: [], columns: [] },
]

const FIELDS: client.ViewFieldDto[] = [
  { fieldKey: 'Status', label: 'Status', dataType: 'Enum', options: ['Draft', 'Open', 'Closed'] },
  { fieldKey: 'ClosesUtc', label: 'Closes', dataType: 'Instant', options: null },
  { fieldKey: 'Code', label: 'RFQ', dataType: 'Code', options: null },
]

const asBuyer = (permissions: string[]) => {
  vi.spyOn(client, 'getPersonas').mockResolvedValue([
    { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'] } as never,
  ])
  vi.spyOn(client, 'getPermissions').mockResolvedValue(permissions)
  vi.spyOn(client, 'getViewFields').mockResolvedValue(FIELDS)
}

beforeEach(() => {
  vi.restoreAllMocks()
  window.history.replaceState(null, '', '?as=u_faridah')
})

describe('ViewPicker', () => {
  it('groups views into System / Shared / My views and offers Edit only on own views', async () => {
    asBuyer(['UseSavedViews', 'ManageOwnSavedViews'])
    const onSelect = vi.fn()
    renderWithProviders(
      <ViewPicker views={VIEWS} selectedId="my1" onSelect={onSelect} onNew={() => {}} onEdit={() => {}} />,
    )
    const select = screen.getByLabelText('Saved view')
    expect(select).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'System' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Shared' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'My views' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit this view' })).toBeInTheDocument())
    await userEvent.selectOptions(select, 'sh1')
    expect(onSelect).toHaveBeenCalledWith('sh1')
  })

  it('offers no Edit on a system view', () => {
    asBuyer(['UseSavedViews'])
    renderWithProviders(
      <ViewPicker views={VIEWS} selectedId="sys1" onSelect={() => {}} onNew={() => {}} onEdit={() => {}} />,
    )
    expect(screen.queryByRole('button', { name: 'Edit this view' })).not.toBeInTheDocument()
  })
})

describe('ViewBuilder', () => {
  it('builds and saves the gate criterion — Status is Open AND ClosesUtc between month tokens', async () => {
    asBuyer(['UseSavedViews', 'ManageOwnSavedViews'])
    const created: SavedViewDto = { ...VIEWS[2], id: 'new1', name: 'Open RFQs closing this month' }
    const create = vi.spyOn(client, 'createView').mockResolvedValue(created)
    const onSaved = vi.fn()

    renderWithProviders(
      <ViewBuilder recordType="Rfq" existing={null} defaultColumns={['Code', 'Status']} onClose={() => {}} onSaved={onSaved} />,
    )
    await userEvent.type(screen.getByLabelText('View name'), 'Open RFQs closing this month')
    await userEvent.click(await screen.findByRole('button', { name: 'Add criterion' }))
    await userEvent.selectOptions(screen.getByLabelText('Operator'), 'Eq')
    await userEvent.selectOptions(await screen.findByLabelText('Status'), 'Open')

    await userEvent.click(screen.getByRole('button', { name: 'Add criterion' }))
    const fieldSelects = screen.getAllByLabelText('Field')
    await userEvent.selectOptions(fieldSelects[1], 'ClosesUtc')
    const opSelects = screen.getAllByLabelText('Operator')
    await userEvent.selectOptions(opSelects[1], 'Between')
    const tokenSelects = screen.getAllByLabelText('Value')
    await userEvent.selectOptions(tokenSelects[0], '@startOfMonth')
    await userEvent.selectOptions(tokenSelects[1], '@endOfMonth')

    await userEvent.click(screen.getByRole('button', { name: 'Save view' }))
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(created))
    expect(create).toHaveBeenCalledWith({
      name: 'Open RFQs closing this month',
      recordType: 'Rfq',
      filters: [
        { fieldKey: 'Status', operator: 'Eq', value: 'Open', value2: null },
        { fieldKey: 'ClosesUtc', operator: 'Between', value: '@startOfMonth', value2: '@endOfMonth' },
      ],
      columns: [{ fieldKey: 'Code' }, { fieldKey: 'Status' }],
    })
  })

  it('shows the Shared toggle only to holders of ManageSharedViews', async () => {
    asBuyer(['UseSavedViews', 'ManageOwnSavedViews'])
    const { unmount } = renderWithProviders(
      <ViewBuilder recordType="Rfq" existing={null} defaultColumns={['Code']} onClose={() => {}} onSaved={() => {}} />,
    )
    await screen.findByRole('button', { name: 'Add criterion' })
    expect(screen.queryByText(/Shared \(visible to everyone/)).not.toBeInTheDocument()
    unmount()

    asBuyer(['UseSavedViews', 'ManageOwnSavedViews', 'ManageSharedViews'])
    renderWithProviders(
      <ViewBuilder recordType="Rfq" existing={null} defaultColumns={['Code']} onClose={() => {}} onSaved={() => {}} />,
    )
    expect(await screen.findByText(/Shared \(visible to everyone/)).toBeInTheDocument()
  })
})

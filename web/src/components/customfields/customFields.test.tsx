import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { CustomFieldsSection } from './CustomFieldsSection'
import { AddKpiModal } from '../portlets/AddKpiModal'
import * as client from '../../api/client'

const identity = (permissions: string[]) => {
  vi.spyOn(client, 'getPersonas').mockResolvedValue([
    { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'] } as never,
  ])
  vi.spyOn(client, 'getPermissions').mockResolvedValue(permissions)
}

beforeEach(() => {
  vi.restoreAllMocks()
  window.history.replaceState(null, '', '?as=u_faridah')
})

describe('CustomFieldsSection — the identical FieldSpec pipeline', () => {
  it('renders defs through the D1 primitives and saves via the value endpoint', async () => {
    identity(['ReadCustomValues', 'EditCustomValues'])
    vi.spyOn(client, 'getCustomValues').mockResolvedValue([
      { code: 'cf_warranty_expiry', label: 'Warranty Expiry', dataType: 'Date', required: false, helpText: '', value: null },
      { code: 'cf_site_induction', label: 'Site Induction', dataType: 'Bool', required: false, helpText: '', value: 'true' },
    ])
    const save = vi.spyOn(client, 'saveCustomValues').mockResolvedValue([])

    renderWithProviders(<CustomFieldsSection recordType="PurchaseOrder" recordId="p1" />)
    const dateInput = await screen.findByLabelText('Warranty Expiry')
    expect(dateInput).toHaveAttribute('type', 'date')       // the D1 DateField — no second path
    await userEvent.type(dateInput, '2026-09-01')
    await userEvent.click(screen.getByRole('button', { name: 'Save custom fields' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith('PurchaseOrder', 'p1',
      expect.objectContaining({ cf_warranty_expiry: '2026-09-01', cf_site_induction: 'true' })))
  })

  it('renders read-only (no Save) without EditCustomValues, and nothing when no defs exist', async () => {
    identity(['ReadCustomValues'])
    vi.spyOn(client, 'getCustomValues').mockResolvedValue([
      { code: 'cf_x', label: 'X', dataType: 'Text', required: false, helpText: '', value: 'v' },
    ])
    const { unmount } = renderWithProviders(<CustomFieldsSection recordType="PurchaseOrder" recordId="p1" />)
    await screen.findByLabelText('X')
    expect(screen.queryByRole('button', { name: 'Save custom fields' })).not.toBeInTheDocument()
    unmount()

    vi.spyOn(client, 'getCustomValues').mockResolvedValue([])
    renderWithProviders(
      <><CustomFieldsSection recordType="Vendor" recordId="v1" /><span>page</span></>,
    )
    await screen.findByText('page')
    expect(screen.queryByText('Custom fields')).not.toBeInTheDocument()
  })
})

describe('Add-KPI inherits custom fields with ZERO changes (proven, not asserted)', () => {
  it('offers a Custom Money field for sum — straight from the registry palette', async () => {
    identity(['UseSavedViews', 'ManageOwnSavedViews', 'UseDashboards', 'ManageOwnDashboard'])
    vi.spyOn(client, 'getViews').mockResolvedValue([
      { id: 'v1', code: 'VIEW-1', name: 'All POs', recordType: 'PurchaseOrder', ownerUserId: 'u_faridah', isSystem: false, isShared: false, filters: [], columns: [] },
    ])
    vi.spyOn(client, 'getViewFields').mockResolvedValue([
      { fieldKey: 'Total', label: 'Value', dataType: 'Money', kind: 'Native', options: null },
      { fieldKey: 'cf_bank_guarantee', label: 'Bank Guarantee', dataType: 'Money', kind: 'Custom', options: null },
    ])
    renderWithProviders(<AddKpiModal onClose={() => {}} onAdd={() => {}} />)
    await userEvent.selectOptions(await screen.findByLabelText('Record type'), 'PurchaseOrder')
    await userEvent.selectOptions(screen.getByLabelText('Function'), 'sum')
    const fieldSelect = await screen.findByLabelText('Field')
    expect(Array.from(fieldSelect.querySelectorAll('option')).map((o) => o.getAttribute('value')))
      .toContain('cf_bank_guarantee')
  })
})

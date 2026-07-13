import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery, pickSearchable } from '../../test/utils'
import { ManualVendorForm } from './ManualVendorForm'
import * as client from '../../api/client'
import type { CustomList } from '../../api/client'

const val = (code: string, label: string, parent: string | null, sort: number) =>
  ({ id: code, code, label, parentValueCode: parent, sort, active: true })
const LISTS: CustomList[] = [
  { id: 'l1', code: 'COUNTRY', name: 'Country', description: null, parentListCode: null, isSystem: true, values: [val('MY', 'Malaysia', null, 0), val('SG', 'Singapore', null, 1)] },
  { id: 'l2', code: 'STATE', name: 'State', description: null, parentListCode: 'COUNTRY', isSystem: true, values: [val('SGR', 'Selangor', 'MY', 0), val('SWK', 'Sarawak', 'MY', 1)] },
  { id: 'l3', code: 'CITY', name: 'City', description: null, parentListCode: 'STATE', isSystem: true, values: [val('Shah Alam', 'Shah Alam', 'SGR', 0)] },
  { id: 'l4', code: 'CURRENCY', name: 'Currency', description: null, parentListCode: null, isSystem: true, values: [val('MYR', 'Malaysian Ringgit (RM)', null, 0), val('USD', 'US Dollar (USD)', null, 1)] },
  { id: 'l5', code: 'PAYMENT_TERMS', name: 'Payment terms', description: null, parentListCode: null, isSystem: true, values: [val('NET30', '30 days', null, 0), val('NET60', '60 days', null, 1)] },
  { id: 'l6', code: 'BANK', name: 'Bank', description: null, parentListCode: null, isSystem: true, values: [val('MBB', 'Maybank', null, 0), val('CIMB', 'CIMB Bank', null, 1)] },
]

describe('ManualVendorForm (Bug 3)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getCustomLists').mockResolvedValue(LISTS)
    vi.spyOn(client, 'getSwec').mockResolvedValue([])
  })

  it('is fully editable and submits CODED conformed dimensions from the seeded lookups', async () => {
    const create = vi.spyOn(client, 'createManualVendor').mockResolvedValue({ vendor: { id: 'v1' } as never, duplicateWarning: null })
    const onSaved = vi.fn()
    renderWithQuery(<ManualVendorForm onSaved={onSaved} onBack={() => {}} />)

    // free-text field is editable
    const name = await screen.findByLabelText('Company name')
    await userEvent.type(name, 'KL Industrial Supplies')
    expect((name as HTMLInputElement).value).toBe('KL Industrial Supplies')

    // dropdowns are populated from the lookups
    expect(screen.getByRole('button', { name: 'Country' })).toHaveTextContent('Malaysia')   // default Malaysia (searchable select)
    // Bank options live in the searchable popup now — open it to check, then close.
    await userEvent.click(screen.getByRole('button', { name: 'Bank' }))
    expect(screen.getByRole('option', { name: 'Maybank' })).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')

    await pickSearchable('State / Region', 'Selangor')
    await pickSearchable('City', 'Shah Alam')          // dependent list
    await pickSearchable('Currency', 'USD')
    await pickSearchable('Payment terms', '60')
    await pickSearchable('Bank', 'CIMB')

    await userEvent.click(screen.getAllByRole('button', { name: /Add to master/ })[0])
    await waitFor(() => expect(create).toHaveBeenCalled())
    expect(create.mock.calls[0][0]).toMatchObject({
      name: 'KL Industrial Supplies', type: 'Non-SWEC',
      country: 'MY', state: 'SGR', city: 'Shah Alam', currency: 'USD', paymentTerms: 'NET60', bank: 'CIMB',
    })
    expect(onSaved).toHaveBeenCalledWith('v1')
  })

  it('surfaces a duplicate warning without navigating away', async () => {
    vi.spyOn(client, 'createManualVendor').mockResolvedValue({ vendor: { id: 'v1' } as never, duplicateWarning: 'A vendor with this registration number already exists (SWK-V-0001).' })
    const onSaved = vi.fn()
    renderWithQuery(<ManualVendorForm onSaved={onSaved} onBack={() => {}} />)
    await userEvent.type(await screen.findByLabelText('Company name'), 'Dup Co')
    await userEvent.click(screen.getAllByRole('button', { name: /Add to master/ })[0])
    expect(await screen.findByText(/already exists/)).toBeInTheDocument()
    expect(onSaved).not.toHaveBeenCalled()
  })
})

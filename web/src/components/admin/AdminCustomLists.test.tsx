import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { AdminCustomLists } from './AdminCustomLists'
import * as client from '../../api/client'
import type { CustomList } from '../../api/client'

const val = (code: string, label: string, parent: string | null, sort: number) =>
  ({ id: `v-${code}`, code, label, parentValueCode: parent, sort, active: true })
const LISTS: CustomList[] = [
  { id: 'l1', code: 'PAYMENT_TERMS', name: 'Payment terms', description: null, parentListCode: null, isSystem: true, values: [val('NET30', '30 days', null, 0), val('NET60', '60 days', null, 1)] },
  { id: 'l2', code: 'STATE', name: 'State', description: null, parentListCode: 'COUNTRY', isSystem: true, values: [val('SGR', 'Selangor', 'MY', 0)] },
  { id: 'l3', code: 'COUNTRY', name: 'Country', description: null, parentListCode: null, isSystem: true, values: [val('MY', 'Malaysia', null, 0)] },
]

describe('AdminCustomLists (NetSuite-style custom lists)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getCustomLists').mockResolvedValue(LISTS)
  })

  it('lists the value sets and shows the selected list’s values (code + label)', async () => {
    renderWithQuery(<AdminCustomLists />)
    // left rail lists all sets
    expect(await screen.findByRole('button', { name: /Payment terms/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Country/ })).toBeInTheDocument()
    // first list is selected by default → its values render
    expect(screen.getByText('NET30')).toBeInTheDocument()
    expect(screen.getByText('30 days')).toBeInTheDocument()
  })

  it('STAGES values then commits on one Save (CF-FIX2-T5: no auto-save)', async () => {
    const add = vi.spyOn(client, 'addCustomListValue').mockResolvedValue(val('3', '90 days', null, 2))
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.type(screen.getByLabelText('Label'), '90 days')
    await userEvent.click(screen.getByRole('button', { name: /Add value/ }))
    expect(add).not.toHaveBeenCalled()                                  // staged, NOT persisted
    expect(screen.getByText('1 unsaved value')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Save values' }))
    await waitFor(() => expect(add).toHaveBeenCalledWith('PAYMENT_TERMS', { code: null, label: '90 days', parentValueCode: null }))
  })

  it('a dependent list exposes the parent-value picker', async () => {
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.click(screen.getByRole('button', { name: /State/ }))
    // parent value dropdown appears (State depends on Country)
    expect(await screen.findByLabelText('Country')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Malaysia' })).toBeInTheDocument()
  })

  // CF-FIX3-T4: the X never deletes directly — it opens the impact report.
  it('the value X opens the impact report; a live-usage block disables Delete and NAMES the store', async () => {
    vi.spyOn(client, 'getListValueReferences').mockResolvedValue({
      configReferences: [], liveCount: 3, historicalCount: 0, canDelete: false, canPurge: false,
      data: [{ storeName: 'Master data columns', liveCount: 3, historicalCount: 0, byRecordType: [{ recordType: 'Vendor', live: 3, historical: 0 }] }],
      blockedReason: '3 live record(s) hold this value — deactivate it instead.',
    })
    const del = vi.spyOn(client, 'deleteCustomListValue')
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.click(screen.getByRole('button', { name: 'Delete NET30' }))

    expect(await screen.findByText(/3 live record\(s\) hold this value/)).toBeInTheDocument()
    expect(screen.getByText('Master data columns')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete list value' })).toBeDisabled()
    expect(del).not.toHaveBeenCalled()
    // Tier 1 is always on offer
    expect(screen.getByRole('button', { name: 'Deactivate instead' })).toBeEnabled()
  })

  it('an unreferenced value deletes through the dialog', async () => {
    vi.spyOn(client, 'getListValueReferences').mockResolvedValue({
      configReferences: [], data: [], liveCount: 0, historicalCount: 0, canDelete: true, canPurge: false, blockedReason: null,
    })
    const del = vi.spyOn(client, 'deleteCustomListValue').mockResolvedValue(undefined)
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.click(screen.getByRole('button', { name: 'Delete NET60' }))
    const btn = await screen.findByRole('button', { name: 'Delete list value' })
    await waitFor(() => expect(btn).toBeEnabled())
    await userEvent.click(btn)
    await waitFor(() => expect(del).toHaveBeenCalledWith('v-NET60'))
  })

  it('creates a new list', async () => {
    const create = vi.spyOn(client, 'createCustomList').mockResolvedValue({
      id: 'l9', code: 'INCOTERM', name: 'Incoterms', description: null, parentListCode: null, isSystem: false, values: [],
    })
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.click(screen.getByRole('button', { name: /New list/ }))
    await userEvent.type(screen.getByLabelText('Internal ID'), 'incoterm')
    await userEvent.type(screen.getByLabelText('Name'), 'Incoterms')
    await userEvent.click(screen.getByRole('button', { name: /Create list/ }))
    await waitFor(() => expect(create).toHaveBeenCalledWith({ code: 'INCOTERM', name: 'Incoterms', description: null, parentListCode: null, orderMode: 'Entered' }))
  })
})

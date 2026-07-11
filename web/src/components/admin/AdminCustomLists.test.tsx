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

  it('adds a value to the selected list (stores code + label)', async () => {
    const add = vi.spyOn(client, 'addCustomListValue').mockResolvedValue(val('NET90', '90 days', null, 2))
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.type(screen.getByLabelText('Code (stored)'), 'NET90')
    await userEvent.type(screen.getByLabelText('Label (shown)'), '90 days')
    await userEvent.click(screen.getByRole('button', { name: /Add value/ }))
    await waitFor(() => expect(add).toHaveBeenCalledWith('PAYMENT_TERMS', { code: 'NET90', label: '90 days', parentValueCode: null }))
  })

  it('a dependent list exposes the parent-value picker', async () => {
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.click(screen.getByRole('button', { name: /State/ }))
    // parent value dropdown appears (State depends on Country)
    expect(await screen.findByLabelText('Country')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Malaysia' })).toBeInTheDocument()
  })

  it('creates a new list', async () => {
    const create = vi.spyOn(client, 'createCustomList').mockResolvedValue({
      id: 'l9', code: 'INCOTERM', name: 'Incoterms', description: null, parentListCode: null, isSystem: false, values: [],
    })
    renderWithQuery(<AdminCustomLists />)
    await screen.findByRole('button', { name: /Payment terms/ })
    await userEvent.click(screen.getByRole('button', { name: /New list/ }))
    await userEvent.type(screen.getByLabelText('Code'), 'incoterm')
    await userEvent.type(screen.getByLabelText('Name'), 'Incoterms')
    await userEvent.click(screen.getByRole('button', { name: /Create list/ }))
    await waitFor(() => expect(create).toHaveBeenCalledWith({ code: 'INCOTERM', name: 'Incoterms', description: null, parentListCode: null }))
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { AdminEntryForms } from './AdminEntryForms'
import * as client from '../../api/client'
import type { EntryFormDefDto } from '../../api/client'

const FORM: EntryFormDefDto = {
  id: 'f1', code: 'ef_custom_pr', name: 'Buyer PR Form', recordType: 'Requisition', isSystem: false, active: true,
  roles: [],
  subtabs: [],
  groups: [
    { id: 'g-header', subtabId: null, title: 'Header', sort: 0, columnBreak: false, isHeader: true },
    { id: 'g-extra', subtabId: null, title: 'Logistics', sort: 1, columnBreak: false, isHeader: false },
  ],
  fields: [
    { fieldKey: 'Department', subtab: null, fieldGroup: 'Header', sort: 0, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null, groupId: 'g-header' },
  ],
  sublistColumns: ['ItemCode', 'Description', 'Qty', 'Uom', 'EstUnitPrice'],
}

describe('AdminEntryForms — CF-FIX4-T3 designer (groups as cards, L1/L3/L4)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getEntryForms').mockResolvedValue([FORM])
    vi.spyOn(client, 'getViewFields').mockResolvedValue([])
  })

  it('renders fields INSIDE their group cards; Header is pinned (no delete verb), empty non-Header offers delete', async () => {
    renderWithQuery(<AdminEntryForms />)
    const headerCard = await screen.findByLabelText('Field group Header')
    expect(headerCard).toHaveTextContent('Department')
    expect(screen.getByText('Header — always present')).toBeInTheDocument()          // the L3 badge
    expect(screen.queryByRole('button', { name: 'Delete group Header' })).toBeNull() // no knife for Header
    expect(screen.getByRole('button', { name: 'Delete group Logistics' })).toBeInTheDocument()
  })

  it('dragging a field onto another group calls the SURGICAL placement move (L1 — one shared row)', async () => {
    const move = vi.spyOn(client, 'moveEntryFormField').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    const row = await screen.findByLabelText('Field row Department')
    const target = screen.getByLabelText('Field group Logistics')

    const dt = { getData: vi.fn(() => 'Department'), setData: vi.fn() }
    fireEvent.dragStart(row, { dataTransfer: dt })
    fireEvent.drop(target, { dataTransfer: dt })
    await waitFor(() => expect(move).toHaveBeenCalledWith('f1', 'Department', 'g-extra', 1))   // global append
  })

  it('the item sublist is FLAT and rearrangeable — a move persists the new order (L4)', async () => {
    const save = vi.spyOn(client, 'saveEntryFormSublist').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    await screen.findByText('Item sublist')
    await userEvent.click(screen.getByRole('button', { name: 'Move Description left' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith('f1', ['Description', 'ItemCode', 'Qty', 'Uom', 'EstUnitPrice']))
  })

  it('a system form is wholly read-only — no verbs, no drag affordances', async () => {
    vi.spyOn(client, 'getEntryForms').mockResolvedValue([{ ...FORM, isSystem: true }])
    renderWithQuery(<AdminEntryForms />)
    expect(await screen.findByText('Standard — the parity baseline, read-only')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save form' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Add group' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Move Description left' })).toBeNull()
  })
})

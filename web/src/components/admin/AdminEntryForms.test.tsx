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
    { fieldKey: 'custbody_partner', subtab: null, fieldGroup: 'Header', sort: 1, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, label: null, placeholder: null, groupId: 'g-header' },
  ],
  sublistColumns: ['ItemCode', 'Description', 'Qty', 'Uom', 'EstUnitPrice'],
}

describe('AdminEntryForms — CF-FIX4-T3 designer (groups as cards, L1/L3/L4)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getEntryForms').mockResolvedValue([FORM])
    vi.spyOn(client, 'getViewFields').mockResolvedValue([
      { fieldKey: 'Department', label: 'Department', dataType: 'Text', kind: 'Native' },
      { fieldKey: 'custbody_partner', label: 'Partner', dataType: 'Text', kind: 'Custom' },
    ] as client.ViewFieldDto[])
  })

  it('renders fields INSIDE their group cards; Header is pinned (no delete verb), empty non-Header offers delete', async () => {
    renderWithQuery(<AdminEntryForms />)
    const headerCard = await screen.findByLabelText('Field group Header')
    expect(headerCard).toHaveTextContent('Department')
    expect(screen.getByText('Header — always present')).toBeInTheDocument()          // the L3 badge
    expect(screen.queryByRole('button', { name: 'Delete group Header' })).toBeNull() // no knife for Header
    expect(screen.getByRole('button', { name: 'Delete group Logistics' })).toBeInTheDocument()
  })

  it('dragging a field onto another group re-parents THE placement (L1) — staged or persisted alike', async () => {
    const save = vi.spyOn(client, 'updateEntryForm').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    const row = await screen.findByLabelText('Field row Department')
    const target = screen.getByLabelText('Field group Logistics')

    const dt = { getData: vi.fn(() => 'Department'), setData: vi.fn() }
    fireEvent.dragStart(row, { dataTransfer: dt })
    fireEvent.drop(target, { dataTransfer: dt })
    await waitFor(() => expect(save).toHaveBeenCalled())
    const sent = save.mock.calls[0][1].fields.find((f: client.EntryFormFieldDto) => f.fieldKey === 'Department')
    expect(sent.fieldGroup).toBe('Logistics')   // the string seam re-points the SAME EntryFormField row
  })

  // T3-FIX(2)+(3): labels-not-ids; the Default control is gone.
  it('a placed field shows its LABEL (internal id muted secondary); no default-value control exists', async () => {
    renderWithQuery(<AdminEntryForms />)
    const row = await screen.findByLabelText('Field row custbody_partner')
    expect(row).toHaveTextContent('Partner')            // the registry label leads
    expect(row).toHaveTextContent('custbody_partner')   // the id stays visible but secondary
    expect(screen.queryByLabelText('custbody_partner default')).toBeNull()
    expect(screen.queryByText('Default')).toBeNull()
  })

  // T3-FIX(5): the arrows re-group when crossing a boundary.
  it('move-down past the last row of Header re-groups the field into the next group', async () => {
    const save = vi.spyOn(client, 'updateEntryForm').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    await screen.findByLabelText('Field row custbody_partner')
    await userEvent.click(screen.getByRole('button', { name: 'Move custbody_partner down' }))
    await waitFor(() => expect(save).toHaveBeenCalled())
    const sent = save.mock.calls[0][1].fields.find((f: client.EntryFormFieldDto) => f.fieldKey === 'custbody_partner')
    expect(sent.fieldGroup).toBe('Logistics')
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

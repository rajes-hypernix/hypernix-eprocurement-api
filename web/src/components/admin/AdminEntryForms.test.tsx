import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
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
    vi.spyOn(client, 'getLineCustomDefs').mockResolvedValue([])
  })

  it('renders fields INSIDE their group cards; Header is pinned (no delete verb), empty non-Header offers delete', async () => {
    renderWithQuery(<AdminEntryForms />)
    const headerCard = await screen.findByLabelText('Field group Header')
    expect(headerCard).toHaveTextContent('Department')
    expect(screen.getByText('Header — always present')).toBeInTheDocument()          // the L3 badge
    expect(screen.queryByRole('button', { name: 'Delete group Header' })).toBeNull() // no knife for Header
    expect(screen.getByRole('button', { name: 'Delete group Logistics' })).toBeInTheDocument()
  })

  // CF-FIX5-T3: drag is REMOVED — the field rows carry no draggable affordance at all.
  it('field rows are NOT draggable (drag removed; arrows are the sole reorder mechanism)', async () => {
    renderWithQuery(<AdminEntryForms />)
    const row = await screen.findByLabelText('Field row Department')
    expect(row).not.toHaveAttribute('draggable', 'true')
  })

  it('within a group, the UP arrow swaps a field with its neighbour (order changes, group unchanged)', async () => {
    const save = vi.spyOn(client, 'updateEntryForm').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    await screen.findByLabelText('Field row custbody_partner')
    // Header holds Department (0) then Partner (1); move Partner UP → they swap, both stay Header.
    await userEvent.click(screen.getByRole('button', { name: 'Move custbody_partner up' }))
    await waitFor(() => expect(save).toHaveBeenCalled())
    const sent: client.EntryFormFieldDto[] = save.mock.calls[0][1].fields
    const headerFields = sent.filter((f) => f.fieldGroup === 'Header').map((f) => f.fieldKey)
    expect(headerFields).toEqual(['custbody_partner', 'Department'])   // swapped within Header
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

  // CF-FIX5-T3 (operator's explicit ask): arrows handle CROSS-group movement —
  // the BOTTOM field of one group moves DOWN into the next group.
  it('cross-group: move-down at the bottom of Header re-groups the field into the next group', async () => {
    const save = vi.spyOn(client, 'updateEntryForm').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    await screen.findByLabelText('Field row custbody_partner')
    await userEvent.click(screen.getByRole('button', { name: 'Move custbody_partner down' }))
    await waitFor(() => expect(save).toHaveBeenCalled())
    const sent = save.mock.calls[0][1].fields.find((f: client.EntryFormFieldDto) => f.fieldKey === 'custbody_partner')
    expect(sent.fieldGroup).toBe('Logistics')
  })

  it('CF-FIX5-T4: the sublist is a VERTICAL list (top = leftmost); up/down persists the new order', async () => {
    const save = vi.spyOn(client, 'saveEntryFormSublist').mockResolvedValue(FORM)
    renderWithQuery(<AdminEntryForms />)
    await screen.findByText('Item sublist')
    // Description is second; Move up swaps it above ItemCode (top row = leftmost column).
    await userEvent.click(screen.getByRole('button', { name: 'Move Description up' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith('f1', ['Description', 'ItemCode', 'Qty', 'Uom', 'EstUnitPrice']))
  })

  it('a system form is wholly read-only — no verbs, no drag affordances', async () => {
    vi.spyOn(client, 'getEntryForms').mockResolvedValue([{ ...FORM, isSystem: true }])
    renderWithQuery(<AdminEntryForms />)
    expect(await screen.findByText('Standard — the parity baseline, read-only')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save form' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Add group' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Move Description up' })).toBeNull()
  })
})

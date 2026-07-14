import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { AdminCustomFields } from './AdminCustomFields'
import * as client from '../../api/client'
import type { CustomFieldDefDto, ImpactReportDto } from '../../api/client'

const DEF: CustomFieldDefDto = {
  id: 'd1', code: 'custbody_project', label: 'Project', recordType: 'PurchaseOrder', dataType: 'Text',
  customListId: null, required: false, helpText: '', active: true, sort: 0, valueCount: 0,
  displayType: 'Normal', showInList: false, scope: 'Header', recordTypes: ['PurchaseOrder'],
}
const report = (over: Partial<ImpactReportDto>): ImpactReportDto => ({
  configReferences: [], data: [], liveCount: 0, historicalCount: 0,
  canDelete: false, canPurge: false, blockedReason: null, ...over,
})

describe('AdminCustomFields — CF-FIX4-T4 placement cascade', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getCustomFieldDefs').mockResolvedValue([])
    vi.spyOn(client, 'getCustomLists').mockResolvedValue([])
    vi.spyOn(client, 'getEntryForms').mockResolvedValue([
      // CFF-T2: the standard (system) form is source-controlled — the picker offers CUSTOM forms only.
      { id: 'f-std', code: 'ef_standard_po_form', name: 'Standard PO Form', recordType: 'PurchaseOrder', isSystem: true, active: true, roles: [], fields: [],
        groups: [{ id: 'g-h', subtabId: null, title: 'Header', sort: 0, columnBreak: false, isHeader: true }] },
      { id: 'f-cust', code: 'customform_po_extra', name: 'PO Extra Form', recordType: 'PurchaseOrder', isSystem: false, active: true, roles: [], fields: [],
        groups: [{ id: 'g-ch', subtabId: null, title: 'Header', sort: 0, columnBreak: false, isHeader: true }] },
    ])
  })

  it('CFF-T2: creating a field pre-selects the first CUSTOM form (not the standard) with Header default', async () => {
    const create = vi.spyOn(client, 'createCustomFieldDef').mockResolvedValue(DEF)
    renderWithQuery(<AdminCustomFields />)
    await userEvent.click(await screen.findByRole('button', { name: 'New field' }))
    await screen.findByText(/Placement — where this field appears/)
    expect(screen.getByLabelText('Purchase Order — form(s)')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Label'), 'Cascade Probe')
    await userEvent.click(screen.getByRole('button', { name: 'Create field' }))
    await waitFor(() => expect(create).toHaveBeenCalled())
    expect(create.mock.calls[0][0].placements).toEqual([
      { recordType: 'PurchaseOrder', formId: 'f-cust', groupId: null },   // the custom form, NOT the standard; Header default
    ])
  })

  it('a LINE-scope field skips placement (L4 — sublist columns have no groups)', async () => {
    renderWithQuery(<AdminCustomFields />)
    await userEvent.click(await screen.findByRole('button', { name: 'New field' }))
    await screen.findByText(/Placement — where this field appears/)
    const { pickSearchable } = await import('../../test/utils')
    await pickSearchable('Scope (header field or line column)', 'Line')
    expect(screen.queryByText(/Placement — where this field appears/)).toBeNull()
  })
})

describe('AdminCustomFields — CF-FIX3 three-tier lifecycle dialog', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getCustomFieldDefs').mockResolvedValue([DEF])
    vi.spyOn(client, 'getCustomLists').mockResolvedValue([])
  })

  it('Delete opens the IMPACT REPORT: a mandatory form placement red-flags and blocks the delete', async () => {
    vi.spyOn(client, 'getCustomFieldReferences').mockResolvedValue(report({
      configReferences: [{ consumerName: 'Entry Forms', kind: 'FormPlacement', targetId: 'f1', targetLabel: 'PO Standard Form', detail: 'PurchaseOrder form · MANDATORY at submit', isMandatory: true }],
      blockedReason: 'Referenced by 1 consumer(s) — remove the references first.',
    }))
    const del = vi.spyOn(client, 'deleteCustomFieldDef')
    renderWithQuery(<AdminCustomFields />)
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Project' }))

    // the report — the admin SEES what blocks before anything happens
    expect(await screen.findByText('PO Standard Form')).toBeInTheDocument()
    expect(screen.getByText('mandatory')).toBeInTheDocument()
    expect(screen.getByText(/remove the references first/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete field' })).toBeDisabled()
    expect(del).not.toHaveBeenCalled()
  })

  it('a clean report enables Tier-2 Delete', async () => {
    vi.spyOn(client, 'getCustomFieldReferences').mockResolvedValue(report({ canDelete: true }))
    const del = vi.spyOn(client, 'deleteCustomFieldDef').mockResolvedValue(undefined)
    renderWithQuery(<AdminCustomFields />)
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Project' }))
    const btn = await screen.findByRole('button', { name: 'Delete field' })
    await waitFor(() => expect(btn).toBeEnabled())
    await userEvent.click(btn)
    await waitFor(() => expect(del).toHaveBeenCalledWith('d1'))
  })

  it('historical-only values offer the governed Tier-3 PURGE behind a second confirm', async () => {
    vi.spyOn(client, 'getCustomFieldReferences').mockResolvedValue(report({
      canPurge: true, historicalCount: 2,
      data: [{ storeName: 'Record values', liveCount: 0, historicalCount: 2, byRecordType: [{ recordType: 'PurchaseOrder', live: 0, historical: 2 }] }],
      blockedReason: '2 value(s) exist on closed records — purge (with snapshot) or deactivate.',
    }))
    const purge = vi.spyOn(client, 'purgeCustomField').mockResolvedValue(undefined)
    renderWithQuery(<AdminCustomFields />)
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Project' }))

    expect(await screen.findByText(/snapshotted to the audit trail/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete field' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Purge history' }))          // first click arms…
    expect(purge).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Confirm purge' }))          // …second executes
    await waitFor(() => expect(purge).toHaveBeenCalledWith('d1'))
  })

  it('T5: the edit modal keeps the type immutable and Create replacement prefills a NEW field', async () => {
    renderWithQuery(<AdminCustomFields />)
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    expect(await screen.findByText(/inactivate this field and create a new one/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Create replacement for Project' }))
    // now in CREATE mode: prefilled label, fresh suggested Internal ID, type choosable again
    expect(await screen.findByText(/Replacement for — Project/)).toBeInTheDocument()
    expect(screen.getByLabelText('Label')).toHaveValue('Project')
    expect(screen.getByLabelText('Internal ID')).toHaveValue('project_2')
    expect(screen.getByLabelText('Data type')).toBeInTheDocument()
  })
})

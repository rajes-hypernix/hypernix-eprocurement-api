import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, STANDARD_PR_FORM } from '../../test/utils'
import { buildSections, splitSubtabs, nativeDefaults, missingRequired } from './resolvedForm'
import { PrForm } from '../sourcing/PrForm'
import { AdminNumbering } from '../admin/AdminNumbering'
import * as client from '../../api/client'
import type { ResolvedFormFieldDto } from '../../api/client'

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

describe('resolvedForm — the definition→layout projection', () => {
  it('the seeded Standard form reproduces HEADER_SECTION exactly (the parity pin)', () => {
    const sections = buildSections(STANDARD_PR_FORM.fields as ResolvedFormFieldDto[])
    expect(sections).toHaveLength(1)
    const s = sections[0]
    expect(s.title).toBe('Header')
    // Two 3-across rows in the exact old order, memo full-width — byte-identical layout.
    expect(s.rows.map((r) => r.map((f) => f.key))).toEqual([
      ['requestor', 'department', 'category'],
      ['location', 'job', 'requiredDate'],
    ])
    expect(s.fullWidth!.map((f) => f.key)).toEqual(['memo'])
    expect(s.rows[1][1].label).toBe('Job / Cost ref')
    expect(s.rows[0][1].placeholder).toBe('e.g. Maintenance')
    expect(s.rows[1][2].dataType).toBe('date')
    expect(s.fullWidth![0].label).toBe('Memo / Justification')
  })

  it('subtabs split, defaults extract (natives only) and required natives are named', () => {
    const fields = [
      ...STANDARD_PR_FORM.fields.map((f) => f.fieldKey === 'Department' ? { ...f, requiredOnForm: true } : f),
      { fieldKey: 'cf_warranty', label: 'Warranty', dataType: 'Date', kind: 'Custom', subtab: 'Additional', fieldGroup: 'Extra', sort: 10, displayType: 'Normal', requiredOnForm: false, defaultValue: '2026-08-01', sourceFieldKey: null, fullWidth: false, placeholder: null, customListCode: null, options: null },
    ] as ResolvedFormFieldDto[]
    const { main, tabs } = splitSubtabs(fields)
    expect(main).toHaveLength(7)
    expect(tabs).toEqual([['Additional', [expect.objectContaining({ fieldKey: 'cf_warranty' })]]])
    expect(nativeDefaults(fields)).toEqual({})                      // cf default is NOT a native default
    expect(missingRequired(fields, { department: '' })).toEqual(['Department'])
    expect(missingRequired(fields, { department: 'Maintenance' })).toEqual([])
  })
})

describe('PrForm — resolver-driven (D7)', () => {
  it('renders the ROLE form: hidden field gone, default prefilled, required blocks submit client-side', async () => {
    identity(['ManageRequisitions'])
    const roleForm = {
      ...STANDARD_PR_FORM, formCode: 'ef_buyer_pr', formName: 'Buyer PR Form',
      fields: STANDARD_PR_FORM.fields
        .filter((f) => f.fieldKey !== 'Job')                        // hidden by REMOVAL from the form
        .map((f) => f.fieldKey === 'Category' ? { ...f, displayType: 'Hidden' } : f)
        .map((f) => f.fieldKey === 'Department' ? { ...f, requiredOnForm: true } : f)
        .map((f) => f.fieldKey === 'RequiredDate' ? { ...f, defaultValue: '2026-07-19' } : f),
    }
    vi.spyOn(client, 'resolveEntryForm').mockResolvedValue(roleForm as never)
    const create = vi.spyOn(client, 'createPr')

    renderWithProviders(<PrForm id={null} onBack={() => {}} />)
    await screen.findByLabelText('Requestor')
    expect(screen.queryByLabelText('Job / Cost ref')).not.toBeInTheDocument()   // removed
    expect(screen.queryByLabelText('Category')).not.toBeInTheDocument()         // displayType hidden
    expect(screen.getByLabelText('Required by')).toHaveValue('2026-07-19')      // token-resolved default

    await userEvent.click(screen.getAllByRole('button', { name: 'Submit PR' })[0])
    expect(await screen.findByText(/Required on your form before submit: Department/)).toBeInTheDocument()
    expect(create).not.toHaveBeenCalled()

    await userEvent.type(screen.getByLabelText('Department'), 'Maintenance')
    create.mockResolvedValue({ id: 'p9' } as never)
    await userEvent.click(screen.getAllByRole('button', { name: 'Submit PR' })[0])
    await waitFor(() => expect(create).toHaveBeenCalledWith(expect.objectContaining({ department: 'Maintenance' }), true))
  })

  it('renders a custom field on an admin-defined SUBTAB on edit, saving via its own endpoint', async () => {
    identity(['ManageRequisitions', 'ReadCustomValues', 'EditCustomValues'])
    const form = {
      ...STANDARD_PR_FORM,
      fields: [...STANDARD_PR_FORM.fields,
        { fieldKey: 'cf_site_ref', label: 'Site Ref', dataType: 'Text', kind: 'Custom', subtab: 'Additional', fieldGroup: 'Extra', sort: 10, displayType: 'Normal', requiredOnForm: false, defaultValue: null, sourceFieldKey: null, fullWidth: false, placeholder: null, customListCode: null, options: null }],
    }
    vi.spyOn(client, 'resolveEntryForm').mockResolvedValue(form as never)
    vi.spyOn(client, 'getRequisition').mockResolvedValue({
      id: 'p1', code: 'PR-2026-0001', requestor: 'QA', department: 'D', location: 'L', category: 'C',
      job: 'J', memo: 'm', requiredDate: null, headerStatus: 'Draft', submitted: false,
      lines: [{ id: 'l1', itemCode: 'X', description: 'x', qty: 1, uom: 'Unit', estUnitPrice: 1, lifecycleStatus: 'Open', editable: true }],
    } as never)
    vi.spyOn(client, 'getCustomValues').mockResolvedValue([
      { code: 'cf_site_ref', label: 'Site Ref', dataType: 'Text', required: false, helpText: '', value: null },
    ])
    vi.spyOn(client, 'updatePr').mockResolvedValue({ id: 'p1' } as never)
    const saveCf = vi.spyOn(client, 'saveCustomValues').mockResolvedValue([])

    renderWithProviders(<PrForm id="p1" onBack={() => {}} />)
    await screen.findByLabelText('Requestor')
    await userEvent.click(screen.getByRole('button', { name: 'Additional' }))   // the admin-defined subtab
    await userEvent.type(await screen.findByLabelText('Site Ref'), 'SR-77')
    await userEvent.click(screen.getAllByRole('button', { name: 'Save changes' })[0])
    await waitFor(() => expect(saveCf).toHaveBeenCalledWith('Requisition', 'p1', { cf_site_ref: 'SR-77' }))
  })
})

describe('AdminNumbering — format is Setup configuration (A70)', () => {
  it('shows the schemes with next-code previews and saves a format change', async () => {
    identity(['ManageNumbering'])
    vi.spyOn(client, 'getNumberingSchemes').mockResolvedValue([
      { recordType: 'PurchaseOrder', prefix: 'PO', yearSegment: true, digits: 4, nextPreview: 'PO-2026-0013' },
    ])
    const update = vi.spyOn(client, 'updateNumberingScheme').mockResolvedValue(
      { recordType: 'PurchaseOrder', prefix: 'SPO', yearSegment: true, digits: 5, nextPreview: 'SPO-2026-00001' })

    renderWithProviders(<AdminNumbering />)
    expect(await screen.findAllByText('PO-2026-0013')).not.toHaveLength(0)
    await userEvent.click(screen.getByRole('button', { name: 'Open PurchaseOrder' }))
    const prefix = await screen.findByLabelText('Prefix (A–Z, 0–9, dash)')
    await userEvent.clear(prefix)
    await userEvent.type(prefix, 'SPO')
    const digits = screen.getByLabelText('Digits (3–6)')
    await userEvent.clear(digits)
    await userEvent.type(digits, '5')
    await userEvent.click(screen.getByRole('button', { name: 'Save format' }))
    await waitFor(() => expect(update).toHaveBeenCalledWith('PurchaseOrder', { prefix: 'SPO', yearSegment: true, digits: 5 }))
  })
})

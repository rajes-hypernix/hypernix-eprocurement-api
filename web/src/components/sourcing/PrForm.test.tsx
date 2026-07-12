import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders, STANDARD_PR_FORM } from '../../test/utils'
import { PrForm } from './PrForm'
import * as client from '../../api/client'

const pr = (headerStatus: string) => ({
  id: 'p1', code: 'PR-2026-0434', requestor: 'QA', department: 'Electrical', location: 'Bintulu',
  category: 'Electrical', memo: 'draft', job: 'J', costCentre: 'C', project: null,
  raisedDate: '2026-06-01', requiredDate: '2026-07-30', status: headerStatus, value: 0,
  headerStatus, submitted: headerStatus !== 'Draft',
  lines: [{ id: 'l1', itemCode: 'CBL-1002', description: 'Cable', qty: 300, uom: 'Meter', estUnitPrice: 62, status: 'available', ref: null, lifecycleStatus: 'Open', noQuotes: false, editable: true }],
})

describe('PrForm — Submit PR (Bug 3)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'resolveEntryForm').mockResolvedValue(STANDARD_PR_FORM as never)
  })

  it('a Draft PR shows Submit PR + Save changes; submitting calls the API', async () => {
    vi.spyOn(client, 'getRequisition').mockResolvedValue(pr('Draft') as never)
    vi.spyOn(client, 'updatePr').mockResolvedValue(pr('Draft') as never)
    const submit = vi.spyOn(client, 'submitPr').mockResolvedValue(pr('Submitted') as never)

    renderWithProviders(<PrForm id="p1" onBack={() => {}} />)
    // Actions appear at the top AND bottom of the form (item 2).
    const submitBtns = await screen.findAllByRole('button', { name: /Submit PR/i })
    expect(submitBtns).toHaveLength(2)
    expect(screen.getAllByRole('button', { name: 'Save changes' })).toHaveLength(2)

    await userEvent.click(submitBtns[0])
    await waitFor(() => expect(submit).toHaveBeenCalledWith('p1'))
  })

  it('a Submitted PR does NOT show Submit PR', async () => {
    vi.spyOn(client, 'getRequisition').mockResolvedValue(pr('Submitted') as never)
    renderWithProviders(<PrForm id="p1" onBack={() => {}} />)
    expect(await screen.findAllByRole('button', { name: 'Save changes' })).toHaveLength(2)
    expect(screen.queryByRole('button', { name: /Submit PR/i })).not.toBeInTheDocument()
  })
})

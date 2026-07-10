import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { Forms } from './Forms'
import * as client from '../../api/client'
import type { FormTemplateDto } from '../../api/client'

const form = (over: Partial<FormTemplateDto>): FormTemplateDto => ({
  id: 'f1', code: 'FORM-2026-0001', name: 'A form', version: 1, updatedUtc: '2026-07-01T00:00:00Z',
  items: [], technicalSections: [], commercialSections: [], purpose: 'Rfq', ...over,
})

describe('Forms — Onboarding-purpose management (Slice E)', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('lists the Purpose and filters by it', async () => {
    vi.spyOn(client, 'getForms').mockResolvedValue([
      form({ id: 'f1', name: 'RFQ Standard', purpose: 'Rfq' }),
      form({ id: 'f2', code: 'FORM-2026-0003', name: 'HSE Pack', purpose: 'Onboarding' }),
    ])
    renderWithQuery(<Forms route="forms" onNavigate={() => {}} />)

    expect(await screen.findByText('HSE Pack')).toBeInTheDocument()
    expect(screen.getByText('RFQ Standard')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Onboarding' }))   // filter
    expect(screen.queryByText('RFQ Standard')).not.toBeInTheDocument()
    expect(screen.getByText('HSE Pack')).toBeInTheDocument()
  })

  it('editor exposes the Purpose selector and saves it', async () => {
    vi.spyOn(client, 'getForm').mockResolvedValue(form({ id: 'f2', name: 'HSE Pack', purpose: 'Onboarding' }))
    const update = vi.spyOn(client, 'updateForm').mockResolvedValue(form({ id: 'f2', name: 'HSE Pack', purpose: 'Both' }))
    renderWithQuery(<Forms route="forms/f2" onNavigate={() => {}} />)

    const sel = (await screen.findByLabelText('Purpose')) as HTMLSelectElement
    expect(sel.value).toBe('Onboarding')

    await userEvent.selectOptions(sel, 'Both')
    await userEvent.click(screen.getByRole('button', { name: /Save form/ }))

    await waitFor(() => expect(update).toHaveBeenCalled())
    expect(update.mock.calls[0][1].purpose).toBe('Both')
  })
})

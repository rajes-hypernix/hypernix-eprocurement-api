import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { OnboardingForm } from './OnboardingForm'
import * as client from '../../api/client'
import type { OnboardingDraft } from '../../api/client'

const draft = (type = 'Non-SWEC'): OnboardingDraft => ({
  id: 'a1', code: 'VOB-2026-0001', status: 'InProgress', type,
  name: '', registrationNo: '', location: '', email: '', contactName: '', contactPhone: '',
  country: 'MY', state: '',
  bank: { bank: '', accountNo: '', swift: '' }, categories: [], financials: [], answers: [], documents: [],
  packs: [{
    id: 'p1', code: 'FORM-2026-0003', name: 'Health, Safety & Environment',
    items: [{ kind: 'question', group: 'technical', section: 'HSE', label: 'Do you maintain an HSE policy?', type: 'yesno', required: true, config: '{}', help: '', order: 0 }],
  }],
  financialBand: null, submittedUtc: null,
})

describe('OnboardingForm (Slice C)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getCustomLists').mockResolvedValue([])   // vendor form reuses the shared Custom Lists
    vi.spyOn(client, 'getSwec').mockResolvedValue([])          // real SWEC picker for categories
  })

  it('renders the company section and, for Non-SWEC, a Financials step + the pack question (D3)', async () => {
    vi.spyOn(client, 'getOnboardingDraft').mockResolvedValue(draft('Non-SWEC'))
    vi.spyOn(client, 'saveOnboardingDraft').mockResolvedValue(draft())
    renderWithQuery(<OnboardingForm token="tok" onSubmitted={() => {}} />)

    expect(await screen.findByLabelText('Registered name')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Financials/ })).toBeInTheDocument()   // Non-SWEC

    // The question pack renders with the shared renderer (yes/no answer control).
    await userEvent.click(screen.getByRole('button', { name: /Health, Safety & Environment/ }))
    expect(await screen.findByText('Do you maintain an HSE policy?')).toBeInTheDocument()
  })

  it('warns on an incomplete section but still lets you continue (progress autosaves)', async () => {
    vi.spyOn(client, 'getOnboardingDraft').mockResolvedValue(draft('Non-SWEC'))   // name/reg empty
    vi.spyOn(client, 'saveOnboardingDraft').mockResolvedValue(draft())
    renderWithQuery(<OnboardingForm token="tok" onSubmitted={() => {}} />)

    // the incomplete company section shows an amber warning, not a block
    expect(await screen.findByText(/This section still needs/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Next/ }))
    expect(await screen.findByLabelText('Bank')).toBeInTheDocument()   // advanced to Banking (not blocked)
  })

  it('enforces completeness on submit (lists incomplete sections, does not submit)', async () => {
    vi.spyOn(client, 'getOnboardingDraft').mockResolvedValue(draft('Non-SWEC'))
    vi.spyOn(client, 'saveOnboardingDraft').mockResolvedValue(draft())
    const submit = vi.spyOn(client, 'submitOnboardingDraft').mockResolvedValue({ ...draft(), status: 'Submitted' })
    renderWithQuery(<OnboardingForm token="tok" onSubmitted={() => {}} />)

    await userEvent.click(await screen.findByRole('button', { name: /Review & submit/ }))   // jump to review (free)
    await userEvent.click(screen.getByRole('button', { name: /Submit application/ }))
    expect(await screen.findByText(/Complete these sections before submitting/)).toBeInTheDocument()
    expect(submit).not.toHaveBeenCalled()
  })

  it('B1 — a SWEC application has no Financials step (pre-qual waived)', async () => {
    vi.spyOn(client, 'getOnboardingDraft').mockResolvedValue(draft('SWEC'))
    vi.spyOn(client, 'saveOnboardingDraft').mockResolvedValue(draft('SWEC'))
    renderWithQuery(<OnboardingForm token="tok" onSubmitted={() => {}} />)

    await screen.findByLabelText('Registered name')
    expect(screen.queryByRole('button', { name: /Financials/ })).not.toBeInTheDocument()
  })

  it('autosaves on navigation and submits when every section is complete', async () => {
    // A fully-complete SWEC draft (no financials; required docs uploaded; pack question not required).
    const complete = {
      ...draft('SWEC'), name: 'Acme Sdn Bhd', registrationNo: '123-A',
      documents: [{ key: 'ssm', fileName: 's.pdf', storedFileId: 'f1' }, { key: 'bank', fileName: 'b.pdf', storedFileId: 'f2' }],
      packs: [{ id: 'p1', code: 'F', name: 'HSE', items: [{ kind: 'question', group: 'technical', section: 'HSE', label: 'Q', type: 'yesno', required: false, config: '{}', help: '', order: 0 }] }],
    }
    vi.spyOn(client, 'getOnboardingDraft').mockResolvedValue(complete)
    const save = vi.spyOn(client, 'saveOnboardingDraft').mockResolvedValue(complete)
    const submit = vi.spyOn(client, 'submitOnboardingDraft').mockResolvedValue({ ...complete, status: 'Submitted' })
    const onSubmitted = vi.fn()
    renderWithQuery(<OnboardingForm token="tok" onSubmitted={onSubmitted} />)

    await userEvent.click(await screen.findByRole('button', { name: /Review & submit/ }))   // autosave on nav
    await waitFor(() => expect(save).toHaveBeenCalled())

    await userEvent.click(screen.getByRole('button', { name: /Submit application/ }))
    await waitFor(() => expect(submit).toHaveBeenCalledWith('tok'))
    expect(onSubmitted).toHaveBeenCalled()
  })
})

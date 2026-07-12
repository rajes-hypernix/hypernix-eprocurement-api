import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { OnboardingQueue } from './OnboardingQueue'
import { OnboardingReview } from './OnboardingReview'
import { OnboardingResubmit } from './OnboardingResubmit'
import * as client from '../../api/client'
import type { OnboardingReview as Review, OnboardingApplication } from '../../api/client'

const review = (status = 'UnderReview'): Review => ({
  id: 'a1', code: 'VOB-2026-0001', status, type: 'Non-SWEC', source: 'SelfService',
  name: 'Acme Sdn Bhd', registrationNo: '1188221-P', location: 'Bintulu', email: 'sales@acme.my',
  contactName: 'Aishah', contactPhone: '082', bank: { bank: 'Maybank', accountNo: '5141', swift: 'MBBEMYKL' },
  categories: ['E12'], financial: {
    band: 'C', risk: 'Medium', zone: 'Grey zone', weightedZ: 1.26, score: 28, statement: 'Moderate financial risk.',
    years: [0, 1, 2].map((i) => ({ yearIndex: i, x1: 0.1, x2: 0.1, x3: 0.1, x4: 0.1, x5: 0.8, z: 1.48 })),
  },
  answers: [{ pack: 'HSE', label: 'HSE policy?', value: 'Yes' }], documents: [], rounds: [],
  duplicateWarning: null, submittedUtc: null, decisionUtc: null, promotedVendorId: null, rejectReason: null,
})

describe('Onboarding queue + review (Slice D)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getSwec').mockResolvedValue([])
  })

  const queueItem = (over: Record<string, unknown> = {}) => ({
    id: 'a1', code: 'VOB-2026-0001', name: 'Acme', type: 'Non-SWEC', status: 'UnderReview',
    source: 'SelfService', createdUtc: '2026-07-01', submittedUtc: '2026-07-01', openRoundNo: null, roundCount: 0,
    invitationId: 'inv1', ...over,
  })

  it('queue lists applications and Review navigates', async () => {
    vi.spyOn(client, 'getOnboardingApplications').mockResolvedValue([queueItem() as never])
    const onNav = vi.fn()
    renderWithProviders(<OnboardingQueue onNavigate={onNav} />)
    expect(await screen.findByText('VOB-2026-0001')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Review/ }))
    expect(onNav).toHaveBeenCalledWith('onboarding/a1')
  })

  it('#4 — offers Resend link for an invited vendor and shows the new magic link', async () => {
    vi.spyOn(client, 'getOnboardingApplications').mockResolvedValue([queueItem({ status: 'Invited' }) as never])
    const resend = vi.spyOn(client, 'resendOnboardingInvitation').mockResolvedValue({
      id: 'inv1', email: 'v@x.my', type: 'Non-SWEC', status: 'Sent', invitedByName: 'F', createdUtc: '', expiresUtc: '',
      applicationId: 'a1', applicationCode: 'VOB-2026-0001', magicLink: 'http://localhost:5173/?t=new-tok#onboard',
    })
    renderWithProviders(<OnboardingQueue onNavigate={() => {}} />)
    await userEvent.click(await screen.findByRole('button', { name: /Resend link/ }))
    await waitFor(() => expect(resend).toHaveBeenCalledWith('inv1'))
    expect(await screen.findByText('Link resent')).toBeInTheDocument()
    expect((screen.getByLabelText('Magic link') as HTMLInputElement).value).toContain('?t=new-tok')
  })

  it('review shows the financial band + calc drawer; approve promotes and shows the vendor code', async () => {
    vi.spyOn(client, 'getOnboardingApplication').mockResolvedValue(review())
    const approve = vi.spyOn(client, 'approveOnboarding').mockResolvedValue({ vendorId: 'v1', vendorCode: 'SWK-V-2026-0001', duplicateWarning: null })
    renderWithProviders(<OnboardingReview id="a1" onBack={() => {}} />)

    expect(await screen.findByText('Financial pre-qualification')).toBeInTheDocument()
    expect(screen.getByText('Band C')).toBeInTheDocument()

    // calc drawer
    await userEvent.click(screen.getByRole('button', { name: /View calculation/ }))
    expect(await screen.findByText(/Working capital \/ TA/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))

    await userEvent.click(screen.getAllByRole('button', { name: /Approve/ })[0])
    await waitFor(() => expect(approve).toHaveBeenCalledWith('a1'))
    expect(await screen.findByText('Approved & onboarded')).toBeInTheDocument()
    expect(screen.getByText('SWK-V-2026-0001')).toBeInTheDocument()
  })

  it('batched clarification: add an item and send it as one round', async () => {
    vi.spyOn(client, 'getOnboardingApplication').mockResolvedValue(review())
    const clarify = vi.spyOn(client, 'clarifyOnboarding').mockResolvedValue(review('ClarificationRequested'))
    renderWithProviders(<OnboardingReview id="a1" onBack={() => {}} />)

    await userEvent.click((await screen.findAllByRole('button', { name: /Request clarification/ }))[0])
    await userEvent.type(screen.getByLabelText('Clarification topic'), 'ISO 9001 certificate')
    await userEvent.click(screen.getByRole('button', { name: /Add item/ }))
    await userEvent.click(screen.getAllByRole('button', { name: /Send clarification/ })[0])

    await waitFor(() => expect(clarify).toHaveBeenCalled())
    expect(clarify.mock.calls[0][2]).toEqual([{ topic: 'ISO 9001 certificate', request: 'Please review and resubmit.' }])
  })

  it('a Submitted application offers Start review (A4)', async () => {
    vi.spyOn(client, 'getOnboardingApplication').mockResolvedValue(review('Submitted'))
    const start = vi.spyOn(client, 'startOnboardingReview').mockResolvedValue(review('UnderReview'))
    renderWithProviders(<OnboardingReview id="a1" onBack={() => {}} />)
    await userEvent.click((await screen.findAllByRole('button', { name: 'Start review' }))[0])
    await waitFor(() => expect(start).toHaveBeenCalledWith('a1'))
  })
})

describe('Vendor resubmit (Slice D, A8)', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('shows only the flagged items and resubmits all in one go', async () => {
    const app: OnboardingApplication = {
      id: 'a1', code: 'VOB-2026-0001', status: 'ClarificationRequested', type: 'Non-SWEC', name: 'Acme', email: '',
      packs: [], createdUtc: '', submittedUtc: null,
      rounds: [{
        roundNo: 1, direction: 'BuyerToVendor', status: 'Open', message: 'A few items.', raisedByName: 'Faridah',
        raisedUtc: '', respondedUtc: null, items: [{ topic: 'ISO 9001', request: 'Attach a valid certificate.', response: '' }],
      }],
    }
    const resubmit = vi.spyOn(client, 'resubmitOnboarding').mockResolvedValue(app)
    renderWithProviders(<OnboardingResubmit token="tok" app={app} onDone={() => {}} />)

    expect(screen.getByText('ISO 9001')).toBeInTheDocument()          // only the flagged item
    await userEvent.type(screen.getByLabelText('Response to ISO 9001'), 'Attached.')
    await userEvent.click(screen.getByRole('button', { name: /Resubmit all/ }))
    await waitFor(() => expect(resubmit).toHaveBeenCalledWith('tok', ['Attached.']))
  })
})

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { OnboardingInvite } from './OnboardingInvite'
import { NewVendorChooser } from './NewVendorChooser'
import { OnboardingLanding } from './OnboardingLanding'
import { OnboardingQueue } from './OnboardingQueue'
import { VendorsPage } from '../vendors/VendorsPage'
import * as client from '../../api/client'

const TEMPLATES = [
  { id: 't1', code: 'FORM-2026-0003', name: 'Health, Safety & Environment', questionCount: 3 },
  { id: 't2', code: 'FORM-2026-0004', name: 'Quality & Certification', questionCount: 3 },
]
const INVITE = {
  id: 'i1', email: 'vieshall@hypernix.net', type: 'Non-SWEC', status: 'Sent', invitedByName: 'Faridah',
  createdUtc: '', expiresUtc: '', applicationId: 'a1', applicationCode: 'VOB-2026-0001',
  magicLink: 'http://localhost:5173/?t=tok-abc#onboard',
}

describe('Onboarding — invite (Slice B)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getOnboardingTemplates').mockResolvedValue(TEMPLATES)
  })

  it('E2 — the vendor email input defaults to vieshall@hypernix.net', async () => {
    renderWithQuery(<OnboardingInvite onBack={() => {}} />)
    const email = (await screen.findByLabelText('Vendor email')) as HTMLInputElement
    expect(email.value).toBe('vieshall@hypernix.net')
  })

  it('sends the invite with the email, type and selected packs, then shows the magic link', async () => {
    const send = vi.spyOn(client, 'sendOnboardingInvitation').mockResolvedValue(INVITE)
    renderWithQuery(<OnboardingInvite onBack={() => {}} />)
    await screen.findByLabelText('Vendor email')

    await userEvent.click(screen.getByRole('button', { name: /Generate/ }))

    await waitFor(() => expect(send).toHaveBeenCalled())
    expect(send.mock.calls[0][0]).toMatchObject({
      email: 'vieshall@hypernix.net', type: 'Non-SWEC', selectedTemplateIds: ['t1', 't2'],
    })
    // Confirmation modal surfaces the emailed magic link.
    expect(await screen.findByText('Invitation sent')).toBeInTheDocument()
    expect((screen.getByLabelText('Magic link') as HTMLInputElement).value).toContain('?t=tok-abc')
  })

  it('deselecting a pack drops it from the invitation', async () => {
    const send = vi.spyOn(client, 'sendOnboardingInvitation').mockResolvedValue(INVITE)
    renderWithQuery(<OnboardingInvite onBack={() => {}} />)
    await userEvent.click(await screen.findByLabelText('Quality & Certification'))   // uncheck t2
    await userEvent.click(screen.getByRole('button', { name: /Generate/ }))

    await waitFor(() => expect(send).toHaveBeenCalled())
    expect(send.mock.calls[0][0].selectedTemplateIds).toEqual(['t1'])
  })
})

describe('Onboarding — New Vendor chooser (Slice B)', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('offers manual and invite, and Invite navigates to the invite screen', async () => {
    const onInvite = vi.fn()
    renderWithQuery(<NewVendorChooser onManual={() => {}} onInvite={onInvite} onBack={() => {}} />)
    expect(screen.getByText('Enter manually')).toBeInTheDocument()
    await userEvent.click(screen.getByText('Invite vendor'))
    expect(onInvite).toHaveBeenCalled()
  })

  // Regression: the chooser's Invite must reach the invite SCREEN, not the onboarding queue.
  it('VendorsPage wires the chooser Invite to onboarding/invite', async () => {
    const onNav = vi.fn()
    renderWithQuery(<VendorsPage route="vendors/new" onNavigate={onNav} />)
    await userEvent.click(screen.getByText('Invite vendor'))
    expect(onNav).toHaveBeenCalledWith('onboarding/invite')
  })
})

describe('Onboarding — magic-link landing (Slice B)', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('A2 — resolves the token from the URL and shows the scoped application', async () => {
    window.history.pushState({}, '', '/?t=tok-xyz#onboard')
    const resolve = vi.spyOn(client, 'resolveOnboardingLink').mockResolvedValue({
      id: 'a1', code: 'VOB-2026-0001', status: 'InProgress', type: 'Non-SWEC', name: 'Acme',
      email: 'vieshall@hypernix.net', rounds: [], packs: [{ id: 'p', code: 'FORM-2026-0003', name: 'HSE', questionCount: 3 }],
      createdUtc: '', submittedUtc: null,
    })

    renderWithQuery(<OnboardingLanding onStart={() => {}} />)

    expect(await screen.findByText('VOB-2026-0001')).toBeInTheDocument()
    expect(screen.getByText('Non-SWEC')).toBeInTheDocument()
    expect(resolve).toHaveBeenCalledWith('tok-xyz')
  })
})

describe('OnboardingQueue — revoke invitation (Slice K / K1)', () => {
  beforeEach(() => vi.restoreAllMocks())

  const APPS = [{
    id: 'a1', code: 'VOB-2026-0001', name: 'Acme', type: 'Non-SWEC', status: 'Invited',
    createdUtc: '2026-07-01T00:00:00Z', submittedUtc: null, openRoundNo: null, invitationId: 'inv1',
  }]

  it('offers Revoke for a live invitation and requires confirmation before calling the API', async () => {
    vi.spyOn(client, 'getOnboardingApplications').mockResolvedValue(APPS as never)
    const revoke = vi.spyOn(client, 'revokeOnboardingInvitation').mockResolvedValue(undefined as never)
    renderWithQuery(<OnboardingQueue onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('VOB-2026-0001')).toBeInTheDocument())

    await userEvent.click(screen.getByRole('button', { name: /Revoke/i }))
    // The confirm modal gates the call — nothing has hit the API yet.
    expect(revoke).not.toHaveBeenCalled()
    expect(screen.getByText(/Revoke the invitation for VOB-2026-0001/i)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /Revoke link/i }))
    await waitFor(() => expect(revoke).toHaveBeenCalledWith('inv1'))
  })

  it('does not offer Revoke once the application is already submitted', async () => {
    vi.spyOn(client, 'getOnboardingApplications').mockResolvedValue([
      { ...APPS[0], status: 'Submitted', submittedUtc: '2026-07-02T00:00:00Z' },
    ] as never)
    renderWithQuery(<OnboardingQueue onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText('VOB-2026-0001')).toBeInTheDocument())
    expect(screen.queryByRole('button', { name: /Revoke/i })).not.toBeInTheDocument()
  })
})

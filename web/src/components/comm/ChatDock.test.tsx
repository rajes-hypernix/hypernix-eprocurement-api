import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { ChatDock } from './ChatDock'
import * as client from '../../api/client'

const VENDOR = { code: 'VU-borneo', name: 'Borneo Inspection', kind: 'vendor', roles: ['Vendor'], vendorName: 'Borneo Inspection', vendorId: 'v-borneo' }
const BUYER = { code: 'u_faridah', name: 'Faridah Yusof', kind: 'internal', roles: ['Buyer'], vendorName: null, vendorId: null }

describe('ChatDock', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getDemoUser').mockReturnValue('VU-borneo')
    vi.spyOn(client, 'getPersonas').mockResolvedValue([VENDOR, BUYER] as never)
    vi.spyOn(client, 'getClarificationThreads').mockResolvedValue([])
    vi.spyOn(client, 'getMyInvitations').mockResolvedValue([
      { rfqId: 'r87', code: 'RFQ-2026-0087', title: 'Piping', envelope: 'Single', status: 'Open', currency: 'MYR', closesUtc: null, bidSubmitted: false, bidDraft: true, ownerUserId: 'u_faridah', ownerName: 'Faridah Yusof' },
    ] as never)
  })

  it('opens an in-dock compose prefilled with the current RFQ and its buyer-in-charge (no navigation)', async () => {
    renderWithProviders(<ChatDock route="bid/r87" />)
    // launcher present
    await waitFor(() => expect(screen.getByText('Clarifications')).toBeInTheDocument())
    // invitations need to load so the RFQ context resolves
    await waitFor(() => expect(client.getMyInvitations).toHaveBeenCalled())

    await userEvent.click(screen.getByText('Clarifications'))

    // Drawer opens straight into compose for this RFQ — no route change.
    await waitFor(() => expect(screen.getByText('New clarification')).toBeInTheDocument())
    const topic = screen.getAllByRole('combobox')[0] as HTMLSelectElement
    expect(topic.value).toBe('RFQ-2026-0087')
    // Buyer-in-charge is offered as the recipient.
    expect(screen.getByText('Faridah Yusof — in charge of this RFQ')).toBeInTheDocument()
  })

  it('preserves the compose draft when the dock is closed and reopened', async () => {
    renderWithProviders(<ChatDock route="bid/r87" />)
    await waitFor(() => expect(client.getMyInvitations).toHaveBeenCalled())
    await userEvent.click(screen.getByText('Clarifications'))
    await waitFor(() => expect(screen.getByText('New clarification')).toBeInTheDocument())

    const box = screen.getByPlaceholderText('Type your clarification…')
    await userEvent.type(box, 'Half-typed question')
    // close
    await userEvent.click(screen.getByTitle('Close'))
    expect(screen.queryByText('New clarification')).not.toBeInTheDocument()
    // reopen — the draft is still there
    await userEvent.click(screen.getByText('Clarifications'))
    expect((screen.getByPlaceholderText('Type your clarification…') as HTMLTextAreaElement).value).toBe('Half-typed question')
  })
})

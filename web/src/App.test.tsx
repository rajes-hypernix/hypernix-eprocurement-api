import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from './test/utils'
import App from './App'
import * as client from './api/client'

describe('App shell', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([
      { code: 'u_faridah', name: 'Faridah Yusof', kind: 'internal', roles: ['Buyer'], vendorName: null },
    ])
    // RM-P1: the sidebar derives from the server permission list — the nav item the test
    // clicks exists only when /auth/permissions grants its action.
    vi.spyOn(client, 'getPermissions').mockResolvedValue(['ViewDashboard', 'ViewVendors', 'ViewRequisitions', 'ViewRfqs'])
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([])
    vi.spyOn(client, 'getDashboard').mockResolvedValue({
      title: 'Sourcing Dashboard', subtitle: 'Live picture of requisitions, active RFQs and awards.',
      cards: [{ label: 'Open requisitions', value: '7', sub: 'ready to source', tone: 'clay', link: 'reqs' }],
    })
    vi.spyOn(client, 'getRfqs').mockResolvedValue([
      { id: 'r1', code: 'RFQ-2026-0087', title: 'Piping', envelope: 'Single', status: 'Open', currency: 'MYR', closesUtc: '2026-07-03T00:00:00Z', invitedCount: 2, lineCount: 3, questionCount: 6, bidCount: 1 },
    ])
  })

  it('renders the brand and the Sourcing Dashboard by default', async () => {
    renderWithProviders(<App />)
    expect(screen.getByText('Hypernix eProcure')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'Sourcing Dashboard' })).toBeInTheDocument())
    // Buyer dashboard now shows the analytics panel (demo) in place of the Active RFQs table.
    expect(screen.getByText('Sourcing analytics')).toBeInTheDocument()
    expect(screen.getByText('PO-2026-0141')).toBeInTheDocument()
  })

  it('navigates to another screen when a nav item is clicked', async () => {
    renderWithProviders(<App />)
    // findBy: the nav renders once the permissions query resolves (server-derived, RM-P1).
    await userEvent.click(await screen.findByText('Vendor Master'))
    expect(screen.getByRole('heading', { level: 1, name: 'Vendor Master' })).toBeInTheDocument()
  })
})

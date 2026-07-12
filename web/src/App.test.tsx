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
    vi.spyOn(client, 'getPermissions').mockResolvedValue(['UseDashboards', 'ViewVendors', 'ViewRequisitions', 'ViewRfqs'])
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([])
    // D4: the dashboard renders from the resolved portlet list + live metric values.
    vi.spyOn(client, 'getMyDashboard').mockResolvedValue({
      id: 'd1', name: 'Sourcing Dashboard', isPersonalized: false,
      portlets: [{
        id: 'p1', portletType: 'KpiScorecard', title: 'Sourcing pipeline', col: 0, row: 0, width: 2,
        savedViewId: null, configJson: JSON.stringify({ items: [{ metricId: 'openRequisitions', link: 'reqs' }] }),
      }],
    })
    vi.spyOn(client, 'getMetricValue').mockResolvedValue({
      id: 'openRequisitions', label: 'Open requisitions', unit: 'count', value: 7, notYetAvailable: false, excludedNullCount: 0,
    })
    vi.spyOn(client, 'getRfqs').mockResolvedValue([
      { id: 'r1', code: 'RFQ-2026-0087', title: 'Piping', envelope: 'Single', status: 'Open', currency: 'MYR', closesUtc: '2026-07-03T00:00:00Z', invitedCount: 2, lineCount: 3, questionCount: 6, bidCount: 1 },
    ])
  })

  it('renders the brand and the server-driven dashboard by default', async () => {
    renderWithProviders(<App />)
    expect(screen.getByText('Hypernix eProcure')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'Sourcing Dashboard' })).toBeInTheDocument())
    // D4: portlets render live metric values — no mock analytics panel.
    await waitFor(() => expect(screen.getByText('Open requisitions')).toBeInTheDocument())
    expect(screen.getByText('7')).toBeInTheDocument()
  })

  it('navigates to another screen when a nav item is clicked', async () => {
    renderWithProviders(<App />)
    // findBy: the nav renders once the permissions query resolves (server-derived, RM-P1).
    await userEvent.click(await screen.findByText('Vendor Master'))
    expect(screen.getByRole('heading', { level: 1, name: 'Vendor Master' })).toBeInTheDocument()
  })
})

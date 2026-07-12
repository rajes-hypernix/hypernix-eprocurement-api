import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '../../test/utils'
import { KpiMeterPortlet } from './KpiMeterPortlet'
import { ChartPortlet } from './ChartPortlet'
import { ShortcutsPortlet } from './ShortcutsPortlet'
import * as client from '../../api/client'
import type { PortletDto } from '../../api/client'

const portlet = (type: string, configJson: string, savedViewId: string | null = null): PortletDto =>
  ({ id: 'p1', portletType: type, title: 'T', col: 0, row: 0, width: 1, savedViewId, configJson })

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

describe('KpiMeterPortlet — honest-null and targets', () => {
  it('renders "not yet available", never zero, for a backfill-null metric', async () => {
    identity([])
    vi.spyOn(client, 'getMetricValue').mockResolvedValue({
      id: 'prToPoCycleDays', label: 'PR → PO cycle', unit: 'days', value: null, notYetAvailable: true, excludedNullCount: 1,
    })
    renderWithProviders(<KpiMeterPortlet portlet={portlet('KpiMeter', '{"metricId":"prToPoCycleDays"}')} onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByText(/Not yet available/)).toBeInTheDocument())
    expect(screen.queryByText('0')).not.toBeInTheDocument()
  })

  it('renders a view-backed count with target progress — the gate KPI', async () => {
    identity([])
    vi.spyOn(client, 'aggregateView').mockResolvedValue({ viewId: 'v1', fn: 'count', fieldKey: null, value: 1, excludedNullCount: 0 })
    renderWithProviders(
      <KpiMeterPortlet portlet={portlet('KpiMeter', '{"fn":"count","target":3}', 'v1')} onNavigate={() => {}} />,
    )
    await waitFor(() => expect(screen.getByText('1')).toBeInTheDocument())
    expect(screen.getByText(/target 3/)).toBeInTheDocument()
    expect(screen.getByText(/2 to go/)).toBeInTheDocument()
  })
})

describe('ChartPortlet — minimum-data honesty', () => {
  it('renders the honest empty state below the minMonths threshold', async () => {
    identity([])
    vi.spyOn(client, 'getMetricSeries').mockResolvedValue({
      id: 'spendByMonth', label: 'Committed spend', unit: 'RM',
      buckets: [{ bucket: '2026-06', value: 0 }, { bucket: '2026-07', value: 1000 }], unbucketedCount: 0,
    })
    renderWithProviders(
      <ChartPortlet portlet={portlet('Chart', '{"seriesIds":["spendByMonth"],"months":12,"minMonths":3}')} onNavigate={() => {}} />,
    )
    await waitFor(() => expect(screen.getByTestId('chart-empty')).toBeInTheDocument())
    expect(screen.getByText(/Not enough history yet \(1 of 3 months/)).toBeInTheDocument()
  })
})

describe('ShortcutsPortlet — server-permission gating', () => {
  it('shows only shortcuts whose action the server granted', async () => {
    identity(['ManageRequisitions'])
    renderWithProviders(
      <ShortcutsPortlet portlet={portlet('Shortcuts', JSON.stringify({
        items: [
          { label: 'New requisition', route: 'reqs', action: 'ManageRequisitions' },
          { label: 'New user', route: 'admin', action: 'ManageUsers' },
        ],
      }))} onNavigate={() => {}} />,
    )
    await waitFor(() => expect(screen.getByText('New requisition')).toBeInTheDocument())
    expect(screen.queryByText('New user')).not.toBeInTheDocument()
  })
})

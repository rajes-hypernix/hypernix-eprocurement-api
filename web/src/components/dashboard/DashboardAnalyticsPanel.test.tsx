import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DashboardAnalyticsPanel } from './DashboardAnalyticsPanel'
import { RECENT_POS } from '../../mock/dashboardAnalytics'

describe('DashboardAnalyticsPanel (demo)', () => {
  it('renders the headline, both charts, and all recent-PO rows from mock data', () => {
    render(<DashboardAnalyticsPanel onNavigate={() => {}} />)

    // editorial headline + delta
    expect(screen.getByText('up 12.8%')).toBeInTheDocument()
    expect(screen.getByText(/Compared with this time last year/)).toBeInTheDocument()

    // both hand-rolled SVGs (labelled via role=img)
    expect(screen.getByRole('img', { name: /Committed spend by month/i })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: /New vendors onboarded/i })).toBeInTheDocument()

    // 5 PO rows, codes as plain text (not links)
    expect(RECENT_POS).toHaveLength(5)
    for (const po of RECENT_POS) expect(screen.getByText(po.code)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /PO-2026-0141/ })).not.toBeInTheDocument()
  })

  it('"View details / View all" links route to real pages', async () => {
    const onNavigate = vi.fn()
    render(<DashboardAnalyticsPanel onNavigate={onNavigate} />)
    await userEvent.click(screen.getByRole('button', { name: 'View details' }))       // → RFQ list
    const viewAll = screen.getAllByRole('button', { name: 'View all' })               // [0] vendors, [1] POs (DOM order)
    await userEvent.click(viewAll[0])
    await userEvent.click(viewAll[1])
    expect(onNavigate).toHaveBeenCalledWith('rfqs')
    expect(onNavigate).toHaveBeenCalledWith('vendors')
    expect(onNavigate).toHaveBeenCalledWith('pos')
  })

  it('toggling "Show forecast" hides the forecast legend entries', async () => {
    render(<DashboardAnalyticsPanel onNavigate={() => {}} />)
    expect(screen.getByText('Materials forecast')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Show forecast/i }))
    expect(screen.queryByText('Materials forecast')).not.toBeInTheDocument()
  })
})

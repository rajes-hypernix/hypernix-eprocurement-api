import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Sidebar } from './Sidebar'
import { BUYER_NAV } from '../nav'

describe('Sidebar', () => {
  it('renders every prototype nav group title', () => {
    render(<Sidebar active="dashboard" onSelect={() => {}} />)
    for (const title of [
      'Sourcing',
      'Procure to Pay',
      'Evaluation',
      'Setup',
      'Communication',
    ]) {
      expect(screen.getByText(title)).toBeInTheDocument()
    }
  })

  it('renders every nav item label from the model', () => {
    render(<Sidebar active="dashboard" onSelect={() => {}} />)
    for (const item of BUYER_NAV.flatMap((g) => g.items)) {
      expect(screen.getByText(item.label)).toBeInTheDocument()
    }
  })

  it('marks the active item and clears others', () => {
    render(<Sidebar active="rfqs" onSelect={() => {}} />)
    expect(screen.getByText('RFQs').closest('.nav')).toHaveClass('on')
    expect(screen.getByText('Dashboard').closest('.nav')).not.toHaveClass('on')
  })

  it('calls onSelect with the item key when clicked', async () => {
    const onSelect = vi.fn()
    render(<Sidebar active="dashboard" onSelect={onSelect} />)
    await userEvent.click(screen.getByText('Purchase Orders'))
    expect(onSelect).toHaveBeenCalledWith('pos')
  })
})

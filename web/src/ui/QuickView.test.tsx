import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QuickView } from './QuickView'

const fetcher = () => Promise.resolve({
  title: 'Acme Sdn Bhd',
  fields: [{ label: 'Code', value: 'VEN-0001' }, { label: 'Status', value: 'Registered' }],
})

describe('QuickView', () => {
  it('shows a compact summary after hover-intent', async () => {
    render(<QuickView fetcher={fetcher} delayMs={30}><span>Acme</span></QuickView>)
    await userEvent.hover(screen.getByText('Acme'))
    await waitFor(() => expect(screen.getByText('Acme Sdn Bhd')).toBeInTheDocument())
    expect(screen.getByText('VEN-0001')).toBeInTheDocument()
    expect(screen.getByRole('tooltip')).toBeInTheDocument()
  })

  it('cancels on quick pass-through (hover-intent) and closes on leave', async () => {
    const spy = vi.fn(fetcher)
    render(<QuickView fetcher={spy} delayMs={120}><span>Acme</span></QuickView>)
    await userEvent.hover(screen.getByText('Acme'))
    await userEvent.unhover(screen.getByText('Acme'))     // leave before the delay
    await new Promise((r) => setTimeout(r, 200))
    expect(spy).not.toHaveBeenCalled()
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument()
  })

  it('shows a graceful message when the fetch fails', async () => {
    render(<QuickView fetcher={() => Promise.reject(new Error('x'))} delayMs={10}><span>Acme</span></QuickView>)
    await userEvent.hover(screen.getByText('Acme'))
    await waitFor(() => expect(screen.getByText('Couldn’t load the preview.')).toBeInTheDocument())
  })
})

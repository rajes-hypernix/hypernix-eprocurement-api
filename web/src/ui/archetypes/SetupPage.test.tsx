import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SetupPage } from './SetupPage'

const ITEMS = [
  { key: 'COUNTRY', label: 'Country', hint: 'COUNTRY' },
  { key: 'STATE', label: 'State', hint: 'STATE · ↳ COUNTRY' },
]

describe('SetupPage', () => {
  it('renders pagehead, rail with hints, selection state, and the detail pane', () => {
    render(
      <SetupPage
        title="Custom Lists" subtitle="Reusable coded value sets."
        primaryAction={<button type="button">New list</button>}
        railItems={ITEMS} selectedKey="STATE" onSelect={() => {}}
        detail={<div>STATE DETAIL</div>}
      />)
    expect(screen.getByRole('heading', { name: 'Custom Lists' })).toBeInTheDocument()
    expect(screen.getByText('STATE · ↳ COUNTRY')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /State/ }).className).toContain('on')
    expect(screen.getByRole('button', { name: /Country/ }).className).not.toContain('on')
    expect(screen.getByText('STATE DETAIL')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New list' })).toBeInTheDocument()
  })

  it('rail selection fires onSelect; empty rail shows the empty hint', async () => {
    const onSelect = vi.fn()
    const { rerender } = render(
      <SetupPage title="T" railItems={ITEMS} selectedKey={null} onSelect={onSelect} detail={null} />)
    await userEvent.click(screen.getByRole('button', { name: /Country/ }))
    expect(onSelect).toHaveBeenCalledWith('COUNTRY')
    rerender(<SetupPage title="T" railItems={[]} selectedKey={null} onSelect={onSelect} detail={null} railEmpty="No lists yet." />)
    expect(screen.getByText('No lists yet.')).toBeInTheDocument()
  })

  it('renders the error notice', () => {
    render(<SetupPage title="T" error="boom" railItems={[]} selectedKey={null} onSelect={() => {}} detail={null} />)
    expect(screen.getByText('boom')).toBeInTheDocument()
  })
})

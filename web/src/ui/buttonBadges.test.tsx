import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Button, LinkButton } from './Button'
import { StatusBadge, Pill, Chip } from './badges'

describe('Button', () => {
  it('maps variants to the existing classes', () => {
    const { rerender } = render(<Button variant="primary">Go</Button>)
    expect(screen.getByRole('button').className).toBe('btn btn-pri')
    rerender(<Button variant="danger">Del</Button>)
    expect(screen.getByRole('button').className).toBe('btn btn-pri btn-danger')
    rerender(<Button variant="ghost" red size="sm">Remove</Button>)
    expect(screen.getByRole('button').className).toBe('btn btn-ghost btn-sm btn-red')
  })

  it('busy disables and sets aria-busy; click fires otherwise', async () => {
    const onClick = vi.fn()
    const { rerender } = render(<Button busy onClick={onClick}>Save</Button>)
    expect(screen.getByRole('button')).toBeDisabled()
    expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'true')
    rerender(<Button onClick={onClick}>Save</Button>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).toHaveBeenCalled()
  })

  it('defaults type=button (never submits by accident) and is keyboard focusable', async () => {
    render(<Button>Go</Button>)
    expect(screen.getByRole('button')).toHaveAttribute('type', 'button')
    await userEvent.tab()
    expect(screen.getByRole('button')).toHaveFocus()
  })

  it('LinkButton renders the .lnk idiom', () => {
    render(<LinkButton>Back</LinkButton>)
    expect(screen.getByRole('button', { name: 'Back' }).className).toBe('lnk')
  })
})

describe('Badges / pills / chips', () => {
  it('StatusBadge and Pill render the tone classes', () => {
    render(<><StatusBadge tone="amber">Open</StatusBadge><Pill tone="green">Matched</Pill></>)
    expect(screen.getByText('Open').className).toBe('badge b-amber')
    expect(screen.getByText('Matched').className).toBe('pill b-green')
  })

  it('Chip renders swchip, hit state, and remove affordance', async () => {
    const onRemove = vi.fn()
    render(<><Chip hit>SWEC 3.02</Chip><Chip onRemove={onRemove}>Gone</Chip></>)
    expect(screen.getByText(/SWEC 3.02/).className).toBe('swchip hit')
    await userEvent.click(screen.getByText(/Gone/))
    expect(onRemove).toHaveBeenCalled()
  })
})

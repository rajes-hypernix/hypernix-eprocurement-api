import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { EntityPage } from './EntityPage'
import { Stat, Kv } from '../display'

const TABS = [
  { key: 'overview', label: 'Overview', content: <div>OVERVIEW</div> },
  { key: 'audit', label: 'Audit', content: <div>AUDIT</div> },
]

describe('EntityPage', () => {
  it('renders crumb, master header with inline nodes, actions, and the first tab', () => {
    render(
      <EntityPage
        crumbParent="Vendor Master" onCrumbParent={() => {}} crumbCurrent="Acme Sdn Bhd"
        title={<>Acme Sdn Bhd <span className="badge b-teal">SWEC</span></>}
        subtitle={<>VEN-0001 · Sarawak</>}
        actions={<button type="button">Deactivate</button>}
        tabs={TABS}
      />)
    expect(screen.getByText('Vendor Master')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Acme Sdn Bhd/ })).toBeInTheDocument()
    expect(screen.getByText('SWEC')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Deactivate' })).toBeInTheDocument()
    expect(screen.getByText('OVERVIEW')).toBeInTheDocument()
  })

  it('uncontrolled tabs switch internally', async () => {
    render(<EntityPage crumbParent="P" onCrumbParent={() => {}} crumbCurrent="C" title="T" tabs={TABS} />)
    await userEvent.click(screen.getByRole('button', { name: 'Audit' }))
    expect(screen.getByText('AUDIT')).toBeInTheDocument()
    expect(screen.queryByText('OVERVIEW')).not.toBeInTheDocument()
  })

  it('controlled tabs report changes and follow the prop (query-gating pattern)', async () => {
    const onTabChange = vi.fn()
    const { rerender } = render(
      <EntityPage crumbParent="P" onCrumbParent={() => {}} crumbCurrent="C" title="T" tabs={TABS} tab="overview" onTabChange={onTabChange} />)
    await userEvent.click(screen.getByRole('button', { name: 'Audit' }))
    expect(onTabChange).toHaveBeenCalledWith('audit')
    expect(screen.getByText('OVERVIEW')).toBeInTheDocument()   // still controlled to overview
    rerender(<EntityPage crumbParent="P" onCrumbParent={() => {}} crumbCurrent="C" title="T" tabs={TABS} tab="audit" onTabChange={onTabChange} />)
    expect(screen.getByText('AUDIT')).toBeInTheDocument()
  })
})

describe('display primitives', () => {
  it('Stat renders the .card.stat idiom with optional tone', () => {
    render(<Stat label="Spend YTD" value="RM 1,250.00" sub="3 POs" tone="teal" />)
    const card = screen.getByText('Spend YTD').closest('.card')!
    expect(card.className).toContain('stat')
    expect(card.className).toContain('tone-teal')
    expect(screen.getByText('3 POs')).toBeInTheDocument()
  })

  it('Kv renders the .vkv idiom', () => {
    render(<Kv k="Vendor code" v="VEN-0001" />)
    expect(screen.getByText('Vendor code').className).toBe('hint')
    expect(screen.getByText('VEN-0001').className).toBe('vv')
  })
})

import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TransactionPage, type TransactionSection } from './TransactionPage'

const SECTION: TransactionSection = {
  title: 'Header',
  rows: [[
    { key: 'requestor', label: 'Requestor', dataType: 'text', placeholder: 'Name' },
    { key: 'requiredDate', label: 'Required by', dataType: 'date' },
  ]],
  fullWidth: [{ key: 'memo', label: 'Memo', dataType: 'text' }],
}

const page = (over: Partial<Parameters<typeof TransactionPage>[0]> = {}) => (
  <TransactionPage
    crumbParent="Requisitions" onCrumbParent={() => {}} crumbCurrent="PR-1"
    title="Edit PR-1" subtitle="sub" statusBadge={<span className="badge b-grey">Draft</span>}
    actions={<button type="button">Save</button>}
    sections={[SECTION]} values={{ requestor: 'QA', requiredDate: '2026-07-30', memo: '' }}
    onFieldChange={() => {}}
    {...over}
  />
)

afterEach(() => { window.location.hash = '' })

describe('TransactionPage', () => {
  it('renders crumb, title, badge, and the action bar at top AND bottom', () => {
    render(page())
    expect(screen.getByText('Requisitions')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Edit PR-1' })).toBeInTheDocument()
    expect(screen.getByText('Draft')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Save' })).toHaveLength(2)
  })

  it('renders sections from FieldSpec arrays through the one pipeline', () => {
    render(page())
    expect(screen.getByLabelText(/Requestor/)).toHaveValue('QA')
    expect(screen.getByLabelText(/Required by/)).toHaveAttribute('type', 'date')
    expect(screen.getByLabelText(/Memo/)).toBeInTheDocument()
  })

  it('field edits flow through onFieldChange with the spec key', async () => {
    const onFieldChange = vi.fn()
    render(page({ onFieldChange }))
    await userEvent.type(screen.getByLabelText(/Memo/), 'x')
    expect(onFieldChange).toHaveBeenCalledWith('memo', 'x')
  })

  it('shows the error notice and per-field errors', () => {
    render(page({ error: 'Could not save', fieldErrors: { requestor: 'required' } }))
    expect(screen.getByText('Could not save')).toBeInTheDocument()
    expect(screen.getByText('required')).toBeInTheDocument()
  })

  it('subtabs switch content', async () => {
    render(page({ tabs: [{ key: 'a', label: 'Lines', content: <div>LINES</div> }, { key: 'b', label: 'Audit', content: <div>AUDIT</div> }] }))
    expect(screen.getByText('LINES')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Audit' }))
    expect(screen.getByText('AUDIT')).toBeInTheDocument()
    expect(screen.queryByText('LINES')).not.toBeInTheDocument()
  })

  it('dirty guard: declining the confirm restores the hash; markClean bypasses', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    let clean: () => void = () => {}
    window.location.hash = '#reqs/p1'
    render(page({ dirty: true, onGuardReady: (fn) => { clean = fn } }))
    window.location.hash = '#dashboard'
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    expect(confirm).toHaveBeenCalled()
    expect(window.location.hash).toBe('#reqs/p1')

    clean()                                       // intentional navigation (save → back)
    window.location.hash = '#dashboard'
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    expect(window.location.hash).toBe('#dashboard')
    confirm.mockRestore()
  })

  it('dirty guard: accepting the confirm allows navigation', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
    window.location.hash = '#reqs/p1'
    render(page({ dirty: true }))
    window.location.hash = '#dashboard'
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    expect(window.location.hash).toBe('#dashboard')
    confirm.mockRestore()
  })

  it('clean forms never prompt', () => {
    const confirm = vi.spyOn(window, 'confirm')
    window.location.hash = '#reqs/p1'
    render(page({ dirty: false }))
    window.location.hash = '#dashboard'
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    expect(confirm).not.toHaveBeenCalled()
    confirm.mockRestore()
  })
})

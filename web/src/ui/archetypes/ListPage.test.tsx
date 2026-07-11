import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ListPage, type ListColumn, type ListFacet } from './ListPage'

type Row = { id: string; code: string; title: string; status: string; qty: number; locked?: boolean }
const ROWS: Row[] = [
  { id: '1', code: 'RFQ-001', title: 'Cables', status: 'Open', qty: 3 },
  { id: '2', code: 'RFQ-002', title: 'Valves', status: 'Draft', qty: 1, locked: true },
  { id: '3', code: 'RFQ-003', title: 'Pumps', status: 'Open', qty: 2 },
]
const COLS: ListColumn<Row>[] = [
  { key: 'code', header: 'Code', render: (r) => r.code },
  { key: 'title', header: 'Title', render: (r) => r.title },
  { key: 'qty', header: 'Qty', amt: true, render: (r) => r.qty, sortValue: (r) => r.qty },
]
const FACETS: ListFacet<Row>[] = [{ key: 'status', label: 'Status', value: (r) => r.status }]

const page = (over: Partial<Parameters<typeof ListPage<Row>>[0]> = {}) => (
  <ListPage<Row>
    title="RFQs" subtitle="All sourcing events."
    searchLabel="RFQ #" searchPlaceholder="number or title…" searchMatch={(r, q) => [r.code, r.title].some((v) => v.toLowerCase().includes(q))}
    facets={FACETS} rows={ROWS} rowKey={(r) => r.id} columns={COLS}
    emptyNone="No RFQs yet." emptyFiltered="No RFQs match these filters."
    {...over}
  />
)

describe('ListPage', () => {
  it('renders pagehead, all rows, and slots', () => {
    render(page({ primaryAction: <button type="button">New RFQ</button>, viewToggle: <div className="viewtoggle">VT</div> }))
    expect(screen.getByRole('heading', { name: 'RFQs' })).toBeInTheDocument()
    expect(screen.getByText('RFQ-001')).toBeInTheDocument()
    expect(screen.getByText('RFQ-003')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New RFQ' })).toBeInTheDocument()
    expect(screen.getByText('VT')).toBeInTheDocument()
  })

  it('search filters rows; empty-filtered vs empty-none states', async () => {
    const { rerender } = render(page())
    await userEvent.type(screen.getByLabelText('RFQ #'), 'valves')
    expect(screen.getByText('RFQ-002')).toBeInTheDocument()
    expect(screen.queryByText('RFQ-001')).not.toBeInTheDocument()
    await userEvent.clear(screen.getByLabelText('RFQ #'))
    await userEvent.type(screen.getByLabelText('RFQ #'), 'zzz')
    expect(screen.getByText('No RFQs match these filters.')).toBeInTheDocument()
    rerender(page({ rows: [] }))
    expect(screen.getByText('No RFQs yet.')).toBeInTheDocument()
  })

  it('facet multi-select filters (values derived from data) and Reset clears', async () => {
    render(page())
    await userEvent.click(screen.getByRole('button', { name: 'Filter by Status' }))
    await userEvent.click(screen.getByLabelText('Draft'))
    expect(screen.getByText('RFQ-002')).toBeInTheDocument()
    expect(screen.queryByText('RFQ-001')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Reset' }))
    expect(screen.getByText('RFQ-001')).toBeInTheDocument()
  })

  it('sorting is opt-in: unsortable headers are plain, sortable toggles asc/desc/off', async () => {
    render(page())
    expect(screen.queryByRole('button', { name: 'Code' })).not.toBeInTheDocument()   // default off
    const qtyHeader = screen.getByRole('button', { name: 'Qty' })
    await userEvent.click(qtyHeader)
    let cells = screen.getAllByRole('row').slice(1).map((r) => r.textContent)
    expect(cells[0]).toContain('RFQ-002')      // qty 1 first (asc)
    await userEvent.click(screen.getByRole('button', { name: /Qty ↑/ }))
    cells = screen.getAllByRole('row').slice(1).map((r) => r.textContent)
    expect(cells[0]).toContain('RFQ-001')      // qty 3 first (desc)
  })

  it('bulk select (F31): select-all is indeterminate on partial and skips unselectable rows', async () => {
    const onChange = vi.fn()
    const { rerender } = render(page({
      bulkSelect: { selected: new Set<string>(), onChange, selectable: (r) => !r.locked, ariaLabel: (r) => `Select ${r.code}` },
    }))
    expect(screen.getByLabelText('Select RFQ-002')).toBeDisabled()   // locked row
    await userEvent.click(screen.getByLabelText('Select all'))
    expect([...(onChange.mock.calls[0][0] as Set<string>)].sort()).toEqual(['1', '3'])  // skips locked '2'

    rerender(page({ bulkSelect: { selected: new Set(['1']), onChange, selectable: (r) => !r.locked } }))
    const all = screen.getByLabelText('Select all') as HTMLInputElement
    expect(all.indeterminate).toBe(true)
  })

  it('row actions render in the last cell; alternateBody replaces the table with filtered rows', async () => {
    const { rerender } = render(page({ rowActions: (r) => <button type="button">Open {r.code}</button> }))
    expect(screen.getByRole('button', { name: 'Open RFQ-001' })).toBeInTheDocument()
    rerender(page({ alternateBody: (rows) => <div>BOARD {rows.length}</div> }))
    expect(screen.getByText('BOARD 3')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})

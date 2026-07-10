import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AnswerCell } from './answers'

describe('AnswerCell (#7 — matrix/group answers render as a grid, not raw JSON)', () => {
  const tableCfg = JSON.stringify({ columns: ['FY2023', 'FY2024', 'FY2025'], rows: ['Revenue', 'Cost', 'Profit'], cellType: 'money' })
  // BidForm serialises a table as rows × cols: data[rowIndex][colIndex].
  const tableVal = JSON.stringify([['100', '120', '140'], ['60', '70', '80'], ['40', '50', '60']])

  it('renders a matrix answer with column + row headers and cell values', () => {
    render(<AnswerCell type="table" value={tableVal} config={tableCfg} />)
    // headers
    expect(screen.getByText('FY2024')).toBeInTheDocument()
    expect(screen.getByText('Revenue')).toBeInTheDocument()
    expect(screen.getByText('Profit')).toBeInTheDocument()
    // a specific cell (Profit / FY2025 = 60)
    const table = screen.getByRole('table')
    expect(table.textContent).toContain('140')
    expect(table.textContent).toContain('40')
    // must NOT show the raw JSON
    expect(screen.queryByText(/\[\[/)).not.toBeInTheDocument()
  })

  it('renders a repeatable-group answer as labelled entries', () => {
    const cfg = JSON.stringify({ fields: [{ label: 'Project', type: 'short_text' }, { label: 'Value', type: 'money' }] })
    const val = JSON.stringify([['Jetty upgrade', '2,000,000'], ['Pipe rack', '850,000']])
    render(<AnswerCell type="group" value={val} config={cfg} />)
    expect(screen.getAllByText('Project:')).toHaveLength(2)
    expect(screen.getByText('Jetty upgrade')).toBeInTheDocument()
    expect(screen.getByText('Pipe rack')).toBeInTheDocument()
    expect(screen.queryByText(/\[\[/)).not.toBeInTheDocument()
  })

  it('falls back to a dash for an empty answer', () => {
    render(<AnswerCell type="table" value="" config={tableCfg} />)
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('still renders plain scalar answers as text', () => {
    render(<AnswerCell type="short_text" value="Hello" />)
    expect(screen.getByText('Hello')).toBeInTheDocument()
  })
})

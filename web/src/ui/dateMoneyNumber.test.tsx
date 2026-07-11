import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { NumberField } from './NumberField'
import { MoneyField } from './MoneyField'
import { DateField } from './DateField'
import type { FieldSpec } from './fieldSpec'

const spec = (over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key: 'k', label: 'Qty', dataType: 'number', ...over })

describe('NumberField', () => {
  it('renders right-aligned number input; min/max come from validation (the qty guards)', () => {
    render(<NumberField spec={spec({ validation: { min: 0, max: 40 } })} value="25" onChange={() => {}} />)
    const input = screen.getByLabelText(/Qty/)
    expect(input).toHaveAttribute('type', 'number')
    expect(input).toHaveAttribute('min', '0')
    expect(input).toHaveAttribute('max', '40')
    expect(input.className).toContain('amt')
  })

  it('unit renders as a suffix; percent defaults to %', () => {
    const { rerender } = render(<NumberField spec={spec({ unit: 'weeks' })} value="" onChange={() => {}} />)
    expect(screen.getByText('weeks')).toBeInTheDocument()
    rerender(<NumberField spec={spec({ dataType: 'percent' })} value="" onChange={() => {}} />)
    expect(screen.getByText('%')).toBeInTheDocument()
  })

  it('commitOnBlur posts only on blur with a non-empty value (EvalScreens cells)', async () => {
    const onCommit = vi.fn()
    const onChange = vi.fn()
    render(<NumberField spec={spec({ label: 'Score', validation: { min: 0, max: 100 } })} value="88" onChange={onChange} commitOnBlur onCommit={onCommit} />)
    const input = screen.getByLabelText(/Score/)
    await userEvent.click(input)
    expect(onCommit).not.toHaveBeenCalled()
    fireEvent.blur(input)
    expect(onCommit).toHaveBeenCalledWith('88')
  })

  it('commitOnBlur does not post an empty value', () => {
    const onCommit = vi.fn()
    render(<NumberField spec={spec({ label: 'Score' })} value="" onChange={() => {}} commitOnBlur onCommit={onCommit} />)
    fireEvent.blur(screen.getByLabelText(/Score/))
    expect(onCommit).not.toHaveBeenCalled()
  })

  it('disabled and error states', () => {
    render(<NumberField spec={spec({ disabled: true })} value="" onChange={() => {}} error="required" />)
    expect(screen.getByLabelText(/Qty/)).toBeDisabled()
    expect(screen.getByText('required')).toBeInTheDocument()
  })
})

describe('MoneyField', () => {
  const mspec = spec({ dataType: 'money', label: 'Unit price' })

  it('value stays a raw numeric string per keystroke (live totals keep working)', async () => {
    const onChange = vi.fn()
    render(<MoneyField spec={mspec} value="" onChange={onChange} />)
    const input = screen.getByLabelText(/Unit price/)
    await userEvent.type(input, '5')
    expect(onChange).toHaveBeenCalledWith('5')
  })

  it('sanitises non-numeric characters (the PrForm regex, now in one place)', () => {
    const onChange = vi.fn()
    render(<MoneyField spec={mspec} value="" onChange={onChange} />)
    fireEvent.change(screen.getByLabelText(/Unit price/), { target: { value: 'RM1,2a50.75' } })
    expect(onChange).toHaveBeenCalledWith('1250.75')
  })

  it('formats for DISPLAY on blur; raw string while focused', async () => {
    render(<MoneyField spec={mspec} value="1250.5" onChange={() => {}} />)
    const input = screen.getByLabelText(/Unit price/)
    expect(input).toHaveValue('1,250.50')       // unfocused: formatted display
    await userEvent.click(input)
    expect(input).toHaveValue('1250.5')          // focused: raw for editing
    fireEvent.blur(input)
    expect(input).toHaveValue('1,250.50')
  })

  it('currency affordance renders from spec.currencyCode', () => {
    render(<MoneyField spec={{ ...mspec, currencyCode: 'MYR' }} value="" onChange={() => {}} />)
    expect(screen.getByText('MYR')).toBeInTheDocument()
  })

  it('readOnly renders the .ro treatment', () => {
    render(<MoneyField spec={{ ...mspec, readOnly: true }} value="100" onChange={() => {}} />)
    const input = screen.getByLabelText(/Unit price/)
    expect(input).toHaveAttribute('readonly')
    expect(input.className).toContain('ro')
  })
})

describe('DateField', () => {
  const dspec = spec({ dataType: 'date', label: 'Required by' })

  it('consumes ISO (bare yyyy-MM-dd and datetime both) and emits ISO date', () => {
    const onChange = vi.fn()
    render(<DateField spec={dspec} value="2026-07-15T00:00:00" onChange={onChange} />)
    const input = screen.getByLabelText(/Required by/)
    expect(input).toHaveValue('2026-07-15')      // Slice H T4 wire shape, date part
    fireEvent.change(input, { target: { value: '2026-08-01' } })
    expect(onChange).toHaveBeenCalledWith('2026-08-01')
  })

  it('readOnly renders dd/MM/yyyy via fmtDay', () => {
    render(<DateField spec={{ ...dspec, readOnly: true }} value="2026-07-15" onChange={() => {}} />)
    expect(screen.getByLabelText(/Required by/)).toHaveValue('15/07/2026')
  })

  it('dateTime renders datetime-local with the timezone affordance and a min floor', () => {
    render(
      <DateField
        spec={{ ...dspec, dataType: 'dateTime', label: 'New close', validation: { min: '2026-07-20T04:00:00Z' } }}
        value="2026-07-21T04:00:00Z" onChange={() => {}}
      />)
    const input = screen.getByLabelText(/New close/)
    expect(input).toHaveAttribute('type', 'datetime-local')
    expect(input).toHaveAttribute('min')
    expect(screen.getByText('local time')).toBeInTheDocument()
  })

  it('error display', () => {
    render(<DateField spec={dspec} value="" onChange={() => {}} error="required" />)
    expect(screen.getByText('required')).toBeInTheDocument()
  })
})

import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TextField } from './TextField'
import { TextAreaField } from './TextAreaField'
import { CodeField } from './CodeField'
import type { FieldSpec } from './fieldSpec'

const spec = (over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key: 'name', label: 'Company name', dataType: 'text', ...over })

describe('TextField (chrome + states)', () => {
  it('renders from spec with label association and typed input reaches onChange', async () => {
    const onChange = vi.fn()
    render(<TextField spec={spec({ placeholder: 'e.g. Acme' })} value="" onChange={onChange} />)
    const input = screen.getByLabelText(/Company name/)
    expect(input).toHaveAttribute('placeholder', 'e.g. Acme')
    await userEvent.type(input, 'A')
    expect(onChange).toHaveBeenCalledWith('A')
  })

  it('is keyboard focusable', async () => {
    render(<TextField spec={spec()} value="" onChange={() => {}} />)
    await userEvent.tab()
    expect(screen.getByLabelText(/Company name/)).toHaveFocus()
  })

  it('required shows the * marker; help renders as hint', () => {
    render(<TextField spec={spec({ required: true, help: 'As registered with SSM' })} value="" onChange={() => {}} />)
    expect(screen.getByText('*')).toBeInTheDocument()
    expect(screen.getByText('As registered with SSM')).toBeInTheDocument()
  })

  it('disabled and readOnly states apply; readOnly gets the .ro treatment', () => {
    const { rerender } = render(<TextField spec={spec({ disabled: true })} value="x" onChange={() => {}} />)
    expect(screen.getByLabelText(/Company name/)).toBeDisabled()
    rerender(<TextField spec={spec({ readOnly: true })} value="x" onChange={() => {}} />)
    const ro = screen.getByLabelText(/Company name/)
    expect(ro).toHaveAttribute('readonly')
    expect(ro.className).toContain('ro')
  })

  it('error renders in the chrome and sets aria-invalid', () => {
    render(<TextField spec={spec()} value="" onChange={() => {}} error="required" />)
    expect(screen.getByText('required')).toBeInTheDocument()
    expect(screen.getByLabelText(/Company name/)).toHaveAttribute('aria-invalid', 'true')
  })

  it('bare chrome renders no visible label but keeps aria-label', () => {
    render(<TextField spec={spec()} value="" onChange={() => {}} chrome="bare" />)
    expect(document.querySelector('label')).toBeNull()
    expect(screen.getByLabelText('Company name')).toBeInTheDocument()
  })

  it('dataType email renders type=email; displayType hidden renders nothing', () => {
    const { rerender, container } = render(
      <TextField spec={spec({ dataType: 'email' })} value="" onChange={() => {}} />)
    expect(screen.getByLabelText(/Company name/)).toHaveAttribute('type', 'email')
    rerender(<TextField spec={spec({ displayType: 'hidden' })} value="" onChange={() => {}} />)
    expect(container.querySelector('input')).toBeNull()
  })

  it('reserved displayType values no-op to normal (D7 seam)', () => {
    render(<TextField spec={spec({ displayType: 'future-thing' })} value="" onChange={() => {}} />)
    const input = screen.getByLabelText(/Company name/)
    expect(input).not.toBeDisabled()
    expect(input).not.toHaveAttribute('readonly')
  })
})

describe('TextAreaField', () => {
  it('renders a textarea from spec with label association and rows', async () => {
    const onChange = vi.fn()
    render(<TextAreaField spec={spec({ dataType: 'longText', label: 'Reason' })} value="" onChange={onChange} rows={2} />)
    const ta = screen.getByLabelText(/Reason/)
    expect(ta.tagName).toBe('TEXTAREA')
    expect(ta).toHaveAttribute('rows', '2')
    await userEvent.type(ta, 'x')
    expect(onChange).toHaveBeenCalledWith('x')
  })

  it('error + disabled states', () => {
    render(<TextAreaField spec={spec({ dataType: 'longText', label: 'Reason', disabled: true })} value="" onChange={() => {}} error="required" />)
    expect(screen.getByLabelText(/Reason/)).toBeDisabled()
    expect(screen.getByText('required')).toBeInTheDocument()
  })
})

describe('CodeField', () => {
  it('renders monospace (.mono) and readOnly for persisted codes', () => {
    render(<CodeField spec={spec({ dataType: 'code', label: 'Value code', readOnly: true })} value="NET30" onChange={() => {}} />)
    const input = screen.getByLabelText(/Value code/)
    expect(input.className).toContain('mono')
    expect(input).toHaveAttribute('readonly')
    expect(input).toHaveValue('NET30')
  })
})

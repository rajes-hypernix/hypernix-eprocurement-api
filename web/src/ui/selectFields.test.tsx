import { describe, it, expect, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../test/utils'
import { SelectField } from './SelectField'
import { DependentSelectField } from './DependentSelectField'
import { MultiSelectField } from './MultiSelectField'
import { YesNoField } from './YesNoField'
import { SegmentedField } from './SegmentedField'
import { CheckboxField } from './CheckboxField'
import type { FieldSpec } from './fieldSpec'

// Custom Lists as the API serves them (getCustomLists) — the dependent chain
// mirrors the seeded COUNTRY → STATE → CITY conformed dimensions.
vi.mock('../api/client', async (importOriginal) => ({
  ...(await importOriginal<object>()),
  getCustomLists: () => Promise.resolve([
    {
      id: '1', code: 'STATE', name: 'State', description: null, parentListCode: 'COUNTRY', isSystem: true,
      values: [
        { id: 's1', code: 'SWK', label: 'Sarawak', parentValueCode: 'MY', sort: 1, active: true },
        { id: 's2', code: 'SGR', label: 'Selangor', parentValueCode: 'MY', sort: 2, active: true },
        { id: 's3', code: 'OLD', label: 'Retired', parentValueCode: 'MY', sort: 3, active: false },
      ],
    },
    {
      id: '2', code: 'PAYMENT_TERMS', name: 'Payment terms', description: null, parentListCode: null, isSystem: true,
      values: [
        { id: 'p1', code: 'NET30', label: 'Net 30', parentValueCode: null, sort: 1, active: true },
        { id: 'p2', code: 'NET60', label: 'Net 60', parentValueCode: null, sort: 2, active: true },
      ],
    },
  ]),
}))

const spec = (over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key: 'k', label: 'Field', dataType: 'select', ...over })

describe('SelectField', () => {
  it('renders static options, stores the CODE, placeholder as empty option', async () => {
    const onChange = vi.fn()
    renderWithQuery(
      <SelectField
        spec={spec({ label: 'Registration type', placeholder: 'Select type', options: { kind: 'static', options: [{ code: 'SWEC', label: 'PETRONAS SWEC' }, { code: 'Non-SWEC', label: 'Non-SWEC' }] } })}
        value="" onChange={onChange}
      />)
    const sel = screen.getByLabelText(/Registration type/)
    expect(screen.getByText('Select type')).toBeInTheDocument()
    await userEvent.selectOptions(sel, 'SWEC')
    expect(onChange).toHaveBeenCalledWith('SWEC')
  })

  it('renders grouped static options as optgroups (topic-select pattern)', () => {
    renderWithQuery(
      <SelectField
        spec={spec({ label: 'Topic', options: { kind: 'static', options: [{ label: 'Specific RFQ', options: [{ code: 'RFQ-1', label: 'RFQ-2026-0001' }] }] } })}
        value="" onChange={() => {}}
      />)
    expect(screen.getByRole('group', { name: 'Specific RFQ' })).toBeInTheDocument()
  })

  it('resolves customList options (active only, sorted, code stored)', async () => {
    const onChange = vi.fn()
    renderWithQuery(
      <SelectField spec={spec({ label: 'Payment terms', options: { kind: 'customList', listCode: 'PAYMENT_TERMS' } })} value="NET30" onChange={onChange} />)
    expect(await screen.findByText('Net 60')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText(/Payment terms/), 'NET60')
    expect(onChange).toHaveBeenCalledWith('NET60')
  })

  it('disabled state and error display', () => {
    renderWithQuery(
      <SelectField spec={spec({ label: 'S', disabled: true, options: { kind: 'static', options: [] } })} value="" onChange={() => {}} error="required" />)
    expect(screen.getByLabelText(/S/)).toBeDisabled()
    expect(screen.getByText('required')).toBeInTheDocument()
  })
})

describe('DependentSelectField (ParentValueCode)', () => {
  const dspec = spec({ label: 'State / Region', options: { kind: 'customList', listCode: 'STATE', parentField: 'country' }, placeholder: 'Select state' })

  it('filters options to the parent value and excludes inactive values', async () => {
    renderWithQuery(<DependentSelectField spec={dspec} value="" onChange={() => {}} parentValue="MY" />)
    expect(await screen.findByText('Sarawak')).toBeInTheDocument()
    expect(screen.getByText('Selangor')).toBeInTheDocument()
    expect(screen.queryByText('Retired')).not.toBeInTheDocument()
  })

  it('degrades to a free-text input when the parent has no children', async () => {
    const onChange = vi.fn()
    renderWithQuery(<DependentSelectField spec={dspec} value="" onChange={onChange} parentValue="SG" />)
    // hasChildren('STATE','SG') is false in the fixture → text input, not select
    const input = await screen.findByLabelText(/State \/ Region/)
    expect(input.tagName).toBe('INPUT')
    await userEvent.type(input, 'W')
    expect(onChange).toHaveBeenCalledWith('W')
  })
})

describe('MultiSelectField', () => {
  it('toggles pipe-joined codes (the stored answer format)', async () => {
    const onChange = vi.fn()
    renderWithQuery(
      <MultiSelectField
        spec={spec({ dataType: 'multiSelect', label: 'Roles', options: { kind: 'static', options: [{ code: 'Buyer', label: 'Buyer' }, { code: 'Admin', label: 'Admin' }] } })}
        value="Buyer" onChange={onChange}
      />)
    expect(screen.getByLabelText('Buyer')).toBeChecked()
    await userEvent.click(screen.getByLabelText('Admin'))
    expect(onChange).toHaveBeenCalledWith('Buyer|Admin')
    await userEvent.click(screen.getByLabelText('Buyer'))
    expect(onChange).toHaveBeenCalledWith('')
  })
})

describe('YesNoField', () => {
  it('offers — / Yes / No and stores the string', async () => {
    const onChange = vi.fn()
    renderWithQuery(<YesNoField spec={spec({ dataType: 'yesNo', label: 'ISO certified?' })} value="" onChange={onChange} />)
    await userEvent.selectOptions(screen.getByLabelText(/ISO certified/), 'Yes')
    expect(onChange).toHaveBeenCalledWith('Yes')
  })
})

describe('SegmentedField', () => {
  it('renders radio-semantics buttons with the btn-pri/btn-out idiom', async () => {
    const onChange = vi.fn()
    renderWithQuery(
      <SegmentedField
        spec={spec({ dataType: 'segmented', label: 'Envelope', options: { kind: 'static', options: [{ code: 'Single', label: 'Single' }, { code: 'Dual', label: 'Dual' }] } })}
        value="Single" onChange={onChange}
      />)
    const single = screen.getByRole('radio', { name: 'Single' })
    const dual = screen.getByRole('radio', { name: 'Dual' })
    expect(single).toHaveAttribute('aria-checked', 'true')
    expect(single.className).toContain('btn-pri')
    expect(dual.className).toContain('btn-out')
    await userEvent.click(dual)
    expect(onChange).toHaveBeenCalledWith('Dual')
  })
})

describe('CheckboxField', () => {
  it('boolean value, label after box, keyboard focusable', async () => {
    const onChange = vi.fn()
    renderWithQuery(<CheckboxField spec={spec({ dataType: 'boolean', label: 'Active (offered in dropdowns)' })} value={true} onChange={onChange} />)
    const box = screen.getByLabelText(/Active/)
    expect(box).toBeChecked()
    await userEvent.tab()
    expect(box).toHaveFocus()
    await userEvent.click(box)
    expect(onChange).toHaveBeenCalledWith(false)
  })

  it('bare chrome renders only the box with aria-label', () => {
    renderWithQuery(<CheckboxField spec={spec({ dataType: 'boolean', label: 'Bid' })} value={false} onChange={() => {}} chrome="bare" />)
    expect(document.querySelector('label')).toBeNull()
    expect(screen.getByLabelText('Bid')).toBeInTheDocument()
  })
})

import { describe, it, expect, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { renderWithQuery } from '../test/utils'
import userEvent from '@testing-library/user-event'
import { SearchSelectField } from './SearchSelectField'
import type { FieldSpec } from './fieldSpec'

// CF-FIX1-T7: the searchable select — type-to-filter + the KEYBOARD contract the
// operator gated (arrows, enter, escape), plus the multi variant's pipe-joined value.

const spec = (over: Partial<FieldSpec> = {}): FieldSpec => ({
  key: 'ss', label: 'Fruit', dataType: 'select', searchable: true,
  options: { kind: 'static', options: [
    { code: 'APPLE', label: 'Apple' }, { code: 'BANANA', label: 'Banana' },
    { code: 'CHERRY', label: 'Cherry' }, { code: 'DATE', label: 'Date Fruit' },
  ] },
  ...over,
})

describe('SearchSelectField', () => {
  it('type-to-filter narrows the options and click selects', async () => {
    const onChange = vi.fn()
    renderWithQuery(<SearchSelectField spec={spec()} value="" onChange={onChange} />)
    await userEvent.click(screen.getByRole('button', { name: 'Fruit' }))
    await userEvent.type(screen.getByRole('combobox', { name: 'Search Fruit' }), 'ban')
    expect(screen.getAllByRole('option')).toHaveLength(1)
    await userEvent.click(screen.getByRole('option', { name: 'Banana' }))
    expect(onChange).toHaveBeenCalledWith('BANANA')
  })

  it('keyboard: arrows move the highlight, Enter selects, Escape closes without selecting', async () => {
    const onChange = vi.fn()
    renderWithQuery(<SearchSelectField spec={spec()} value="" onChange={onChange} />)
    await userEvent.click(screen.getByRole('button', { name: 'Fruit' }))
    const box = screen.getByRole('combobox', { name: 'Search Fruit' })
    await userEvent.keyboard('{ArrowDown}{ArrowDown}{Enter}')          // → Cherry (index 2)
    expect(onChange).toHaveBeenCalledWith('CHERRY')

    onChange.mockClear()
    await userEvent.click(screen.getByRole('button', { name: 'Fruit' }))
    await userEvent.keyboard('{Escape}')
    expect(onChange).not.toHaveBeenCalled()
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()      // closed
    void box
  })

  it('multi variant toggles selections into a pipe-joined value and stays open', async () => {
    const onChange = vi.fn()
    renderWithQuery(<SearchSelectField spec={spec({ dataType: 'multiSelect' })} value="APPLE" onChange={onChange} />)
    await userEvent.click(screen.getByRole('button', { name: 'Fruit' }))
    await userEvent.click(screen.getByRole('option', { name: /Cherry/ }))
    expect(onChange).toHaveBeenCalledWith('APPLE|CHERRY')
    expect(screen.getByRole('listbox')).toBeInTheDocument()            // multi keeps the list open
    await userEvent.click(screen.getByRole('option', { name: /Apple/ }))
    expect(onChange).toHaveBeenCalledWith('')                          // toggle off
  })
})

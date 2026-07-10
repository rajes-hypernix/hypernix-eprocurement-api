import { describe, it, expect, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QuestionEditor } from './QuestionEditor'
import { QTYPES, newItemId, type EditItem, type FormSections } from '../../lib/formTypes'

const NO_SECTIONS: FormSections = { technical: [], commercial: [] }

describe('QuestionEditor', () => {
  it('renders both sealed-envelope groups', () => {
    render(<QuestionEditor items={[]} sections={NO_SECTIONS} onChange={() => {}} />)
    expect(screen.getByText('Commercial')).toBeInTheDocument()
    expect(screen.getByText('Technical')).toBeInTheDocument()
  })

  it('offers all 12 field types when an item is expanded', async () => {
    const item: EditItem = {
      id: newItemId(), kind: 'question', group: 'technical', section: '', label: 'Q', type: 'short_text',
      required: false, config: {}, help: '',
    }
    render(<QuestionEditor items={[item]} sections={NO_SECTIONS} onChange={() => {}} />)
    await userEvent.click(screen.getByText('Q'))   // expand the collapsed item
    const selects = screen.getAllByRole('combobox') as HTMLSelectElement[]
    const typeSelect = selects.find((s) => s.options.length === QTYPES.length)!
    expect(typeSelect).toBeTruthy()
    for (const [, label] of QTYPES) {
      expect(within(typeSelect).getByRole('option', { name: label })).toBeInTheDocument()
    }
  })

  it('adds a question to a group via the add-menu', async () => {
    const onChange = vi.fn()
    render(<QuestionEditor items={[]} sections={NO_SECTIONS} onChange={onChange} />)
    // Commercial group is first; click its "Add item" then pick "Short text".
    await userEvent.click(screen.getAllByRole('button', { name: /Add item/i })[0])
    await userEvent.click(screen.getByRole('button', { name: 'Short text' }))
    expect(onChange).toHaveBeenCalledWith(
      [expect.objectContaining({ kind: 'question', group: 'commercial', type: 'short_text' })],
      NO_SECTIONS,
    )
  })

  it('adds a sub-group section to a group', async () => {
    const onChange = vi.fn()
    render(<QuestionEditor items={[]} sections={NO_SECTIONS} onChange={onChange} />)
    await userEvent.click(screen.getAllByRole('button', { name: /Sub-group/i })[0])
    expect(onChange).toHaveBeenCalledWith([], { technical: [], commercial: ['New sub-group'] })
  })
})

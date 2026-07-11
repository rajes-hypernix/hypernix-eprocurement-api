import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AttachmentField } from './AttachmentField'
import { TableField } from './TableField'
import { RepeatingGroupField } from './RepeatingGroupField'
import type { FieldSpec } from './fieldSpec'

vi.mock('../api/client', async (importOriginal) => ({
  ...(await importOriginal<object>()),
  uploadFile: vi.fn((f: File) => Promise.resolve({ id: 'f1', name: f.name })),
  fileUrl: (id: string) => `/api/files/${id}`,
}))

const spec = (over: Partial<FieldSpec> = {}): FieldSpec =>
  ({ key: 'k', label: 'Answer', dataType: 'attachment', ...over })

describe('AttachmentField', () => {
  it('uploads through the existing client and stores id::name pairs', async () => {
    const onChange = vi.fn()
    render(<AttachmentField spec={spec()} value="" onChange={onChange} />)
    expect(screen.getByText('No file chosen.')).toBeInTheDocument()
    const input = screen.getByLabelText('Answer')
    await userEvent.upload(input, new File(['x'], 'cert.pdf', { type: 'application/pdf' }))
    await waitFor(() => expect(onChange).toHaveBeenCalledWith('f1::cert.pdf'))
  })

  it('renders stored files as download links with remove buttons', () => {
    render(<AttachmentField spec={spec()} value="f1::cert.pdf" onChange={() => {}} />)
    const link = screen.getByRole('link', { name: 'cert.pdf' })
    expect(link).toHaveAttribute('href', '/api/files/f1')
    expect(screen.getByText('PDF')).toBeInTheDocument()
    expect(screen.getByLabelText('Remove cert.pdf')).toBeInTheDocument()
  })

  it('single-file replaces; multiple appends (config.multiple)', async () => {
    const onChange = vi.fn()
    render(<AttachmentField spec={spec({ config: { multiple: true } })} value="f0::old.pdf" onChange={onChange} />)
    await userEvent.upload(screen.getByLabelText('Answer'), new File(['x'], 'new.pdf'))
    await waitFor(() => expect(onChange).toHaveBeenCalledWith('f0::old.pdf|f1::new.pdf'))
  })

  it('readOnly hides upload and remove affordances', () => {
    render(<AttachmentField spec={spec({ readOnly: true })} value="f1::cert.pdf" onChange={() => {}} />)
    expect(screen.queryByText(/Choose file/)).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Remove cert.pdf')).not.toBeInTheDocument()
  })
})

describe('TableField', () => {
  const tspec = spec({
    dataType: 'table', label: 'Financials',
    config: { columns: ['FY2024', 'FY2025'], rows: ['Revenue', 'Profit'], cellType: 'money' },
  })

  it('renders the rows×columns grid and serialises cell edits as JSON', async () => {
    const onChange = vi.fn()
    render(<TableField spec={tspec} value="" onChange={onChange} />)
    expect(screen.getByText('FY2025')).toBeInTheDocument()
    const cell = screen.getByLabelText('Revenue FY2024')
    expect(cell).toHaveAttribute('type', 'number')
    await userEvent.type(cell, '5')
    expect(onChange).toHaveBeenCalledWith(JSON.stringify([['5', ''], ['', '']]))
  })

  it('disabled locks every cell', () => {
    render(<TableField spec={{ ...tspec, disabled: true }} value="" onChange={() => {}} />)
    expect(screen.getByLabelText('Profit FY2025')).toBeDisabled()
  })
})

describe('RepeatingGroupField', () => {
  const gspec = spec({
    dataType: 'group', label: 'Track record',
    config: { fields: [{ label: 'Project', type: 'short_text' }, { label: 'Value', type: 'money' }], max: 2 },
  })

  it('adds entries up to max and serialises field edits', async () => {
    const onChange = vi.fn()
    const { rerender } = render(<RepeatingGroupField spec={gspec} value="" onChange={onChange} />)
    await userEvent.click(screen.getByText(/Add entry \(0\/2\)/))
    expect(onChange).toHaveBeenCalledWith(JSON.stringify([['', '']]))
    rerender(<RepeatingGroupField spec={gspec} value={JSON.stringify([['', '']])} onChange={onChange} />)
    await userEvent.type(screen.getByLabelText('Project entry 1'), 'J')
    expect(onChange).toHaveBeenCalledWith(JSON.stringify([['J', '']]))
    expect(screen.getByLabelText('Value entry 1')).toHaveAttribute('type', 'number')
  })

  it('caps at config.max and removes entries', async () => {
    const onChange = vi.fn()
    render(<RepeatingGroupField spec={gspec} value={JSON.stringify([['a', '1'], ['b', '2']])} onChange={onChange} />)
    expect(screen.queryByText(/Add entry/)).not.toBeInTheDocument()
    await userEvent.click(screen.getByLabelText('Remove entry 1'))
    expect(onChange).toHaveBeenCalledWith(JSON.stringify([['b', '2']]))
  })
})

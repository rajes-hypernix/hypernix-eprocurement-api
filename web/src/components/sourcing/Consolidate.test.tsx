import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { Consolidate } from './Consolidate'
import * as client from '../../api/client'

const line = (id: string, itemCode: string, qty: number, uom: string, status = 'Open') => ({
  id, itemCode, description: `${itemCode} desc`, qty, uom, estUnitPrice: 100,
  status: status === 'Open' ? 'available' : 'rfq', ref: null, lifecycleStatus: status, noQuotes: false, editable: status === 'Open',
})
const PRS = [
  {
    id: 'pa', code: 'PR-2026-0701', requestor: 'Aishah', department: 'Ops', location: 'Bintulu', category: 'Piping',
    memo: '', job: 'J1', costCentre: 'C1', project: null, raisedDate: '2026-06-01', requiredDate: '2026-07-30',
    status: 'Approved', value: 0, headerStatus: 'Submitted', submitted: true,
    lines: [line('a-merge', 'ITEM-MERGE', 4, 'Unit'), line('a-uom', 'ITEM-UOM', 2, 'Unit'), line('a-lock', 'ITEM-LOCK', 1, 'Unit', 'InRfq')],
  },
  {
    id: 'pb', code: 'PR-2026-0702', requestor: 'Ramesh', department: 'Production', location: 'Samalaju', category: 'Valves',
    memo: '', job: 'J2', costCentre: 'C2', project: null, raisedDate: '2026-06-02', requiredDate: '2026-07-25',
    status: 'Approved', value: 0, headerStatus: 'Submitted', submitted: true,
    lines: [line('b-merge', 'ITEM-MERGE', 6, 'Unit'), line('b-uom', 'ITEM-UOM', 3, 'Box')],
  },
]

// A Draft PR with an Open line — must be gated OUT of the sourcing pool (Bug 2).
const DRAFT_PR = {
  id: 'pd', code: 'PR-2026-0434', requestor: 'Draft', department: 'Electrical', location: 'B', category: 'Electrical',
  memo: '', job: 'J', costCentre: 'C', project: null, raisedDate: '', requiredDate: '2026-01-01',
  status: 'Draft', value: 0, headerStatus: 'Draft', submitted: false,
  lines: [line('d1', 'DRAFT-ITEM', 5, 'Unit')],
}

describe('Consolidate Lines (Slice C)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getRequisitions').mockResolvedValue(PRS as never)
    vi.spyOn(client, 'reservePrLine').mockResolvedValue(PRS[0] as never)
    vi.spyOn(client, 'unreservePrLine').mockResolvedValue(PRS[0] as never)
  })

  it('Bug 1 — expanding/collapsing a PR toggles its lines in normal flow', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    // default expanded → the line's add-button is present
    expect(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeInTheDocument()
    // collapsing the PR header hides its lines (they render in-flow, not as an overlay)
    await userEvent.click(screen.getByText('PR-2026-0701'))
    expect(screen.queryByLabelText('Add ITEM-MERGE from PR-2026-0701')).not.toBeInTheDocument()
  })

  it('Bug 2 — a Draft PR is excluded from the Consolidate pool', async () => {
    vi.spyOn(client, 'getRequisitions').mockResolvedValue([...PRS, DRAFT_PR] as never)
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await waitFor(() => expect(screen.getByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeInTheDocument())
    expect(screen.queryByText('PR-2026-0434')).not.toBeInTheDocument()               // Draft PR absent
    expect(screen.queryByLabelText('Add DRAFT-ITEM from PR-2026-0434')).not.toBeInTheDocument()
  })

  it('D1 — left pane shows only OPEN lines; locked lines are absent', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await waitFor(() => expect(screen.getByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeInTheDocument())
    expect(screen.getByLabelText('Add ITEM-UOM from PR-2026-0701')).toBeInTheDocument()
    expect(screen.queryByLabelText('Add ITEM-LOCK from PR-2026-0701')).not.toBeInTheDocument()  // InRfq absent
  })

  it('D2/D4 — same item code + same UoM prompts Merge → one merged line with provenance', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await userEvent.click(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0701'))
    await userEvent.click(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0702'))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()      // merge prompt is a modal now
    expect(screen.getByText('Merge into one line?')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Merge \(sum qty\)/ }))

    expect(await screen.findByText('Merged from 2 PRs')).toBeInTheDocument()
    expect(screen.getByText('10 Unit')).toBeInTheDocument()   // 4 + 6 summed
  })

  it('D3 — same item code + different UoM auto keep-separate with a note', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await userEvent.click(await screen.findByLabelText('Add ITEM-UOM from PR-2026-0701'))
    await userEvent.click(await screen.findByLabelText('Add ITEM-UOM from PR-2026-0702'))

    expect(await screen.findByText(/different UoM/)).toBeInTheDocument()
    expect(screen.getByText('from PR-2026-0701')).toBeInTheDocument()
    expect(screen.getByText('from PR-2026-0702')).toBeInTheDocument()   // two separate basket lines
  })

  it('Expand all / Collapse all toggle every PR group', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    expect(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Collapse all' }))
    expect(screen.queryByLabelText('Add ITEM-MERGE from PR-2026-0701')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Add ITEM-MERGE from PR-2026-0702')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Expand all' }))
    expect(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeInTheDocument()
  })

  it('Add all confirms every merge group in one modal, then merges on confirm', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await userEvent.click(await screen.findByRole('button', { name: /Add all/ }))

    // ITEM-MERGE is in both PRs at the same UoM → the bulk confirmation lists it (not silent).
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('ITEM-MERGE')).toBeInTheDocument()
    expect(within(dialog).getByText(/10 Unit/)).toBeInTheDocument()   // 4 + 6 summed shown in the preview
    await userEvent.click(within(dialog).getByRole('button', { name: /Merge & add all/ }))

    expect(await screen.findByText('Merged from 2 PRs')).toBeInTheDocument()
    // every open line is now claimed (its add button disabled)
    expect(screen.getByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeDisabled()
    expect(screen.getByLabelText('Add ITEM-UOM from PR-2026-0702')).toBeDisabled()
  })

  it('Add all — Keep separate adds every line without merging', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await userEvent.click(await screen.findByRole('button', { name: /Add all/ }))

    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: /Keep separate/ }))

    // no merge happened — ITEM-MERGE stays two separate basket lines
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.queryByText('Merged from 2 PRs')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeDisabled()
    expect(screen.getByLabelText('Add ITEM-MERGE from PR-2026-0702')).toBeDisabled()
  })

  it('Add all — Cancel adds nothing', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await userEvent.click(await screen.findByRole('button', { name: /Add all/ }))

    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByLabelText('Add ITEM-MERGE from PR-2026-0701')).toBeEnabled()   // nothing added
    expect(client.reservePrLine).not.toHaveBeenCalled()
  })

  it('Add all respects the current filter (only the filtered PRs are added)', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0701')
    // filter to Production (PR-2026-0702 only)
    await userEvent.click(screen.getByRole('button', { name: 'Filter by Dept' }))
    await userEvent.click(screen.getByLabelText('Production'))
    expect(screen.queryByLabelText('Add ITEM-MERGE from PR-2026-0701')).not.toBeInTheDocument()  // PR-A filtered out

    await userEvent.click(screen.getByRole('button', { name: /Add all/ }))
    expect(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0702')).toBeDisabled()       // PR-B line added
    expect(client.reservePrLine).not.toHaveBeenCalledWith('pa', expect.anything())                // PR-A never reserved
  })

  it('item 4 — Clear empties the basket and keeps the PR lines available in the pool', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    const addBtn = await screen.findByLabelText('Add ITEM-UOM from PR-2026-0701')
    await userEvent.click(addBtn)
    expect(await screen.findByLabelText('Add ITEM-UOM from PR-2026-0701')).toBeDisabled()  // now in basket (greyed)

    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByLabelText('Add ITEM-UOM from PR-2026-0701')).toBeEnabled()   // back in the pool, not gone
  })

  it('D6 — removing a basket line calls unreserve (returns the source to Open)', async () => {
    renderWithProviders(<Consolidate onOpenRfq={() => {}} onBack={() => {}} />)
    await userEvent.click(await screen.findByLabelText('Add ITEM-UOM from PR-2026-0701'))
    await userEvent.click(await screen.findByLabelText('Remove ITEM-UOM'))
    await waitFor(() => expect(client.unreservePrLine).toHaveBeenCalledWith('pa', 'a-uom'))
  })

  it('D7 — Build RFQ hands the basket to the existing wizard with provenance', async () => {
    const create = vi.spyOn(client, 'createRfqDraft').mockResolvedValue({ id: 'rfq-new' } as never)
    const onOpen = vi.fn()
    renderWithProviders(<Consolidate onOpenRfq={onOpen} onBack={() => {}} />)
    await userEvent.click(await screen.findByLabelText('Add ITEM-MERGE from PR-2026-0701'))
    await userEvent.click(screen.getByRole('button', { name: /Build RFQ/i }))

    await waitFor(() => expect(create).toHaveBeenCalled())
    const arg = create.mock.calls[0][0]
    expect(arg.lines?.[0]).toMatchObject({ itemCode: 'ITEM-MERGE', sourcePrLineIds: ['a-merge'] })
    await waitFor(() => expect(onOpen).toHaveBeenCalledWith('rfq-new'))
  })
})

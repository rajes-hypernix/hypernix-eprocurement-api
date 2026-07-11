import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../test/utils'
import { GlobalSearch, NewMenu, RecentsMenu } from './TopBarNav'
import { getRecents, recordRecent } from '../lib/recents'
import * as client from '../api/client'

const HITS: client.SearchHit[] = [
  { type: 'Rfq', id: 'r1', code: 'RFQ-2026-0001', title: 'Cables' },
  { type: 'Rfq', id: 'r2', code: 'RFQ-2026-0002', title: 'Valves' },
  { type: 'PurchaseOrder', id: 'p1', code: 'PO-2026-0001', title: 'Acme' },
]

const asBuyer = () => vi.spyOn(client, 'getPersonas').mockResolvedValue([
  { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'] } as never,
])
const asAdmin = () => vi.spyOn(client, 'getPersonas').mockResolvedValue([
  { code: 'u_admin', name: 'Admin', kind: 'internal', roles: ['Admin'] } as never,
])

beforeEach(() => {
  vi.restoreAllMocks()
  localStorage.clear()
  window.history.replaceState(null, '', '?as=u_faridah')
})

describe('GlobalSearch', () => {
  it('debounces, groups results by record type, renders mono codes', async () => {
    asBuyer()
    const search = vi.spyOn(client, 'searchGlobal').mockResolvedValue(HITS)
    renderWithProviders(<GlobalSearch onNavigate={() => {}} />)
    await userEvent.type(screen.getByLabelText('Global search'), 'RFQ')
    await waitFor(() => expect(screen.getByText('RFQs')).toBeInTheDocument())
    expect(search).toHaveBeenCalledWith('RFQ')
    expect(screen.getByText('Purchase Orders')).toBeInTheDocument()
    expect(screen.getByText('RFQ-2026-0001').className).toContain('mono')
  })

  it('keyboard navigation: arrows move the active option, Enter opens and records a recent', async () => {
    asBuyer()
    vi.spyOn(client, 'searchGlobal').mockResolvedValue(HITS)
    const onNavigate = vi.fn()
    renderWithProviders(<GlobalSearch onNavigate={onNavigate} />)
    const input = screen.getByLabelText('Global search')
    await userEvent.type(input, 'RFQ')
    await waitFor(() => expect(screen.getByText('RFQs')).toBeInTheDocument())
    await userEvent.keyboard('{ArrowDown}{Enter}')
    expect(onNavigate).toHaveBeenCalledWith('rfqs/r2')          // second option after one ArrowDown
    expect(getRecents('u_faridah')[0]).toMatchObject({ type: 'Rfq', code: 'RFQ-2026-0002', hash: 'rfqs/r2' })
  })

  it('short queries do not hit the API', async () => {
    asBuyer()
    const search = vi.spyOn(client, 'searchGlobal').mockResolvedValue([])
    renderWithProviders(<GlobalSearch onNavigate={() => {}} />)
    await userEvent.type(screen.getByLabelText('Global search'), 'R')
    await new Promise((r) => setTimeout(r, 350))
    expect(search).not.toHaveBeenCalled()
  })
})

describe('NewMenu (<Gated> — display gating only)', () => {
  it('a Buyer sees buyer creates but no admin creates', async () => {
    asBuyer()
    renderWithProviders(<NewMenu onNavigate={() => {}} />)
    await waitFor(() => expect(screen.getByRole('button', { name: /New/ })).toBeInTheDocument())
    await userEvent.click(screen.getByRole('button', { name: /New/ }))
    expect(await screen.findByRole('menuitem', { name: /Purchase Requisition/ })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /Vendor invitation/ })).toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: /User/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: /Custom list/ })).not.toBeInTheDocument()
  })

  it('an Admin sees admin creates but no buyer creates; routes to existing screens', async () => {
    asAdmin()
    window.history.replaceState(null, '', '?as=u_admin')
    const onNavigate = vi.fn()
    renderWithProviders(<NewMenu onNavigate={onNavigate} />)
    await waitFor(() => expect(screen.getByRole('button', { name: /New/ })).toBeInTheDocument())
    await userEvent.click(screen.getByRole('button', { name: /New/ }))
    const userItem = await screen.findByRole('menuitem', { name: /User/ })
    expect(screen.queryByRole('menuitem', { name: /Purchase Requisition/ })).not.toBeInTheDocument()
    await userEvent.click(userItem)
    expect(onNavigate).toHaveBeenCalledWith('admin')
  })
})

describe('RecentsMenu', () => {
  it('renders recents per principal with mono codes and navigates on click', async () => {
    asBuyer()
    recordRecent('u_faridah', { type: 'Rfq', code: 'RFQ-2026-0009', hash: 'rfqs/r9' })
    recordRecent('someone_else', { type: 'Rfq', code: 'RFQ-OTHER', hash: 'rfqs/x' })
    const onNavigate = vi.fn()
    renderWithProviders(<RecentsMenu onNavigate={onNavigate} />)
    await userEvent.click(screen.getByRole('button', { name: /Recent/ }))
    expect(screen.getByText('RFQ-2026-0009')).toBeInTheDocument()
    expect(screen.queryByText('RFQ-OTHER')).not.toBeInTheDocument()   // per-principal trail
    await userEvent.click(screen.getByRole('menuitem', { name: /RFQ-2026-0009/ }))
    expect(onNavigate).toHaveBeenCalledWith('rfqs/r9')
  })

  it('caps the list at 8 with most-recent first', () => {
    for (let i = 1; i <= 10; i++) recordRecent('u_faridah', { type: 'Rfq', code: `RFQ-${i}`, hash: `rfqs/${i}` })
    const rec = getRecents('u_faridah')
    expect(rec).toHaveLength(8)
    expect(rec[0].code).toBe('RFQ-10')
  })
})

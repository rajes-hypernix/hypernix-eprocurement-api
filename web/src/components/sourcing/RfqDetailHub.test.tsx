import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen } from '@testing-library/react'
import { renderWithProviders } from '../../test/utils'
import { RfqDetailHub } from './RfqDetailHub'
import * as client from '../../api/client'
import type { RfqDetail, CustomList } from '../../api/client'

type Inv = NonNullable<RfqDetail['invitations']>[number]
const inv = (vendorId: string, vendorName: string, status: string, extra: Partial<Inv> = {}): Inv =>
  ({ vendorId, vendorName, status, invitedUtc: '2026-07-01T00:00:00Z', ...extra })

const rfq: RfqDetail = {
  id: 'r1', code: 'RFQ-2026-0089', title: 'Valves', envelope: 'Single', status: 'Open', currency: 'MYR',
  opensUtc: '2026-06-25T00:00:00Z', closesUtc: '2026-07-10T00:00:00Z', originalClosesUtc: '2026-07-08T00:00:00Z',
  extensionCount: 1, prRefs: [], invitedVendorIds: [], technicalEvaluatorIds: [], commercialEvaluatorIds: [],
  techFinalized: false, commercialOpened: false, lines: [], formItems: [], technicalSections: [], commercialSections: [],
  invitedVendors: [{ vendorId: 'v1', vendorName: 'Alpha', category: 'Pumps', submitted: true }],
  invitations: [
    inv('v1', 'Alpha', 'BidSubmitted'),
    inv('v2', 'Bravo', 'Invited'),
    inv('v3', 'Charlie', 'Viewed'),
    inv('v4', 'Delta', 'IntendToBid'),
    inv('v5', 'Echo', 'Declined', { declineReasonCode: 'CAPACITY', declineNote: 'no slots' }),
    inv('v6', 'Foxtrot', 'Rescinded', { rescindReasonCode: 'DUPLICATE' }),
  ],
  events: [{ eventType: 'Extended', oldClosesUtc: '2026-07-08T00:00:00Z', newClosesUtc: '2026-07-10T00:00:00Z', actorUserId: 'u_faridah', occurredUtc: '2026-07-02T00:00:00Z' }],
}

const reasonLists: CustomList[] = [
  { id: 'l', code: 'RFQ_DECLINE_REASON', name: 'x', description: null, parentListCode: null, isSystem: true, values: [{ id: 'c', code: 'CAPACITY', label: 'No capacity in timeframe', parentValueCode: null, sort: 0, active: true }] },
]

describe('RfqDetailHub — invitation lifecycle (Slice J)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getPersonas').mockResolvedValue([])
    vi.spyOn(client, 'getCustomLists').mockResolvedValue(reasonLists)
  })

  it('renders all six invitation statuses; Rescinded is struck through', () => {
    renderWithProviders(<RfqDetailHub rfq={rfq} onBack={() => {}} onNavigate={() => {}} />)
    for (const t of ['Bid submitted', 'Invited', 'Viewed', 'Intends to bid', 'Declined', 'Rescinded'])
      expect(screen.getByText(t)).toBeInTheDocument()
    // Rescinded vendor name is struck through
    expect(screen.getByText('Foxtrot')).toHaveStyle({ textDecoration: 'line-through' })
  })

  it('offers Rescind only for pre-bid rows, never for a submitted bid', () => {
    renderWithProviders(<RfqDetailHub rfq={rfq} onBack={() => {}} onNavigate={() => {}} />)
    const rescindButtons = screen.getAllByRole('button', { name: 'Rescind' })
    // Invited, Viewed, IntendToBid, Declined = 4 rescindable; BidSubmitted + Rescinded excluded.
    expect(rescindButtons).toHaveLength(4)
  })

  it('shows Add vendor + Extend deadline while Open, and the extension count', () => {
    renderWithProviders(<RfqDetailHub rfq={rfq} onBack={() => {}} onNavigate={() => {}} />)
    expect(screen.getByRole('button', { name: /Add vendor/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Extend deadline/ })).toBeInTheDocument()
    expect(screen.getByText('extended 1×')).toBeInTheDocument()
    expect(screen.getByText('Deadline extended')).toBeInTheDocument()   // activity timeline
  })
})

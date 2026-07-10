import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { InvitationStatusBadge, ReasonModal, ExtendModal, ActivityTimeline } from './RfqGovernance'
import * as client from '../../api/client'
import type { CustomList } from '../../api/client'

const list = (code: string, ...vals: [string, string][]): CustomList => ({
  id: code, code, name: code, description: null, parentListCode: null, isSystem: true,
  values: vals.map(([c, l], i) => ({ id: c, code: c, label: l, parentValueCode: null, sort: i, active: true })),
})
const REASONS: CustomList[] = [
  list('RFQ_RESCIND_REASON', ['DUPLICATE', 'Invited in error / duplicate'], ['SCOPE_CHANGE', 'Requirement changed']),
  list('RFQ_EXTENSION_REASON', ['LOW_RESPONSE', 'Insufficient responses']),
  list('RFQ_DECLINE_REASON', ['CAPACITY', 'No capacity in timeframe']),
]

describe('RfqGovernance components', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getCustomLists').mockResolvedValue(REASONS)
  })

  it('InvitationStatusBadge renders every lifecycle status', () => {
    for (const [s, text] of [
      ['Invited', 'Invited'], ['Viewed', 'Viewed'], ['IntendToBid', 'Intends to bid'],
      ['Declined', 'Declined'], ['BidSubmitted', 'Bid submitted'], ['Rescinded', 'Rescinded'],
    ] as const) {
      const { unmount } = renderWithQuery(<InvitationStatusBadge status={s} />)
      expect(screen.getByText(text)).toBeInTheDocument()
      unmount()
    }
  })

  it('ReasonModal requires a reason before confirm is enabled', async () => {
    const onSubmit = vi.fn()
    renderWithQuery(<ReasonModal title="Rescind invitation" listCode="RFQ_RESCIND_REASON" confirmLabel="Rescind" onCancel={() => {}} onSubmit={onSubmit} />)
    const confirm = await screen.findByRole('button', { name: 'Rescind' })
    expect(confirm).toBeDisabled()                                   // no reason selected yet
    await waitFor(() => expect(screen.getByRole('option', { name: 'Invited in error / duplicate' })).toBeInTheDocument())
    await userEvent.selectOptions(screen.getByLabelText('Reason code'), 'DUPLICATE')
    expect(confirm).toBeEnabled()
    await userEvent.click(confirm)
    expect(onSubmit).toHaveBeenCalledWith('DUPLICATE', null)
  })

  it('ExtendModal blocks a backward date and enables a forward one', async () => {
    const onSubmit = vi.fn()
    renderWithQuery(<ExtendModal currentClosesUtc="2026-07-10T00:00:00Z" originalClosesUtc="2026-07-10T00:00:00Z" extensionCount={0} maxExtensions={2} onCancel={() => {}} onSubmit={onSubmit} />)
    const extend = await screen.findByRole('button', { name: 'Extend' })
    expect(extend).toBeDisabled()
    // a backward date keeps it disabled and shows the inline hint
    await userEvent.type(screen.getByLabelText('New close date and time'), '2026-07-05T00:00')
    expect(extend).toBeDisabled()
    expect(screen.getByText(/Must be later than the current close/)).toBeInTheDocument()
    // a forward date + reason enables it
    await userEvent.clear(screen.getByLabelText('New close date and time'))
    await userEvent.type(screen.getByLabelText('New close date and time'), '2026-07-20T00:00')
    await userEvent.selectOptions(screen.getByLabelText('Extension reason'), 'LOW_RESPONSE')
    expect(extend).toBeEnabled()
  })

  it('ExtendModal disables submission when the server cap is reached', async () => {
    renderWithQuery(<ExtendModal currentClosesUtc="2026-07-10T00:00:00Z" originalClosesUtc="2026-07-01T00:00:00Z" extensionCount={2} maxExtensions={2} onCancel={() => {}} onSubmit={() => {}} />)
    expect(await screen.findByText(/Extension 2 of 2/)).toBeInTheDocument()
    expect(await screen.findByText(/reached the maximum of 2/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Extend' })).toBeDisabled()
  })

  // T9: with no server cap supplied the client shows no limit and never invents one — it omits the
  // "of N" text and does not gate on a fabricated cap (the server still enforces G5).
  it('ExtendModal omits the cap and never blocks on a client-invented limit when maxExtensions is absent', async () => {
    renderWithQuery(<ExtendModal currentClosesUtc="2026-07-10T00:00:00Z" originalClosesUtc="2026-07-01T00:00:00Z" extensionCount={5} onCancel={() => {}} onSubmit={() => {}} />)
    expect(await screen.findByText(/Extension 6\b/)).toBeInTheDocument()
    expect(screen.queryByText(/Extension 6 of/)).not.toBeInTheDocument()
    expect(screen.queryByText(/reached the maximum/)).not.toBeInTheDocument()
  })

  it('ActivityTimeline shows an empty state and renders events with old→new close', () => {
    const { unmount } = renderWithQuery(<ActivityTimeline events={[]} />)
    expect(screen.getByText('No activity yet.')).toBeInTheDocument()
    unmount()
    renderWithQuery(<ActivityTimeline events={[
      { eventType: 'Extended', oldClosesUtc: '2026-07-10T00:00:00Z', newClosesUtc: '2026-07-15T00:00:00Z', actorUserId: 'u_faridah', occurredUtc: '2026-07-02T00:00:00Z' },
      { eventType: 'VendorDeclined', vendorName: 'Acme', reasonCode: 'CAPACITY', occurredUtc: '2026-07-01T00:00:00Z' },
    ]} />)
    expect(screen.getByText('Deadline extended')).toBeInTheDocument()
    expect(screen.getByText(/10\/07\/2026 → 15\/07\/2026/)).toBeInTheDocument()
    expect(screen.getByText('Vendor declined')).toBeInTheDocument()
  })
})

import { describe, it, expect, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useLookups } from './lookups'

// TEST-SWEEP-T2 (inventory PART 7): an INACTIVE value is absent from new-entry options
// while its stored code still resolves for display — history never degrades to a raw code.

vi.mock('../api/client', () => ({
  getCustomLists: vi.fn().mockResolvedValue([
    {
      code: 'PAYTERM', name: 'Payment terms', active: true, orderMode: 'Entered',
      values: [
        { code: 'NET30', label: 'Net 30', sort: 0, active: true, parentValueCode: null },
        { code: 'NET90', label: 'Net 90 (retired)', sort: 1, active: false, parentValueCode: null },
      ],
    },
  ]),
}))

const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
)

describe('lookups — inactive value semantics', () => {
  it('excludes an inactive value from options but still resolves its label for display', async () => {
    const { result } = renderHook(() => useLookups(), { wrapper })
    await waitFor(() => expect(result.current.isPending).toBe(false))

    expect(result.current.of('PAYTERM').map((o) => o.code)).toEqual(['NET30'])   // no NET90 for new entry
    expect(result.current.labelOf('PAYTERM', 'NET90')).toBe('Net 90 (retired)') // stored code still shows its label
  })
})

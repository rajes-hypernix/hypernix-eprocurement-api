import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithQuery } from '../test/utils'
import { HealthBadge } from './HealthBadge'
import * as client from '../api/client'

describe('HealthBadge', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('shows healthy status when the API reports the DB connected', async () => {
    vi.spyOn(client, 'getHealth').mockResolvedValue({
      status: 'ok',
      database: 'connected',
      timeUtc: '2026-06-28T00:00:00Z',
    })
    renderWithQuery(<HealthBadge />)
    await waitFor(() =>
      expect(screen.getByText(/API healthy/)).toBeInTheDocument(),
    )
    expect(screen.getByText(/Database connected/)).toBeInTheDocument()
  })

  it('shows an error state when the API is unreachable', async () => {
    vi.spyOn(client, 'getHealth').mockRejectedValue(new Error('boom'))
    renderWithQuery(<HealthBadge />)
    await waitFor(() =>
      expect(screen.getByText(/API unreachable/)).toBeInTheDocument(),
    )
  })
})

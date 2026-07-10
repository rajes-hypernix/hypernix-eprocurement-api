import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithQuery } from '../../test/utils'
import { VendorMaster } from './VendorMaster'
import * as client from '../../api/client'

describe('VendorMaster', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getSwec').mockResolvedValue([])
    vi.spyOn(client, 'getVendors').mockResolvedValue([
      {
        id: 'v1',
        code: 'SWK-V-10293',
        name: 'Sentausa Engineering Sdn Bhd',
        type: 'SWEC',
        categories: ['40101800P'],
        region: 'Peninsular',
        state: 'Selangor',
        rating: 4.6,
        otd: 96,
        status: 'Registered',
      },
    ])
  })

  it('lists seeded vendors with type, rating, OTD and status', async () => {
    renderWithQuery(<VendorMaster onOpen={() => {}} />)
    await waitFor(() =>
      expect(screen.getByText('Sentausa Engineering Sdn Bhd')).toBeInTheDocument(),
    )
    expect(screen.getByText('SWK-V-10293')).toBeInTheDocument()
    // "SWEC" appears in both the Type filter option and the row badge.
    expect(screen.getAllByText('SWEC').length).toBeGreaterThan(0)
    expect(screen.getByText('Registered')).toBeInTheDocument()
    expect(screen.getByText('96%')).toBeInTheDocument()
  })

  it('renders the prototype filter controls', () => {
    renderWithQuery(<VendorMaster onOpen={() => {}} />)
    expect(screen.getByText('Vendor Master')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('name or SWK-V-…')).toBeInTheDocument()
  })
})

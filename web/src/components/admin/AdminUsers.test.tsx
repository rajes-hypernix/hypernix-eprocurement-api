import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithQuery } from '../../test/utils'
import { AdminUsers } from './AdminUsers'
import * as client from '../../api/client'

describe('AdminUsers', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(client, 'getUsers').mockResolvedValue([
      { id: 'u_lim', code: 'u_lim', name: 'Lim Chee Kong', email: 'lim@hypernix.test', roles: ['Buyer', 'Approver'], isActive: true },
    ])
    vi.spyOn(client, 'getVendorLogins').mockResolvedValue([
      { id: 'vu1', code: 'VU-sentausa', vendorName: 'Sentausa Engineering Sdn Bhd', name: 'Sentausa — Portal', email: 'vendor@sentausa.test', isActive: true },
    ])
  })

  it('lists internal users with roles and vendor logins separately', async () => {
    renderWithQuery(<AdminUsers />)
    await waitFor(() => expect(screen.getByText('Lim Chee Kong')).toBeInTheDocument())
    expect(screen.getByText('Approver (DoA)')).toBeInTheDocument()
    expect(screen.getByText('vendor@sentausa.test')).toBeInTheDocument()
    expect(screen.getByText('Sentausa Engineering Sdn Bhd')).toBeInTheDocument()
  })

  it('edit modal offers internal roles but NOT the Vendor role (SoD)', async () => {
    renderWithQuery(<AdminUsers />)
    await waitFor(() => expect(screen.getByText('Lim Chee Kong')).toBeInTheDocument())
    await userEvent.click(screen.getByRole('button', { name: /Add user/i }))
    // "Buyer" also appears as a chip in the table; the modal adds another.
    expect(screen.getAllByText('Buyer').length).toBeGreaterThan(0)
    expect(screen.getByText('Commercial Evaluator')).toBeInTheDocument()
    // The Vendor role must not be selectable for internal users (SoD).
    expect(screen.queryByText('Vendor', { exact: true })).not.toBeInTheDocument()
  })
})

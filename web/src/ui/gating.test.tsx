import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '../test/utils'
import { Gated } from './gating'
import * as client from '../api/client'

/**
 * Slice RM Phase 3 pin: display gating renders from the SERVER's permission list
 * (GET /api/auth/permissions, AUTHORIZATION-MATRIX A58) — the static role map is dead.
 * Same persona, different server list → different render. If anyone reintroduces a
 * client-side role map, the second case fails.
 */

const persona = () => vi.spyOn(client, 'getPersonas').mockResolvedValue([
  { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'] } as never,
])

beforeEach(() => {
  vi.restoreAllMocks()
  window.history.replaceState(null, '', '?as=u_faridah')
})

describe('<Gated> derives from the server permission list', () => {
  it('shows the affordance when the server grants the action', async () => {
    persona()
    vi.spyOn(client, 'getPermissions').mockResolvedValue(['ManageRequisitions'])
    renderWithProviders(<Gated action="createPr"><button>New PR</button></Gated>)
    await waitFor(() => expect(screen.getByRole('button', { name: 'New PR' })).toBeInTheDocument())
  })

  it('hides the affordance when the server withholds it — even for the same persona roles', async () => {
    persona()
    vi.spyOn(client, 'getPermissions').mockResolvedValue([])   // Buyer persona, but server says no
    renderWithProviders(
      <>
        <Gated action="createPr"><button>New PR</button></Gated>
        <span>rendered</span>
      </>,
    )
    await waitFor(() => expect(screen.getByText('rendered')).toBeInTheDocument())
    expect(screen.queryByRole('button', { name: 'New PR' })).not.toBeInTheDocument()
  })

  it('maps the T8 re-invite onto the same server action as invite (matrix A27)', async () => {
    persona()
    vi.spyOn(client, 'getPermissions').mockResolvedValue(['InviteVendorToRfq'])
    renderWithProviders(
      <>
        <Gated action="reInviteVendor"><button>Re-invite</button></Gated>
        <Gated action="inviteVendorToRfq"><button>Invite</button></Gated>
      </>,
    )
    await waitFor(() => expect(screen.getByRole('button', { name: 'Re-invite' })).toBeInTheDocument())
    expect(screen.getByRole('button', { name: 'Invite' })).toBeInTheDocument()
  })
})

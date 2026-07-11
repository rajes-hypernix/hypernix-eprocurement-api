import { createContext, useContext, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getPersonas, getPermissions, getDemoUser, setDemoUser, type PersonaDto } from './api/client'

interface IdentityCtx {
  code: string
  persona: PersonaDto | undefined
  personas: PersonaDto[]
  isVendor: boolean
  roles: string[]
  /** The caller's allowed actions, fetched once post-identity from GET /api/auth/permissions
   *  (AUTHORIZATION-MATRIX A58). Display gating derives from THIS list — never a client map. */
  permissions: string[]
  switchTo: (code: string) => void
}

const Ctx = createContext<IdentityCtx | null>(null)

export function IdentityProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const [code, setCode] = useState(() => {
    const fromUrl = new URLSearchParams(window.location.search).get('as')
    if (fromUrl) setDemoUser(fromUrl)
    return getDemoUser()
  })
  const { data: personas = [] } = useQuery({ queryKey: ['personas'], queryFn: getPersonas, staleTime: Infinity })
  // Keyed by persona code so a switch refetches the new principal's action list (switchTo also
  // clears the cache wholesale — same rule as every other identity-scoped query).
  const { data: permissions = [] } = useQuery({
    queryKey: ['permissions', code], queryFn: getPermissions, staleTime: Infinity,
  })

  const persona = personas.find((p) => p.code === code)
  const switchTo = (next: string) => {
    setDemoUser(next)
    setCode(next)
    // Land on the new persona's home screen (buyer vs vendor navs differ).
    window.location.hash = 'dashboard'
    // All server data is identity-scoped. Drop the whole cache (not just invalidate) so
    // no screen can render another principal's stale data before the refetch resolves.
    qc.clear()
  }

  const value: IdentityCtx = {
    code,
    persona,
    personas,
    isVendor: persona?.kind === 'vendor',
    roles: persona?.roles?.filter((r): r is string => !!r) ?? [],
    permissions,
    switchTo,
  }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export function useIdentity(): IdentityCtx {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useIdentity must be used within IdentityProvider')
  return ctx
}

import { createContext, useContext, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getPersonas, getDemoUser, setDemoUser, type PersonaDto } from './api/client'

interface IdentityCtx {
  code: string
  persona: PersonaDto | undefined
  personas: PersonaDto[]
  isVendor: boolean
  roles: string[]
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
    switchTo,
  }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export function useIdentity(): IdentityCtx {
  const ctx = useContext(Ctx)
  if (!ctx) throw new Error('useIdentity must be used within IdentityProvider')
  return ctx
}

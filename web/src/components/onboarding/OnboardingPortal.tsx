import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { resolveOnboardingLink, getOnboardingLookups } from '../../api/client'
import { OnboardingLanding, tokenFromUrl } from './OnboardingLanding'
import { OnboardingForm } from './OnboardingForm'
import { OnboardingResubmit } from './OnboardingResubmit'
import { Icon } from '../Icon'

/**
 * The public, no-login vendor onboarding flow reached via the magic link (#onboard). Token-scoped:
 * landing → the multi-section form → submitted. If a clarification round is open, it routes straight
 * to the resubmit view (only the flagged items).
 */
export function OnboardingPortal() {
  const token = tokenFromUrl()
  const [screen, setScreen] = useState<'landing' | 'form' | 'done'>('landing')
  // Shared query key with the landing — TanStack dedupes, so the token resolves once.
  const { data: app } = useQuery({
    queryKey: ['onboarding-resolve', token],
    queryFn: () => resolveOnboardingLink(token),
    enabled: token.length > 0, retry: false,
  })

  // A2F-T2 (Obs-6): a real anonymous applicant has NO principal, so the authenticated
  // /custom-lists and /swec calls that useLookups/useSwec make would 401. The token
  // endpoint returns the form's reference data, and we seed it into the EXACT cache keys
  // those hooks read (both staleTime: Infinity) — the form's field machinery is untouched
  // and never issues the authenticated calls here.
  const qc = useQueryClient()
  const { data: lookups } = useQuery({
    queryKey: ['onboarding-lookups', token],
    queryFn: () => getOnboardingLookups(token),
    enabled: token.length > 0, retry: false, staleTime: Infinity,
  })
  useEffect(() => {
    if (!lookups) return
    qc.setQueryData(['swec'], lookups.swec)
    qc.setQueryData(['custom-lists'], lookups.customLists)
  }, [lookups, qc])

  if (screen === 'done') return <OnboardingSubmitted />
  if (screen === 'form') return <OnboardingForm token={token} onSubmitted={() => setScreen('done')} />
  if (app?.status === 'ClarificationRequested')
    return <OnboardingResubmit token={token} app={app} onDone={() => setScreen('done')} />
  return <OnboardingLanding onStart={() => setScreen('form')} />
}

function OnboardingSubmitted() {
  return (
    <div className="onboard-land">
      <div className="onboard-card">
        <div className="onboard-badge" style={{ background: 'var(--green)' }}><Icon name="check" size={26} /></div>
        <h1>Application submitted</h1>
        <p className="hint">
          Your application is now with SPSB procurement for review. You’ll be emailed if they approve,
          reject, or need clarification. If they request changes, this link will show exactly what to fix.
        </p>
      </div>
    </div>
  )
}

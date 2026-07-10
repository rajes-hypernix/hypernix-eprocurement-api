import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { resolveOnboardingLink } from '../../api/client'
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

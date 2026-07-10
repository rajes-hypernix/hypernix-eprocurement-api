import { OnboardingQueue } from './OnboardingQueue'
import { OnboardingReview } from './OnboardingReview'
import { OnboardingInvite } from './OnboardingInvite'

/** Buyer-side onboarding router: queue (default) · invite · review by id. */
export function OnboardingPage({ route, onNavigate }: { route: string; onNavigate: (key: string) => void }) {
  if (route === 'onboarding/invite') return <OnboardingInvite onBack={() => onNavigate('onboarding')} />
  if (route.startsWith('onboarding/')) return <OnboardingReview id={route.slice('onboarding/'.length)} onBack={() => onNavigate('onboarding')} />
  return <OnboardingQueue onNavigate={onNavigate} />
}

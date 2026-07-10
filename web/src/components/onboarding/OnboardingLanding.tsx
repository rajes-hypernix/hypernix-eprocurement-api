import { useQuery } from '@tanstack/react-query'
import { resolveOnboardingLink } from '../../api/client'
import { Icon } from '../Icon'
import { Spinner } from '../ui'

// Reads the magic-link token from the URL (?t=…). The link the server emails is
// {portal}/?t={token}#onboard, so the token lives in the query string.
export function tokenFromUrl(): string {
  try {
    return new URLSearchParams(window.location.search).get('t') ?? ''
  } catch {
    return ''
  }
}

/**
 * Vendor magic-link landing (VENDOR-ONBOARDING-SPEC §1.3, EDGE-CASES A2). A no-login, token-scoped
 * page: resolving the token opens the application (Invited → InProgress) and shows what the vendor
 * will provide. "Start onboarding" advances to the multi-section form (Slice C).
 */
export function OnboardingLanding({ onStart }: { onStart: () => void }) {
  const token = tokenFromUrl()
  const { data: app, isPending, error } = useQuery({
    queryKey: ['onboarding-resolve', token],
    queryFn: () => resolveOnboardingLink(token),
    enabled: token.length > 0,
    retry: false,
  })

  return (
    <div className="onboard-land">
      <div className="onboard-card">
        <div className="onboard-badge"><Icon name="vendor" size={26} /></div>
        <h1>Welcome to SPSB supplier onboarding</h1>

        {!token ? (
          <p className="hint">This onboarding link is missing its access token. Please use the link from your invitation email.</p>
        ) : isPending ? (
          <Spinner label="Opening your application…" />
        ) : error ? (
          <div className="ribbon ribbon-error" style={{ justifyContent: 'center' }}>
            <Icon name="x" size={14} /> {(error as Error).message || 'This onboarding link is not valid or has expired.'}
          </div>
        ) : app ? (
          <>
            <p className="hint">
              You’ve been invited to register as a supplier. This link is secure and expires in 14 days —
              no password needed. You can save and return any time via the same link.
            </p>
            <div className="card" style={{ textAlign: 'left', marginTop: 20 }}>
              <div className="cbody">
                <div className="kv"><span className="k">Application</span><span className="v mono">{app.code}</span></div>
                <div className="kv"><span className="k">Registration type</span><span className="v">{app.type}</span></div>
                <div className="kv">
                  <span className="k">You’ll provide</span>
                  <span className="v" style={{ textAlign: 'right' }}>
                    Company &amp; banking, documents{app.type !== 'SWEC' ? ', 3-yr financials' : ''}
                    {app.packs.length > 0 ? `, ${app.packs.length} question set${app.packs.length === 1 ? '' : 's'}` : ''}
                  </span>
                </div>
              </div>
            </div>
            <button type="button" className="btn btn-pri" style={{ marginTop: 18 }} onClick={onStart}>
              Start onboarding <Icon name="chev" size={15} />
            </button>
          </>
        ) : null}
      </div>
    </div>
  )
}

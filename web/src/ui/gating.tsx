import { useIdentity } from '../identity'

/**
 * DISPLAY GATING ONLY — hidden ≠ forbidden (D2 Step 0b, operator-recorded).
 * Hiding a button here does NOT protect the server action: server-side
 * role-matrix authorization is a separate slice (the PERMISSIONS-REGISTER's
 * "ONE remaining authorization item"), scheduled before D3. Until it lands,
 * every endpoint remains exactly as protected as it is today — no more.
 *
 * Each row cites its provenance: docs/PERMISSIONS-REGISTER.md for the ten
 * enumerated actions; the role sidebars/screens for quick-create access.
 */

export type GatedAction =
  // RFQ lifecycle — PERMISSIONS-REGISTER "RFQ lifecycle — Slice I"
  | 'inviteVendorToRfq'        // Buyer (RFQ owner)
  | 'rescindRfqInvitation'     // Buyer (RFQ owner)
  | 'extendRfq'                // Buyer (RFQ owner)
  | 'reInviteVendor'           // Buyer (RFQ owner)
  | 'declineRfqInvitation'     // Vendor principal
  | 'declareIntendToBid'       // Vendor principal
  | 'withdrawBid'              // Vendor principal
  | 'raiseClarification'       // Vendor principal (register); buyers start threads too (Clarifications screen)
  // Vendor portal close-out — PERMISSIONS-REGISTER "Slice K"
  | 'revokeOnboardingInvitation' // Buyer
  // Quick-create — de-facto access from the role sidebars/screens (no register rows)
  | 'createPr'                 // Buyer (Requisitions)
  | 'createRfq'                // Buyer (Consolidate)
  | 'createVendor'             // Buyer (Vendor Master → New vendor)
  | 'inviteOnboarding'         // Buyer (Onboarding → Invite)
  | 'createForm'               // Buyer (Forms)
  | 'createUser'               // Admin (User Management)
  | 'createCustomList'         // Admin (Custom Lists)

type Rule = { roles?: string[]; vendor?: boolean }

const RULES: Record<GatedAction, Rule> = {
  inviteVendorToRfq: { roles: ['Buyer'] },
  rescindRfqInvitation: { roles: ['Buyer'] },
  extendRfq: { roles: ['Buyer'] },
  reInviteVendor: { roles: ['Buyer'] },
  declineRfqInvitation: { vendor: true },
  declareIntendToBid: { vendor: true },
  withdrawBid: { vendor: true },
  raiseClarification: { vendor: true, roles: ['Buyer'] },
  revokeOnboardingInvitation: { roles: ['Buyer'] },
  createPr: { roles: ['Buyer'] },
  createRfq: { roles: ['Buyer'] },
  createVendor: { roles: ['Buyer'] },
  inviteOnboarding: { roles: ['Buyer'] },
  createForm: { roles: ['Buyer'] },
  createUser: { roles: ['Admin'] },
  createCustomList: { roles: ['Admin'] },
}

/** May the current principal SEE the affordance for this action? (Display only.) */
export function useGate() {
  const { isVendor, roles } = useIdentity()
  return (action: GatedAction): boolean => {
    const r = RULES[action]
    if (isVendor) return r.vendor === true
    return (r.roles ?? []).some((x) => roles.includes(x))
  }
}

/** Renders children only when the display gate allows the action. Hidden ≠ forbidden. */
export function Gated({ action, children }: { action: GatedAction; children: React.ReactNode }) {
  const gate = useGate()
  return gate(action) ? <>{children}</> : null
}

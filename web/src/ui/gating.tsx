import { useIdentity } from '../identity'

/**
 * DISPLAY GATING, DERIVED FROM THE SERVER (Slice RM Phase 3). The old static
 * role map is DEAD: the affordance list comes from GET /api/auth/permissions —
 * the caller's allowed actions per the server's ActionCatalog, the single
 * source of truth ruled in docs/AUTHORIZATION-MATRIX.md. Hiding remains a
 * courtesy; the SERVER enforces every action with a 403 regardless of what
 * renders here.
 *
 * Each GatedAction maps to its AUTHORIZATION-MATRIX action name (the wire
 * values of /api/auth/permissions). reInviteVendor shares InviteVendorToRfq —
 * the T8 re-invite is the same endpoint and matrix row (A27).
 */

export type GatedAction =
  // RFQ lifecycle — AUTHORIZATION-MATRIX A27–A29, A50–A52
  | 'inviteVendorToRfq'
  | 'rescindRfqInvitation'
  | 'extendRfq'
  | 'reInviteVendor'
  | 'declineRfqInvitation'
  | 'declareIntendToBid'
  | 'withdrawBid'
  | 'raiseClarification'
  // Vendor portal close-out — A43
  | 'revokeOnboardingInvitation'
  // Quick-create — A24, A25, A40, A42, A45–A47
  | 'createPr'
  | 'createRfq'
  | 'createVendor'
  | 'inviteOnboarding'
  | 'createForm'
  | 'createUser'
  | 'createCustomList'

/** GatedAction → server action name (AUTHORIZATION-MATRIX row). */
const SERVER_ACTION: Record<GatedAction, string> = {
  inviteVendorToRfq: 'InviteVendorToRfq',
  rescindRfqInvitation: 'RescindRfqInvitation',
  extendRfq: 'ExtendRfq',
  reInviteVendor: 'InviteVendorToRfq',
  declineRfqInvitation: 'DeclineRfqInvitation',
  declareIntendToBid: 'DeclareIntendToBid',
  withdrawBid: 'WithdrawBid',
  raiseClarification: 'SendClarification',
  revokeOnboardingInvitation: 'RevokeOnboardingInvitation',
  createPr: 'ManageRequisitions',
  createRfq: 'ManageRfqDraft',
  createVendor: 'ManageVendors',
  inviteOnboarding: 'InviteOnboarding',
  createForm: 'ManageForms',
  createUser: 'ManageUsers',
  createCustomList: 'ManageCustomLists',
}

/** May the current principal SEE the affordance for this action? (Derived from the server list.) */
export function useGate() {
  const { permissions } = useIdentity()
  return (action: GatedAction): boolean => permissions.includes(SERVER_ACTION[action])
}

/** Renders children only when the server-derived permission list allows the action. */
export function Gated({ action, children }: { action: GatedAction; children: React.ReactNode }) {
  const gate = useGate()
  return gate(action) ? <>{children}</> : null
}

# PERMISSIONS REGISTER (deferred auth)

Actions that will need authorization enforcement in the deferred auth pass. Slice I records them
here rather than adding `[Authorize]` (README rule 9); today they follow the existing
service-layer `ICurrentUser` convention (vendor-scoped where noted) but carry no role gate.

## RFQ lifecycle — Slice I

| Action | Endpoint | Principal / role (TBD) | Scope | Notes |
|---|---|---|---|---|
| InviteVendorToRfq | POST /api/rfqs/{id}/invitations | Buyer (RFQ owner) | RFQ | new invite + T8 re-invite of a rescinded row |
| RescindRfqInvitation | POST /api/rfqs/{id}/invitations/{vendorId}/rescind | Buyer (RFQ owner) | RFQ | reason code required (G4); blocked if a bid is submitted (G3) |
| ExtendRfq | POST /api/rfqs/{id}/extend | Buyer (RFQ owner) | RFQ | forward-only, capped (G5) |
| ReInviteVendor | (same as InviteVendorToRfq) | Buyer (RFQ owner) | RFQ | T8 — returns a Rescinded row to Invited |
| DeclineRfqInvitation | POST /api/my/rfqs/{id}/decline | Vendor principal | own invitation only | resource-scoped via ICurrentUser.VendorId |
| ReverseDecline / DeclareIntendToBid | POST /api/my/rfqs/{id}/intend | Vendor principal | own invitation only | T2 and T4 |
| WithdrawBid | POST /api/my/rfqs/{id}/withdraw-bid | Vendor principal | own invitation + own bid | Open + before close (T6) |
| MarkInvitationViewed | (side effect of GET /api/rfqs/{id}) | Vendor principal | own invitation only | T1, passive/idempotent |

## Vendor portal close-out — Slice K

| Action | Endpoint | Principal / role (TBD) | Scope | Notes |
|---|---|---|---|---|
| RevokeOnboardingInvitation | POST /api/onboarding/invitations/{id}/revoke | Buyer | invitation | wired into the Onboarding Queue; revokes the magic link (link stops resolving) |
| RaiseClarification (vendor) | POST /api/clarifications | Vendor principal | own vendor + own invited RFQs | vendor-initiated thread; scope limited client-side to the vendor's invited RFQs |

## Security perimeter — Slice F (Hardening 2)

The perimeter is now enforced in the framework, not just in services (SEC-1/2/3):

- **Deny-anonymous-by-default.** `Program.cs` sets a `FallbackPolicy = RequireAuthenticatedUser`, so
  every endpoint requires an authenticated principal unless it carries `[AllowAnonymous]`. The
  **exact** exemption list (asserted by `AnonymousSweepTests`, which fails if anyone adds an
  un-exempted anonymous endpoint): `GET /api/health`; `GET /api/auth/dev-users` +
  `POST /api/auth/dev-login` (demo bootstrap, internally demo-gated); and the token-scoped onboarding
  endpoints (`POST /onboarding/resolve`, `GET|PUT /onboarding/draft`, `POST /onboarding/draft/submit`,
  `POST|DELETE /onboarding/draft/documents`, `POST /onboarding/draft/resubmit`,
  `POST /onboarding/draft/raise-clarification`).
- **Demo identity is a real authentication scheme.** `DemoAuthenticationHandler` turns `X-Demo-User`
  into a `ClaimsPrincipal` (inert outside demo mode; hard-blocked in Production), so the fallback
  policy sees an authenticated request. `CurrentUser` is now a pure claims reader.
- **`GET /api/auth/personas` is authenticated-only** (was ungated after Hardening 1) — NOT on the
  exemption list; resolves the disposition the register flagged.

## Known scoping gaps (for the auth slice)

- ~~**`GET /api/rfqs` returns ALL RFQs to vendor principals**~~ — **CLOSED (Slice F Phase 3):**
  `RfqService.ListAsync` now scopes vendor principals to RFQs they hold a live invitation to
  (buyers/admins unchanged); asserted by `VendorScopingTests`.
- **CLOSED (Slice F Phase 2/3):** file downloads are resource-scoped (`FileAccessPolicy`,
  deny-on-uncertainty) + audited; vendor bank `AccountNo`/`Swift` masked (last-4) for any
  non-Buyer/Admin on `GET /api/vendors/{id}` (list DTOs carry no bank fields at all).

### Still open (deferred, out of Slice F's perimeter scope)
- **Role-matrix authorization** — which role may perform which enumerated action above (Buyer vs
  Approver etc.) beyond the existing service-layer `ICurrentUser` checks. Slice F required only
  *authentication* everywhere + scoping for confirmed leaks; per-action role policies are a later pass.
- **File-ownership schema** — ownership is inferred by scanning answer-value GUIDs (`StoredFile` has no
  owner column); add a typed column in Slice G (see BACKLOG).

## Tickets raised by Slice J (next backend slice)

- **Expose `MaxExtensions` in `RfqDetail`** — the RFQ detail returns the extension COUNT but not the
  cap, so the Slice J UI mirrors the server default in a `MAX_EXTENSIONS = 2` constant
  (`web/src/components/sourcing/RfqGovernance.tsx`). If an admin changes `RfqGovernanceOptions.MaxExtensions`
  to 3, buyers would still see "Extension 2 of 2" while the server allows a third. Fix: add `MaxExtensions`
  (int) to `RfqDetail`, fold into the next backend slice, and delete the client constant.

## Known items for the hardening pass (not fixed in this slice)

- **Extend/close race** — two buyers extending (or one extending while another closes) concurrently.
  Last-write-wins is acceptable for now (both `RfqEvent` rows are still recorded); add a concurrency
  token (RowVersion/xmin) when the deferred concurrency pass lands (RFQ-LIFECYCLE-ADDENDUM E11).
- **FluentValidation referenced but unused** — the `FluentValidation` package is on
  `eProcure.Application` but no validators/pipeline exist. Slice I follows the codebase's existing
  inline `DomainRuleException → 409` convention for reason-code / note-length validation rather than
  introducing a second validation style. Decision for a future pass: adopt FluentValidation
  platform-wide or remove the package.
- **Audit-write-in-separate-save** — `IAuditLog.WriteAsync` does its own `SaveChangesAsync` after the
  state change (existing convention). The typed `RfqEvent` IS written in the same transaction as its
  state change (G6); only the generic `AuditEntry` follows separately. Already on the register.

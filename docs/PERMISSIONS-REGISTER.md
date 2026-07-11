# PERMISSIONS REGISTER — closed; superseded by AUTHORIZATION-MATRIX.md

**The living who-may-do-what document is [AUTHORIZATION-MATRIX.md](AUTHORIZATION-MATRIX.md)**
(operator-ruled 2026-07-11, Slice RM). Every authenticated endpoint carries an
`[Action(...)]` resolved against the declarative `ActionCatalog`
(`api/src/eProcure.Application/Authorization/ActionCatalog.cs`), whose rows mirror the
matrix one-for-one; the web's display gating is derived at runtime from
`GET /api/auth/permissions`. Drift is test-guarded in both directions
(`ActionAssignmentSweepTests`) and the full role × action matrix is asserted by
`RoleMatrixTests`, generated from the catalog.

### The "ONE remaining authorization item" — CLOSED (Slice RM)

Role-matrix authorization is enforced at the server. The 10 actions this register
enumerated (8 RFQ-lifecycle from Slice I + 2 vendor-portal from Slice K) are matrix
rows A27–A29, A43, A48, A50–A53 (MarkInvitationViewed rides A8 as the T1 side
effect). Authentication and the perimeter were closed by Slice F; data integrity and
status encapsulation by Slice G; **with Slice RM there are no outstanding
authorization items.**

### What remains true (enforced elsewhere, unchanged by Slice RM)

- **Deny-anonymous-by-default** — fallback policy + the exact 11-endpoint
  `[AllowAnonymous]` exemption list, asserted by `AnonymousSweepTests`
  (matrix §3 quotes it verbatim; Slice RM changed nothing here).
- **Vendor resource scoping** (Slice F) — RFQ-list invitation scoping, file
  download `FileAccessPolicy` (deny-on-uncertainty), bank-detail masking; pinned by
  `VendorScopingTests`, all untouched. The role gate is the coarse layer ABOVE these.
- **Demo identity** — `X-Demo-User` → `DemoAuthenticationHandler` → ClaimsPrincipal;
  hard-blocked in Production; dev-login/dev-users 404 outside demo mode.

### Deferred items raised here or by the RM rulings (tracked in BACKLOG.md)

- Slice F patch: vendor resource scoping on RFQ-detail and GRN-for-ASN reads
  (matrix Obs-1 + Obs-4) — standalone micro-commit immediately after RM.
- Onboarding magic-link page depends on authenticated `/swec` + custom-list lookups
  (matrix Obs-6) — token-scoped/anonymous lookups design needed before staging.
- `SetScoreAsync` validates the body's `EvaluatorId`, not the caller (matrix Obs-7)
  — hardening pass.
- FluentValidation package referenced but unused (decision: adopt platform-wide or
  remove); audit-write-in-separate-save convention — both pre-RM notes, unchanged.

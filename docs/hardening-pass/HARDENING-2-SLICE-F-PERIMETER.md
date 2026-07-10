# HARDENING 2 of 2 — SLICE F: Security Perimeter

Prerequisite: HARDENING-1 merged and green. This slice closes the audit's
Critical finding (SEC-1/2/3, X1): no endpoint authorization, ungated files,
unauthenticated access to vendor bank data. Scope is the PERIMETER —
authentication required everywhere + resource scoping for confirmed leaks.
The fine-grained role matrix (which role may perform which of the
register's enumerated actions) is a SEPARATE later pass — do not build it
here beyond what already exists in services.

## Ground rules
1. Step 0: read docs/PERMISSIONS-REGISTER.md fully; list every file you'll
   touch, grouped by phase; WAIT for confirmation.
2. No behaviour change for legitimate authenticated users: after this
   slice, the web app under a Development X-Demo-User persona must behave
   exactly as before (the crawl proves it).
3. Baselines from HARDENING-1's final report; nothing below them.

## Phase 1 — Fallback authorization (deny anonymous by default)
- Program.cs: add a FallbackPolicy = RequireAuthenticatedUser. Every
  endpoint now requires a principal unless explicitly opted out.
- Explicit [AllowAnonymous] ONLY on: health check; the onboarding
  token-resolve/apply endpoints (anonymous BY DESIGN, token-hash scoped —
  verify each is token-gated in the service before exempting); dev-login/
  dev-users (already demo-gated by HARDENING-1 T1; anonymous access to
  them remains env/flag-controlled inside the controller).
- The X-Demo-User principal (when demo mode active) must satisfy the
  fallback policy — verify the CurrentUser/authentication handler wiring
  produces an authenticated principal, not just an ICurrentUser value,
  or the web app breaks. If the current shim bypasses ASP.NET auth
  entirely, convert it to a proper authentication handler that issues a
  ClaimsPrincipal — report the design before implementing.

## Phase 2 — Files gating (SEC-3)
- FilesController upload + download require an authenticated principal
  (covered by Phase 1) PLUS resource scoping: a vendor may download only
  files attached to their own bids/onboarding/invoices; buyers/admins may
  download any. Upload requires an authenticated principal; keep the
  existing size/type validation behaviour unchanged (improving validation
  is SEC-5, out of scope).
- Every file download writes an AuditEntry ("File", fileId, "Downloaded")
  — the O&G probity requirement: who accessed sealed-bid documents.

## Phase 3 — Confirmed resource-scoping leaks (register: "Known scoping gaps")
- GET /api/rfqs: vendor principals receive ONLY RFQs they hold a live
  invitation to (reuse the ListMyInvitations derivation); buyer/admin
  behaviour unchanged.
- GET /api/vendors/{id}: bank account fields (account no, SWIFT) masked
  for any principal that is not Buyer/Admin — and masked in list DTOs
  everywhere regardless of role (full values only on the detail endpoint
  for authorized roles). Follow the existing masking pattern if one
  exists; otherwise last-4 convention.
- GET /api/statements list: authenticated-only via fallback; vendor
  my-statement scoping already enforced — verify with a test.

## Phase 4 — Authorization test matrix (the missing test class)
- New WebApplicationFactory-based integration test suite (first in the
  repo — establish the harness):
  a. ANONYMOUS sweep: every registered endpoint (enumerate via
     ApiExplorer) returns 401 except the explicit [AllowAnonymous] set —
     assert the exemption list is EXACTLY as designed, so any future
     endpoint added without thought fails this test by default.
  b. Vendor-scoping: vendor A cannot fetch vendor B's file, RFQ list shows
     only invited, bank fields masked for vendor principal.
  c. Demo-gating regression: X-Demo-User rejected in fake Production.
- These tests are the slice's real deliverable: the perimeter stays closed
  because the suite fails when anyone opens it.

## Phase 5 — Verification
- Full API suite + new matrix; web vitest; e2e-audit crawl green under
  Development demo identity (proves rule 2).
- curl transcript reproducing the audit's original security probes
  (/users, /vendors, vendor SWIFT grep, files download) now returning
  401/403/masked — the before/after proof against full-audit-2026-07-02.md
  §security probes.

## Final report
Files per phase; the exact [AllowAnonymous] exemption list with per-item
justification; matrix test counts; the curl before/after transcript;
register updated (scoping gaps closed marked as such); anything observed
but not fixed.

## Out of scope
Role-matrix authorization (Buyer vs Approver per action) beyond existing
service checks; file upload content validation (SEC-5); rate limiting;
FKs/concurrency (integrity slice); object storage (REL-1).

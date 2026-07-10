# Engineering Standards — the bar for this module

Build as the most detail-oriented engineer/architect/CTO would: correct, clean, maintainable,
cleanly handed over. Binding.

## Architecture & DDD
- **Clean-architecture boundaries.** Domain has no EF/infra/web deps. Application orchestrates;
  Infrastructure implements (email, files, persistence); Api stays thin.
- **Aggregate roots.** `VendorOnboardingApplication` is the aggregate root and owns its value
  objects (profile/contacts/addresses/banking/certs drafts) and its financial assessment.
  Clarification rounds are owned by the application. `VendorOnboardingInvitation` is its own
  aggregate. `Vendor` / `VendorUser` are promoted *from* an application, not owned by it.
- **Rich domain, not anemic.** Transitions are **methods on the aggregate** and the single
  chokepoint for recording each change: `Submit`, `StartReview`, `RequestClarification(items)`,
  `Resubmit`, `Approve`, `Reject`, `Revoke`, `Expire`. Services call these; controllers never
  mutate state directly.
- **Illegal transitions throw `DomainRuleException`** (existing type) with a clear message.
- **Value objects** where invariants exist: Money (amount+currency), the magic-link token,
  Code IDs.

## Determinism & side effects
- **All time via `IClock`** (UTC). No `DateTime.Now`.
- **All codes via `NumberSequence`/`CodeGenerator`.**
- **All transitions audited** via `AuditEntry` (typed fields).
- **Email via `IEmailSender`** only — an abstraction with a config-driven transport, a fake in
  tests, and a non-production recipient override. No SMTP secrets in code.
- The token-resolve/submit paths are safe under retry (idempotent where it matters).

## Financial calculation
- Pure, deterministic functions matching the prototype **exactly** (coefficients, weights,
  bands, score clamp). Unit-tested against known inputs → known Z/score/band. Computed values
  are **snapshotted** at submit/decision; never store only a live-derived value as the record
  of decision.

## Code quality
- No magic strings/numbers — enums + named constants; stable enum values.
- Names match existing codebase conventions; new code reads like it was always there.
- Nullability honoured (NRT on). Public surface has concise XML doc-comments; **each fact/link
  entity states its grain**.
- Frontend: Oxlint (type-aware) + `tsc --noEmit` clean. Reuse existing form-renderer and UI
  primitives; take layout from the prototype but colours/fonts/components from the app's style
  package.

## Migrations
- **One focused, reversible migration per slice**, clearly named, 14-digit timestamp. Never edit
  a shipped migration. Additive-first: add new tables/columns and backfill; retire old shapes
  only when nothing reads them. Preserve existing seed/readers.

## Testing (test in detail — non-negotiable)
- **Unit:** every transition + guard (including illegal-transition rejections); the financial
  model against known vectors; token hashing/expiry; header/status derivation.
- **Integration:** endpoints + EF persistence (existing InMemory provider), `IEmailSender`
  fake asserting recipient/subject/link, token-resolve scope enforcement, promotion-to-master.
- **Parity** against the prototype behaviour; **E2E** for the headline flow (EDGE-CASES §G).
- Deterministic seed; order-independent tests. **The full existing suite stays green;** new
  behaviour ships with new tests.

## Hand-over
- Deferred work is tagged (`// HARDENING:`, `// DEFERRED:`, `// WORKFLOW-SEAM:`) and listed in
  the slice report: what changed, migration name, new test count, full-suite status, and any
  decision taken where the spec left a choice open.

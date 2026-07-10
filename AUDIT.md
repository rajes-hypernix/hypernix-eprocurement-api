# eProcure — Code & Production-Readiness Audit

_Prepared as a CTO-level review. Scope: full repo (.NET 10 API, React 19 web, PostgreSQL 16)._
_Date: 2026-06-29. Method: hard metrics + structural verification + three parallel specialist audits (backend, frontend, production/ops), cross-checked against direct reads._

> Status: **recorded for later action.** No fixes from this report have been applied yet (sidebar scroll fix tracked separately).

---

## 1. Executive verdict

This is a well-architected, genuinely disciplined MVP — and it is **not production-ready** for a system people stake audited award and payment decisions on. The gap is almost entirely **platform, auth, and concurrency**, not domain logic. The hardest, most valuable thing — server-side enforcement of the procurement rules (SoD, DoA, bid deadlines, technically-passed-only eligibility, evaluator masking, decimal money) — is built correctly. The blockers are the trust boundary *around* that core: effectively no authentication in production, no authorization on any endpoint, no concurrency control on the money flows, and no CI/observability.

**Composite: ~4.5/10 production-ready** — Backend code 5/10, Frontend 6.5/10, Domain core ~8/10, Platform/Ops ~2/10.

## 2. By the numbers

| Area | Metric |
|---|---|
| Backend hand-written | 5,785 LOC / 109 files — Domain 851, Application 628, Infrastructure 3,349, Api 957 |
| Backend surface | 15 controllers · 23 services · 24 domain entities · 11 migrations |
| Frontend hand-written | 5,992 LOC / 42 files (+3,550 generated client) · 29 components · index.css 683 LOC |
| Tests | 71 backend facts (1,432 LOC) · 67 frontend it() (624 LOC) |
| Type hygiene (web) | 0 `any`, 0 `@ts-ignore`, 0 eslint-disable — but 76 non-null `!`, 337 inline styles |
| Risk signals | 0 concurrency tokens · 0 `[Authorize]` attributes · 3 log statements · 0 TODO/FIXME |

Code quantity is right-sized and lean — no bloat, no dead-code sprawl, no copy-paste mega-files. The problem is specific structural gaps, not volume.

## 3. What's genuinely good (don't lose these)

- **Textbook layering.** Api → Infrastructure → Application → Domain; Domain references nothing (verified at `.csproj` level).
- **Hard business rules enforced server-side and un-spoofable.** Self-approval block + DoA role check (`AwardService.cs:144`); bid-deadline enforcement vs injected clock (`BidService.cs:83`); evaluator masking derived from roles not a client flag (`EvaluationService.cs:120`).
- **Money discipline.** Every `decimal` → `numeric(18,2)` model-wide; human codes from a server-side sequence.
- **Clean typed frontend boundary.** 0 `any`; DTOs re-exported from generated OpenAPI schema; `qc.clear()` on persona switch (`identity.tsx:26`).
- **Demo data correctly walled off.** Seeder + auto-migrate run only under `IsDevelopment()` (`Program.cs:89`); seeder is idempotent.

## 4. Findings by severity

### CRITICAL — production blockers

1. **No real authentication; `X-Demo-User` impersonation is live in production.** `CurrentUser.cs:51` reads the `X-Demo-User` header whenever no JWT is present, with **no environment gate** (verified — the "Development only" docstring is false). `X-Demo-User: u_admin` → admin; a vendor code → defeats vendor scoping. Only token issuer `dev-login` mints a fully-roled JWT for any seeded user with **no password**. → Anyone can be anyone.
2. **Zero framework authorization.** Six role policies defined but never applied — 0 `[Authorize]` attributes, so `UseAuthorization()` guards nothing. `RfqService`, `FormService` (incl. delete), and `StatementService` (returns every vendor's financials) inject no `ICurrentUser` at all.
3. **No concurrency control on any money/quantity mutation.** Zero `RowVersion`/`xmin` tokens. Concurrent approvals → duplicate POs (`AwardService.cs:135`); concurrent GRNs → over-receipt; stale read → over-billing. `CodeGenerator` is racy → duplicate human codes.
4. **Multi-step money mutations aren't transactional.** `ApproveAsync` flips award + RFQ status, generates POs, pushes NetSuite across multiple `SaveChangesAsync` with no enclosing transaction; audit row written *after* the state commit (can be lost on crash).
5. **Hardcoded document dates on financial records** (corroborated by both auditors). `DeliveryService.cs:125`, `InvoiceScreens.tsx:140`, `DeliveryScreens.tsx:92` stamp `"28/06/2026"` literal — wrong date on any other day, corrupts statement aging.
6. **Committed secrets / known JWT key.** `appsettings.Development.json` ships the signing key + DB password; anyone with the repo can forge tokens.
7. **No CI/CD, no Dockerfile, no prod migration step, near-zero observability** (3 log statements total).

### HIGH

- **Audit-trail holes**: evaluator scoring, clarifications, form edits write no audit entry; audit written outside the state transaction.
- **An evaluator can score under another evaluator's identity** — `SetScoreAsync` takes `EvaluatorId` from the request body, only checks it's assigned (not == caller).
- **Files unscoped** — `FilesController.cs:21` returns any stored blob by GUID with no ownership check.
- **Frontend crash risk + systemic a11y gap.** Non-null assertions on optional arrays (`VendorDetail.tsx:174`); no `htmlFor` anywhere; back-crumbs and drill-in rows are keyboard-inaccessible `<a onClick>`/`<div onClick>`; modals have no focus trap/Escape.
- **Cache under-invalidation on the money path** (award→PO→receive→invoice→statement), masked only by aggressive default refetching.
- **API hardening**: CORS hardcoded to `localhost:5173` for all envs; no HSTS/security headers; no rate limiting.

### MEDIUM / LOW

- Anemic aggregates (property bags with public setters; invariants only in services).
- Whole-table loads + client-side aggregation (`DashboardService`, `VendorService`, invitation lists); `InvitedVendorIds` is a `|`-delimited string column.
- REST inconsistency (creates return 200 not 201; missing resources 204 not 404).
- Tests use EF InMemory — doesn't validate constraints/transactions/concurrency; no Postgres integration tests, no e2e in CI.
- Styling: 337 inline styles + hardcoded hex bypass tokens; 0 `@media` (no responsive).

## 5. Production-readiness scorecard

| Dimension | Status |
|---|---|
| Architecture & layering | ✅ Clean |
| Domain / business-rule enforcement | ✅ Strong |
| Money & decimal handling | ✅ Correct |
| Authentication | ❌ Demo-only; prod bypass |
| Authorization | ❌ Absent at framework level |
| Concurrency / data integrity | ❌ No tokens, non-transactional money flows |
| Audit trail completeness | ⚠️ Holes + outside-transaction |
| Secrets management | ❌ Committed; known key |
| CI/CD & deploy | ❌ None |
| Observability | ❌ ~3 log lines |
| API hardening (CORS/HSTS/rate-limit) | ⚠️ Dev-only config |
| Accessibility | ❌ Systemic gaps |
| Test integrity (vs InMemory) | ⚠️ Doesn't validate constraints |
| Documentation (design) | ✅ Good (ops runbooks ❌) |

## 6. What must be done before production (prioritized)

**Phase 0 — Security lockdown (blocks everything; ~1–2 weeks)**
1. Gate the entire `X-Demo-User` branch behind `IsDevelopment()`; delete/hard-gate `dev-login` + `DevUserStore`. Integrate real OIDC/SSO (staff) + separate vendor login; MFA for approvers. **(L)**
2. Add `AuthorizationOptions.FallbackPolicy` (require authenticated user) + `[Authorize(Policy=…)]` on all 15 controllers; add scope guards to `RfqService`/`FormService`/`StatementService`/`FilesController`. **(S–M)**
3. Move JWT key + connection string to a secrets manager; rotate the leaked key; purge from history. **(S–M)**

**Phase 1 — Data integrity (~1–2 weeks)**
4. Add `xmin`/`RowVersion` tokens to Award, PO/PoLine, Invoice/InvoiceLine, Bid, Rfq; retry on `DbUpdateConcurrencyException`. Replace `CodeGenerator` with a Postgres `SEQUENCE`. **(M)**
5. Wrap multi-step mutations in one transaction with the audit write inside it; add missing audit entries; derive `EvaluatorId` from the principal. **(M)**
6. Fix the hardcoded dates (3 sites) — derive from the clock / server-owned timestamps. **(S)**

**Phase 2 — Platform & ops (~2–3 weeks)**
7. Dockerfiles (api + web); CI running `dotnet test` + web tests on PRs; deploy pipeline with explicit migration step; keep seeder out of prod images. **(M–L)**
8. Structured logging + correlation IDs + request logging; metrics + tracing + error/alert sink; real `AddHealthChecks` (liveness/readiness). **(M)**
9. HSTS + security headers, config-driven CORS origin, rate limiting, `EnableRetryOnFailure`/command timeouts on Npgsql, restrict `AllowedHosts`. **(S–M)**

**Phase 3 — Quality & correctness (~1–2 weeks)**
10. Frontend: centralize `Modal` (focus trap/Escape/restore), crumb `<a>`→`<button>`, add `htmlFor`; guard optional-array `!`; fix money-path cache invalidation (query-key factory). **(M)**
11. Replace EF-InMemory money-flow tests with Postgres-backed integration tests (Testcontainers); wire prototype harnesses into CI. **(M)**
12. Promote hottest aggregates (Award, Invoice, PO) to rich domain with guarded transitions. **(M, recommended)**

**Phase 4 — Deferred (tracked, not blocking)**
13. Real NetSuite integration via a transactional outbox (currently correctly stubbed). **(L)**

## 7. Bottom line

Craftsmanship and domain modeling are strong; the team clearly understands clean architecture, the procurement rules, and type safety. What's missing is the production trust boundary: authentication, authorization, concurrency, transactions, secrets, CI, observability — plus two real data-correctness bugs (hardcoded dates) and a systemic accessibility gap. None of the Critical items are deep rewrites. **Phases 0–1 are the true go-live gate;** Phases 2–3 make it operable and compliant.

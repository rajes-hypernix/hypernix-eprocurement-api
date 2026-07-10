# Data Model — Analytics- & Workflow-Ready Standard

> Long-term goals this schema must serve: (1) a SuiteAnalytics-style analytics layer, and
> (2) a configurable workflow engine for all record types. Build the OLTP so both are later
> mechanical, with **zero refactor of business tables**. Normalize now; shape in analytics
> views later; keep workflow state in its own seam.

Binding for every table this module adds or touches.

## 1. Staging is separate from the master
The vendor's self-entered data lives in `VendorOnboardingApplication` (+ its owned value
objects), **never** in `Vendor`, until **approval** promotes it. This keeps the master clean
(only governed, approved data), makes reject/clarify non-destructive, and gives a clean audit
of what was proposed vs what was accepted.

## 2. Append-only events / lineage — never destroy history
- **No hard deletes** of applications, rounds, or answers. Withdraw/reject/expire are **states +
  timestamps**.
- `OnboardingClarificationRound` / `OnboardingClarificationItem` are **append-only**. A round,
  once sent, is immutable; the response fills a separate field. This yields cycle-time and
  clarification-round analytics for free.
- Every transition is recorded via the existing `AuditEntry` with **typed columns** (entity,
  entity id, from-state, to-state, reason, actor, `OccurredUtc`) — not a free-text blob.

## 3. Keys: surrogate + business, both stable and immutable
- **GUID PK**, system-generated, never reused.
- **Business code** `VOB-2026-####` from `NumberSequence`, immutable once assigned. Join on the
  GUID, never the code.

## 4. Conformed dimensions — controlled, not free-text
Analytics must group reliably by vendor type, status, financial band, risk, source
(Manual/SelfService), SWEC category, and (for onboarding throughput) invited-by / decided-by.
Store these as **enums or coded references**, not free strings. SWEC categories reuse the
existing category codes; vendor type/status reuse the existing `VendorType`/`VendorStatus`
enums. Add `OnboardingStatus`, `LinkStatus`(round), `ClarificationDirection`, `FinancialBand`,
`RiskCategory`, `ApplicationSource` as enums.

## 5. Dates/times: typed and UTC
All timestamps `DateTimeOffset`/UTC via `IClock` (`CreatedUtc`, `SubmittedUtc`, `DecisionUtc`,
round `RaisedUtc`/`RespondedUtc`, invitation `ExpiresUtc`). Any business date is `DateOnly`.
No string dates — they cannot be range-filtered for onboarding cycle-time analytics.

## 6. Financial assessment: typed + as-of snapshot
`VendorFinancialAssessment` stores the 11 line items × 3 years as **typed decimals** (RM'000),
plus **as-of snapshots** of weighted Z′, score, band, risk, and the conditional statement,
captured at submit and at decision. Live ratios/Z are **derived** for display; the snapshot is
what's stored and reported, so a later change to SPSB Finance's thresholds/weights never
rewrites a historical assessment. Money is `decimal`, never float.

## 7. Grain — declare it per fact/link table (XML doc-comment)
- `VendorOnboardingApplication` — one onboarding application (the onboarding grain).
- `OnboardingClarificationRound` — one clarification exchange (one packet).
- `OnboardingClarificationItem` — one flagged item within a round.
- `OnboardingAnswer` — one answer to one `FormItem` on one application.
- `VendorFinancialAssessment` — one financial assessment snapshot for one application/vendor.

## 8. Reuse, don't duplicate
- **Questionnaire:** extend `FormTemplate` with `Purpose (Rfq|Onboarding|Both)`; reuse
  `FormItem`; store responses as `OnboardingAnswer (FormItemId, Value)` — the same shape as the
  RFQ `BidAnswer`, so the two answer sets are analytically consistent.
- **Documents:** `StoredFile`. **Codes:** `NumberSequence`. **Audit:** `AuditEntry`.
  **Login:** `VendorUser` (provisioned only on approve). Don't reinvent any of these.

## 9. Workflow-engine seam
Model the review as an ordered `OnboardingStep`/decision list (v1: manual = 0, invite = 1, with
an optional Finance sub-step for Non-SWEC), with each decision carrying actor + timestamp +
outcome + reason. Keep this in its own table(s) so a future configurable engine can drive
routing for any record type without reshaping onboarding tables. **Do not build the engine
now** — just leave the seam.

## 10. Security
Magic-link token: store a **hash** (never the raw token), with `ExpiresUtc`, single-application
scope, and a status (`Sent/Opened/Expired/Revoked/Completed`). The onboarding portal scope is
read/write **only its own application** — no access to other portal data pre-approval.
No SMTP or token secrets in code — config only.

## 11. Indexing (light, not premature)
Index FKs and the columns analytics/queue filters use: status, vendor type, band, source,
`CreatedUtc`/`SubmittedUtc`/`DecisionUtc`, invited-by, decided-by. Add in the migration.

## 12. OLTP vs analytics separation
Don't denormalize business tables for reporting. Clean, normalized, fully-keyed, append-only
facts + conformed dimensions = a future star/ELT projection (and the analytics gallery) is
mechanical.

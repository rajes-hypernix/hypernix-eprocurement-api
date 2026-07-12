# Edge Cases — expected behaviour (assert all of these)

Each row is a test. Behaviour mirrors `prototype/vendor-onboarding-mockup.html`.

## A. Lifecycle

| # | Scenario | Expected |
|---|---|---|
| A1 | Buyer sends invite | Application created `Invited`; magic link emailed; token stored **hashed**; `VOB` code from sequence; audit |
| A2 | Vendor opens link | `Invited → InProgress`; scope limited to this application only |
| A3 | Vendor submits | `InProgress → Submitted`; financial snapshot captured; buyer notified |
| A4 | Buyer starts review | `Submitted → UnderReview` |
| A5 | Approve | `→ Approved`; promoted to `Vendor` master; `VendorUser` provisioned; assessment copied; set-password email; audit |
| A6 | Reject (reason) | `→ Rejected` (terminal); reason emailed; master untouched |
| A7 | Request clarification (N items) | `→ ClarificationRequested`; one round, one email, N items; append-only |
| A8 | Vendor resubmits round | `ClarificationRequested → Resubmitted → UnderReview`; only flagged items shown to vendor |
| A9 | Link expires before submit | `→ Expired`; opening shows expired message; buyer can resend (new token) |
| A10 | Buyer revokes an invite | `→ Revoked`; token no longer resolves |
| A11 | Illegal transition (e.g. approve an `Invited` app) | `DomainRuleException`; no state change |

## B. Registration type & financials

| # | Scenario | Expected |
|---|---|---|
| B1 | SWEC application | No financials section; pre-qual **waived**; review shows LLRC waiver |
| B2 | Non-SWEC application | Financials required; ratios + Altman Z computed live |
| B3 | Altman Z correctness | For the prototype's seed vectors, Z/score/band match the prototype exactly |
| B4 | Band thresholds | Z≥2.9→A, ≥2.0→B, ≥1.23→C, <1.23→D; score = clamp(round(Z/4.5·100),5,99) |
| B5 | Snapshot vs live | Stored Z/band = snapshot at submit/decision; later weight change doesn't rewrite it |
| B6 | Excel import (interim) | Populates the 11 line items; recompute matches manual entry |

## C. Manual entry

| # | Scenario | Expected |
|---|---|---|
| C1 | Manual create | Written straight to `Vendor` master, status Registered; **no approval**; audit |
| C2 | Manual create with duplicate reg. no. | Duplicate warning surfaced (not silently created twice) |

## D. Questionnaire = Form Templates

| # | Scenario | Expected |
|---|---|---|
| D1 | `FormTemplate.Purpose` | Forms page can filter Onboarding templates; invite offers only those |
| D2 | Invite selects packs | Selected templates attach to the application |
| D3 | Vendor renders packs | Rendered with the existing form renderer (same as RFQ questionnaire) |
| D4 | Answers stored | As `OnboardingAnswer (FormItemId, value)`; retrievable in review |
| D5 | Edit a pack on Forms page | Change reflects for **new** invites; existing applications keep their answered items |

## E. Email (testable)

| # | Scenario | Expected |
|---|---|---|
| E1 | Invite email | Sent via `IEmailSender`; contains the magic link; link resolves to the application |
| E2 | Default recipient | Invite vendor-email input defaults to `vendor-invites@hypernix.test` |
| E3 | Non-prod override | With `Onboarding:TestRecipientOverride` set, ALL onboarding emails route to that address |
| E4 | Clarification / approve / reject emails | Each transition sends the right template to the vendor |
| E5 | No secrets in code | SMTP config comes from configuration, not source |

## F. Security / integrity

| # | Assert |
|---|---|
| F1 | Token stored hashed; raw token never persisted |
| F2 | Onboarding scope can read/write only its own application |
| F3 | No `Vendor`/`VendorUser` created before approval |
| F4 | Clarification rounds/items never edited or deleted after send |

## G. Must-not-regress + E2E

| # | Assert |
|---|---|
| G1 | Existing Vendor Master, RFQ, Forms, bid, award, PO, GRN, invoice flows unchanged |
| G2 | All pre-existing tests stay green |
| G3 | **E2E:** invite (email to vendor-invites@hypernix.test) → open link → fill (Non-SWEC, live Z) → submit → buyer review → request clarification (2 items, one email) → vendor resubmits both → approve → vendor in master + `VendorUser` provisioned + assessment copied. Assert audit + snapshots at each step. |

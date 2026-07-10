# Build Plan — 5 slices

Build in order. **Stop after each**, run lint + typecheck + full suite, confirm green, then
continue. Batches of ~3 screens then checkpoint. Everything additive; staging separate from
master; reuse the Forms engine.

---

## Slice A — Backend foundation (schema + lifecycle + financials + Forms.Purpose; NO UI)
- Entities: `VendorOnboardingInvitation` (hashed token, email, type, template ids, `ExpiresUtc`,
  status, invited-by); `VendorOnboardingApplication` (staging: profile/contacts/addresses/
  banking/certs value objects mirroring `Vendor`, categories, lifecycle status, source, links
  to invitation, `SubmittedUtc`/`DecisionUtc`/`PromotedVendorId?`); `VendorFinancialAssessment`
  (11 line items ×3 typed decimals + as-of snapshots of Z/score/band/risk/statement + finance
  remarks); `OnboardingClarificationRound` + `OnboardingClarificationItem` (append-only,
  directional); `OnboardingAnswer` (FormItemId, value).
- Extend `FormTemplate` with `Purpose (Rfq|Onboarding|Both)`. **Seed the 5 starter packs** as
  Onboarding Form Templates (HSE, Quality, Capability, References, Compliance).
- Enums: `OnboardingStatus`, `ApplicationSource`, `ClarificationDirection`, round `LinkStatus`,
  `FinancialBand`, `RiskCategory`.
- Financial service: exact Altman Z′ model (coefficients/weights/bands/score) as pure
  functions; snapshot helper.
- Domain transition methods + `DomainRuleException`; reuse `NumberSequence`, `AuditEntry`
  (typed), `IClock`. Workflow-seam step list (v1 length 0–1). Index analytics/queue columns.
- **Tests:** EDGE-CASES A (state machine incl. A11), B3–B5 (financials/snapshot), D1/D4 (Purpose
  + answer store), F1/F4 (token hash, append-only). One reversible migration.
- **No UI.** ✅ Checkpoint: suite green.

## Slice B — Invitation + email + magic-link entry
- API: create invitation (type, selected onboarding templates, email **default
  `vieshall@hypernix.net`**), send via `IEmailSender`; token-resolve endpoint (opens/creates the
  application, scoped); resend / revoke; expiry.
- `IEmailSender` abstraction + config-driven transport + **non-prod `TestRecipientOverride`**
  routing all onboarding email to `vieshall@hypernix.net`. No secrets in code.
- Buyer UI: Vendor Master → **New Vendor chooser** (manual / invite); the **Invite** screen
  (type toggle, pack selection from Onboarding templates, doc checklist; email input defaulted).
- Vendor UI: magic-link **landing** (limited scope, no login).
- **Tests:** EDGE-CASES E1–E5, A1–A2, A9–A10, F1–F2. ✅ Checkpoint.

## Slice C — Vendor onboarding form (unified, live scoring, save/resume)
- Vendor UI: one multi-section form — company, banking, documents (`StoredFile`), SWEC
  categories, financials (Non-SWEC, **live Altman Z**), the selected packs **rendered via the
  existing form renderer**, review, submit. Save-and-resume via the token.
- API: autosave draft; submit (validate required core; snapshot financials).
- **Tests:** EDGE-CASES B1–B2/B6, D2–D3, A3; save/resume; financial live-compute parity with
  the prototype. ✅ Checkpoint (3 sections built → review → checkpoint).

## Slice D — Buyer review + batched clarification + approve/reject + promote
- Buyer UI: Onboarding **queue**; **Review** screen (profile/banking/financial band + calc
  drawer/questionnaire/documents) with action bar.
- **Batched clarification:** flag N items → one round → one email; vendor **resubmit** view shows
  only flagged items → resubmit → back to review. Both directions.
- **Approve** → promote to `Vendor` master + provision `VendorUser` (set-password email) + copy
  assessment; **Reject** (reason email). Duplicate check at approve. Emails at each transition.
- **Manual entry** path finalised (straight to master, no approval; workflow seam noted).
- **Tests:** EDGE-CASES A4–A8, C1–C2, E4, F3, and the full **E2E G3**; confirm G1/G2. ✅ Checkpoint.

## Slice E — Forms page: manage Onboarding templates (no-code packs)
- Extend the existing Forms page to create/edit/publish **Onboarding-purpose** templates and
  filter by purpose, so supplier admins maintain question packs without a developer.
- Invite step reads live from published Onboarding templates.
- **Tests:** create an Onboarding template → appears in invite selector → renders in the vendor
  form → answers persist as `OnboardingAnswer`; D5. ✅ Checkpoint: full suite green.

# Claude Code — copy-paste prompts (one per slice)

Run in order. Paste one, let it finish, confirm green, paste the next. Package lives at
`docs/vendor-onboarding/`, prototype at `prototype/vendor-onboarding-mockup.html`.

---

## PROMPT — Slice A (backend foundation)

```
Read, in order, before writing any code:
  1. CLAUDE.md
  2. docs/vendor-onboarding/README-FIRST.md  (GOLDEN CONSTRAINTS — obey all 10; design from the 5 roles)
  3. docs/vendor-onboarding/DATA-MODEL-ANALYTICS.md  (BINDING)
  4. docs/vendor-onboarding/ENGINEERING-STANDARDS.md  (BINDING)
  5. docs/vendor-onboarding/VENDOR-ONBOARDING-SPEC.md  (§2, §3, §5, §9)
  6. docs/vendor-onboarding/EDGE-CASES.md  (A, B, D1/D4, F1/F4)
For any ambiguity, open prototype/vendor-onboarding-mockup.html and match its behaviour —
especially the financial model (altmanZ, weighted, bandFor, scoreFor) and lifecycle (LC, mkApp).

Slice A: BACKEND ONLY, NO UI. Additive. Staging is SEPARATE from the Vendor master.

Do:
- Add entities: VendorOnboardingInvitation (HASHED token, email, type, selected template ids,
  ExpiresUtc, status, invited-by); VendorOnboardingApplication (staging profile/contacts/
  addresses/banking/certs value objects mirroring Vendor, categories, lifecycle status, source,
  invitation link, SubmittedUtc/DecisionUtc/PromotedVendorId?); VendorFinancialAssessment
  (11 line items x3 typed decimals + AS-OF snapshots of weighted Z/score/band/risk/statement +
  finance remarks); OnboardingClarificationRound + OnboardingClarificationItem (APPEND-ONLY,
  directional); OnboardingAnswer (FormItemId, value).
- Extend FormTemplate with Purpose (Rfq|Onboarding|Both). SEED 5 onboarding Form Templates:
  HSE, Quality & Certification, Capability & Capacity, Client References, Compliance & Declarations.
- Enums (stable values): OnboardingStatus, ApplicationSource, ClarificationDirection, round
  LinkStatus, FinancialBand, RiskCategory.
- Financial service: EXACT Altman Z' from the prototype (X1x0.717,X2x0.847,X3x3.107,X4x0.420,
  X5x0.998; years 0.2/0.3/0.5; bands A>=2.9,B>=2.0,C>=1.23,D<1.23; score=clamp(round(Z/4.5*100),
  5,99)) as pure functions + a snapshot helper.
- Transitions as DOMAIN METHODS on the aggregate (Submit, StartReview, RequestClarification(items),
  Resubmit, Approve, Reject, Revoke, Expire) throwing DomainRuleException on illegal moves.
  Reuse NumberSequence (VOB-2026-####), AuditEntry (TYPED columns), IClock. Add a workflow-seam
  step list (v1 length 0-1) in its own shape. Index analytics/queue columns. Declare grain in
  each entity's XML doc-comment. Money = decimal; dates = DateOnly; times = DateTimeOffset/UTC.

Test in detail: EDGE-CASES A1-A11 (incl. illegal transition), B3-B5, D1/D4, F1/F4. One
reversible migration. STOP; report entities, migration name, test count, the exact Z test
vectors used, all green. No UI.
```

---

## PROMPT — Slice B (invitation + email + magic link)

```
Read first: README-FIRST.md, VENDOR-ONBOARDING-SPEC.md (§1, §6), EDGE-CASES.md (E, A1-A2/A9-A10,
F1-F2). Match ivSend + the magic-link landing in the prototype for behaviour; use the app's
existing style package for visuals.

Slice B. Additive. Keep existing Vendor Master and Forms untouched except the additive New
Vendor entry.

Do:
- IEmailSender abstraction with a config-driven transport (SMTP from configuration, NO secrets
  in code), a fake for tests, and a NON-PRODUCTION recipient override
  (Onboarding:TestRecipientOverride) that routes ALL onboarding emails to one address.
- API: create invitation (registration type, selected Onboarding FormTemplate ids, vendor email
  DEFAULTING to vendor-invites@hypernix.test) -> send invite email with the magic link; token-resolve
  endpoint that opens/creates the scoped application; resend; revoke; expiry handling.
- Buyer UI: Vendor Master -> New Vendor CHOOSER (Enter manually | Invite vendor); the Invite
  screen (type toggle, pack selection from Onboarding templates, document checklist, vendor
  email input PRE-FILLED with vendor-invites@hypernix.test).
- Vendor UI: magic-link LANDING page (limited onboarding scope, no login).

Test in detail: EDGE-CASES E1-E5 (fake IEmailSender asserts recipient/subject/link; override
routes to vendor-invites@hypernix.test; default input value), A1-A2, A9-A10, F1-F2. Run lint, typecheck,
full suite. STOP and report.
```

---

## PROMPT — Slice C (vendor onboarding form)

```
Read first: README-FIRST.md, VENDOR-ONBOARDING-SPEC.md (§3, §5, §7), EDGE-CASES.md (B, D2-D3, A3).
Match vSection/finSet/vSubmit in the prototype for behaviour; RENDER question packs with the
EXISTING form renderer (the same components the RFQ questionnaire uses). Existing style package
for visuals; take only layout/structure from the prototype.

Slice C. Additive.

Do:
- Vendor UI: one seamless multi-section form via the magic link — company & contact, banking,
  documents (StoredFile upload), SWEC categories, financials (Non-SWEC only, with LIVE Altman Z
  updating as they type), then the selected question packs rendered from their FormTemplates,
  then review & submit. Save-and-resume via the token (autosave draft).
- SWEC applications skip the financials section entirely (waiver).
- API: autosave draft; submit (validate required core fields; SNAPSHOT the financial assessment
  on submit).

Test in detail: EDGE-CASES B1-B2, B6, D2-D3, A3; save/resume; live financial compute PARITY with
the prototype's numbers. Build core sections then review, checkpoint. Run lint, typecheck, full
suite. STOP and report.
```

---

## PROMPT — Slice D (buyer review + batched clarification + approve/reject + promote)

```
Read first: README-FIRST.md, VENDOR-ONBOARDING-SPEC.md (§4, §7, §8), DATA-MODEL-ANALYTICS.md
(§1-2 append-only + promotion), EDGE-CASES.md (A4-A8, C, E4, F3, G3). Match sendClar/resubmit/
approveApp/rejectApp/showCalc in the prototype.

Slice D. You will finalise the manual path and wire promotion. Additive; do not break existing
tests. Never delete a clarification round/item; never create Vendor/VendorUser before approval.

Do:
- Buyer UI: Onboarding QUEUE (status + clarification-round count); REVIEW screen (profile,
  banking, financial band + a "view calculation" drawer, questionnaire answers, documents) with
  an action bar.
- BATCHED clarification: buyer flags N items -> ONE round -> ONE email to the vendor. Vendor's
  resubmit view shows ONLY the flagged items -> resubmit all -> back to UnderReview. Support both
  directions (vendor can raise a round too).
- APPROVE -> promote application into the Vendor master (copy profile/contacts/addresses/banking/
  certs/categories), set status (SWEC->Registered, Non-SWEC->Provisional), copy the financial
  assessment, provision a VendorUser with a set-password email, set PromotedVendorId, audit.
  Duplicate check (reg. no./name) at approve. REJECT -> reason + email. Emails at each transition
  via IEmailSender.
- MANUAL entry -> straight to master, NO approval (leave the workflow-seam note; do not build the
  engine).

Test in detail: EDGE-CASES A4-A8, C1-C2, E4, F3; the full E2E G3 (invite email to
vendor-invites@hypernix.test -> fill -> submit -> clarify 2 items one email -> resubmit -> approve ->
master + VendorUser + assessment copied, with audit + snapshots asserted); confirm G1-G2. Run
lint, typecheck, full suite. STOP and report full green.
```

---

## PROMPT — Slice E (Forms page: manage Onboarding templates)

```
Read first: README-FIRST.md, VENDOR-ONBOARDING-SPEC.md (§5), EDGE-CASES.md (D5). Reuse the
existing Forms page and FormItem engine — do NOT build a second question builder.

Slice E. Additive.

Do:
- Extend the existing Forms page to create/edit/PUBLISH Onboarding-purpose FormTemplates and
  filter templates by Purpose, so supplier admins maintain question packs without a developer.
- The Invite step (Slice B) reads live from published Onboarding templates.

Test in detail: create an Onboarding template -> it appears in the invite selector -> renders in
the vendor form -> answers persist as OnboardingAnswer (D5); editing a pack affects new invites
only, not already-answered applications. Run lint, typecheck, full suite. STOP and report.
```

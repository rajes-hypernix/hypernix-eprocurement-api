# Vendor Onboarding — Build Package

Adds **vendor onboarding** to eProcure: a buyer invites a supplier (or enters one manually),
the supplier self-registers through a secure magic link (profile, banking, documents,
financials, and a configurable questionnaire), and the buyer reviews and **approves / rejects /
requests clarification** before the supplier is promoted into the Vendor Master. Additive —
nothing existing is removed.

## What is in here

| File | Purpose |
|---|---|
| `README-FIRST.md` | This file. Read first. The golden constraints + the roles. |
| `docs/vendor-onboarding/VENDOR-ONBOARDING-SPEC.md` | The full spec — flow, lifecycle, screens, email, Forms reuse. |
| `docs/vendor-onboarding/DATA-MODEL-ANALYTICS.md` | **Analytics- & workflow-ready schema — binding.** |
| `docs/vendor-onboarding/ENGINEERING-STANDARDS.md` | **Clean-code / DDD / testing bar — binding.** |
| `docs/vendor-onboarding/EDGE-CASES.md` | Every edge case + expected behaviour (test these). |
| `docs/vendor-onboarding/BUILD-PLAN-SLICES.md` | The 5 slices. |
| `docs/vendor-onboarding/CLAUDE-CODE-PROMPTS.md` | One copy-paste prompt per slice. |
| `prototype/vendor-onboarding-mockup.html` | **Behavioural oracle for this module.** |

## How to use with Claude Code

One slice at a time, in order (A → E). Paste the matching prompt from
`CLAUDE-CODE-PROMPTS.md`. After each slice: **stop, run lint + typecheck + the full test
suite, confirm green**, then continue.

## The behavioural oracle

`prototype/vendor-onboarding-mockup.html` is the standalone clickable mockup of this module —
use it exactly like `prototype/eprocure-portal.html`. When the written spec is ambiguous, open
it and match its behaviour. Read its financial model (`altmanZ`, `weighted`, `bandFor`,
`scoreFor`), its lifecycle (`LC`, `mkApp`, `sendClar`, `resubmit`, `approveApp`), the invite
flow (`ivSend`), and the vendor form (`vSection`, `finSet`, `vSubmit`). The financial model in
the prototype is the **same** one already in the colleague's first prototype — match it exactly.

## Design the whole thing from FIVE viewpoints (state which, when it matters)

- **Vendor:** one seamless form; a no-password magic link; save-and-resume via the same link;
  on a clarification, sees only the flagged items, batched; clear submitted/approved/rejected
  states. Never confused, never asked for the same thing twice.
- **Buyer / Procurement:** composes one onboarding pack (core details + selected question
  packs) at invite; one queue; one review screen; raises all clarifications as one round;
  approve promotes to master and provisions the login.
- **Solution Architect:** staging application is **separate from the Vendor master** until
  approval; reuse the existing Forms/`FormItem` engine, financial model, `StoredFile`,
  `VendorUser`, `NumberSequence`, `AuditEntry`; one review gate with an insertable step for the
  future workflow engine.
- **Programmer:** rich domain entities with transition methods, `DomainRuleException` on
  illegal moves, append-only clarification rounds, as-of financial snapshots, one reversible
  migration per slice, detailed tests.
- **CTO:** analytics-grade schema (typed dates/decimals, conformed dimensions, as-of snapshots,
  append-only events, cycle-time measurable), a workflow-engine seam, magic-link security, no
  master pollution or pre-approval credentials, no secrets in code.

## GOLDEN CONSTRAINTS — read every time, do not violate

1. **Additive.** Do not change existing Vendor Master, RFQ, bid, award, PO, GRN, invoice, or
   the existing Forms behaviour except where a slice explicitly extends it (additively).

2. **Staging ≠ master.** Nothing the vendor types touches the approved `Vendor` master until
   **approval** promotes the application. Reject/clarify never pollute the master.

3. **Reuse the Forms engine — don't build a second question system.** Question packs are
   `FormTemplate`s (existing engine, `FormItem`). The invite step *selects* templates; it does
   not define questions. Vendor answers store as `OnboardingAnswer` (mirror of `BidAnswer`).

4. **Reuse infrastructure.** `NumberSequence` for codes (`VOB-2026-####`), `AuditEntry` (typed)
   for every transition, `StoredFile` for documents, `VendorUser` provisioned **only on
   approval**, `IClock` for all time. No new code-gen, audit, or file mechanisms.

5. **Financial model is fixed.** SWEC → financial pre-qual **waived**. Non-SWEC → the exact
   Altman Z′ model from the prototype (X1×0.717, X2×0.847, X3×3.107, X4×0.420, X5×0.998;
   years weighted 0.2/0.3/0.5; bands A ≥2.9, B ≥2.0, C ≥1.23, D <1.23). Store the computed
   Z/score/band as an **as-of snapshot** at submit/decision; keep live values derived.

6. **Analytics-grade schema is non-negotiable** — follow `DATA-MODEL-ANALYTICS.md`.

7. **Hold the CTO bar** — follow `ENGINEERING-STANDARDS.md`.

8. **Email is real and testable.** Onboarding emails go through an `IEmailSender` abstraction.
   The invite form's vendor-email input **defaults to `vieshall@hypernix.net`**, and in
   non-production a config override routes ALL onboarding emails to that address so the flow can
   be tested end-to-end without real vendor inboxes. No SMTP secrets in code — config only.

9. **Manual entry has no approval** (kept simple), but the review path is modelled as an
   ordered step list of length 0–1 so the **future configurable workflow engine** can insert
   steps without reshaping tables.

10. **Report before large or risky edits**, work in batches of ~3 screens then checkpoint, and
    **when in doubt: the prototype wins on behaviour; the existing app wins on style.**

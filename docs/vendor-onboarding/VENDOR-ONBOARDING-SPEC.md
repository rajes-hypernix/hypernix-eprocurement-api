# Vendor Onboarding — Specification

> Behaviour source of truth: `prototype/vendor-onboarding-mockup.html`.
> Style source of truth: the app's existing design tokens/primitives (not the mockup's exact hex/fonts).
> Additive. Staging application is separate from the Vendor master until approval.

---

## 1. The flow (end to end)

1. **Vendor Master → New Vendor** offers two paths that both converge on the same records:
   - **Enter manually** — buyer keys the supplier in; written **straight to the master, no
     approval** (v1). Simple. (Workflow seam left for later — see §9.)
   - **Invite vendor** — buyer composes an onboarding pack and sends a secure link.
2. **Invite** — buyer sets vendor email (**defaults to `vieshall@hypernix.net`**), registration
   type (SWEC / Non-SWEC), and selects which **question packs** (Form Templates) to include on
   top of the always-on core details. Sends a **magic link** (signed, 14-day expiry, no
   password). Email delivered via `IEmailSender`.
3. **Vendor self-service** (via link, limited onboarding scope, no login): one seamless
   multi-section form — company & contact, banking, documents, SWEC categories, financials
   (Non-SWEC only, with **live Altman Z scoring**), the selected question packs, review, submit.
   Save-and-resume via the same link.
4. **Buyer review** — application lands in the Onboarding queue. Buyer reviews profile, banking,
   financial band + calculation, questionnaire answers, and documents, then decides:
   **Approve**, **Reject** (reason), or **Request clarification** (batched — see §4).
5. **Approve** — application is **promoted** into the `Vendor` master (status Registered for
   SWEC, Provisional for Non-SWEC pending finance conditions), a `VendorUser` login is
   provisioned (set-password email), and the financial assessment is copied onto the vendor.

---

## 2. Lifecycle (state machine)

```
Draft (manual, unsent)
Invited ─► InProgress ─► Submitted ─► UnderReview ─┬─► Approved  → Vendor master + VendorUser
   │                                               ├─► Rejected  (reason, terminal)
   │                          ClarificationRequested ⇄ Resubmitted ─► UnderReview
Expired / Revoked / Withdrawn (terminal)
```
Every transition runs through a **domain method** on the aggregate (`Submit`, `StartReview`,
`RequestClarification`, `Resubmit`, `Approve`, `Reject`), throws `DomainRuleException` on
illegal moves, writes a typed `AuditEntry`, and stamps time via `IClock`.

---

## 3. Registration type & financial pre-qualification

- **SWEC** → financial pre-qualification **waived** (verified against the PETRONAS LLRC). No
  financials section.
- **Non-SWEC** → the vendor keys **11 financial line items × 3 years** (RM'000): revenue, net
  profit, EBIT, total/current assets, inventory, current/total liabilities, equity, retained
  earnings, fixed assets. The system computes **8 ratios** and the **Altman Z′ private-firm
  model**, weighted across years, → score/100 → band A–D → risk → auto conditional statement +
  free-text finance remarks.
- **Exact model (do not change):** X1=(CA−CL)/TA ×0.717, X2=RE/TA ×0.847, X3=EBIT/TA ×3.107,
  X4=Equity/TL ×0.420, X5=Revenue/TA ×0.998; year weights **FY-2 ×0.2 / FY-1 ×0.3 / current
  ×0.5**; bands **A ≥2.9 · B ≥2.0 · C ≥1.23 · D <1.23**; score = clamp(round(Z/4.5×100),5,99).
  Thresholds/weights/wording are indicative — SPSB Finance finalises (B4.8). Interim Excel
  upload supported. **Store the computed Z/score/band/statement as an as-of snapshot** at
  submit and at decision; live values are derived.

---

## 4. Clarification — item-level, batched into rounds (both directions)

- The buyer flags **multiple items** ("ISO cert missing", "add FY2025 accounts"), then sends
  them as **one round → one email**. The vendor never gets drip-fed.
- The vendor's resubmit view shows **only the flagged items**, batched; they fix all and
  resubmit the whole round at once → `Resubmitted` → `UnderReview`.
- A round has a **direction** (BuyerToVendor / VendorToBuyer) so the vendor can likewise raise
  several points back as one round.
- Rounds and items are **append-only** — never edited/deleted after sending; you get a clean
  record of how many rounds each onboarding took.

---

## 5. Questionnaire = Form Templates (reuse the existing Forms engine)

- A **question pack is a `FormTemplate`** built on the existing Forms page, using the existing
  `FormItem` engine (12 field types, sections, required, help, order).
- `FormTemplate` gains a **`Purpose`** (`Rfq | Onboarding | Both`) so the Forms page can filter
  onboarding forms and the invite step only offers those.
- The invite step **selects** one or more onboarding templates; it does not define questions.
- Vendor answers store as **`OnboardingAnswer` (FormItemId, value)** — a mirror of `BidAnswer`.
- The vendor form **renders packs with the existing form renderer** (same components as the RFQ
  questionnaire), so it looks and behaves natively.
- **Starter packs to seed as Form Templates** (buyer can prune/extend on the Forms page): HSE,
  Quality & Certification, Capability & Capacity, Client References, Compliance & Declarations.
- **Document checklist** (attach via `StoredFile`): SSM/CCM (required), ISO 9001 (optional,
  expiry), CIDB grade (optional, expiry), Bank Confirmation (required), Audited Accounts
  (Non-SWEC), PETRONAS SWEC cert (SWEC). Mandatory-by-type enforced.

---

## 6. Email (real, testable)

All onboarding emails go through an **`IEmailSender`** abstraction (no SMTP secrets in code —
config only). Messages:
- **Invitation** — magic link (to the vendor).
- **Reminder** — if not started by day 7 (optional in v1; wire the hook).
- **Clarification requested** — to the vendor, listing the round's items.
- **Approved** — to the vendor, with a set-password link (provisions `VendorUser`).
- **Rejected** — to the vendor, with the reason.

**Testability:** the invite form's vendor-email input **defaults to `vieshall@hypernix.net`**.
In non-production, a config override (`Onboarding:TestRecipientOverride`) routes **every**
onboarding email to that address, so the full flow is testable without real vendor inboxes.
Tests use a fake `IEmailSender` asserting recipient, subject, and that the magic link resolves.

---

## 7. Screens

**Buyer:** Vendor Master (+ New Vendor chooser), Invite (type + pack selection + doc checklist),
Onboarding queue (applications by status + clarification-round count), Review (profile /
banking / financial band + calculation drawer / questionnaire / documents; action bar
Approve / Reject / Request clarification).
**Vendor (magic link):** landing (limited scope), the unified multi-section onboarding form
(live financial scoring), submitted state, and the clarification-resubmit view (flagged items
only).

---

## 8. Promotion on approve (exact)

On **Approve**: create/complete the `Vendor` (copy profile, contacts, addresses, banking, certs,
categories from the application), set status (SWEC→Registered, Non-SWEC→Provisional), copy the
`VendorFinancialAssessment` onto the vendor, provision a `VendorUser` (set-password email), mark
the application `Approved` with `PromotedVendorId`, and audit. **Duplicate check** at approve
(and at invite): warn if registration no./name matches an existing vendor.

---

## 9. Workflow-engine seam (design now, build later)

The review is modelled as an **ordered list of approval steps** (v1: manual = 0 steps; invite =
1 review step, with a Finance sub-step available for Non-SWEC). Keep the decision/step data in
its own shape so a future **configurable workflow engine** can drive it for any record type
without reshaping these tables. Do **not** build the engine now — just don't hard-wire a single
approver in a way that blocks it.

---

## 10. Out of scope for v1 (track, don't build)

Configurable workflow engine; multi-step approver routing UI; automated financial-doc parsing;
vendor performance scorecards; NetSuite vendor sync (stub). Manual-entry approval (seam only).

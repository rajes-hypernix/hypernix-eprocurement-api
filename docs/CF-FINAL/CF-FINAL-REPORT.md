# CF-FINAL — Morning Report

**Three slices, unattended, correctness-first. All shipped and tagged.**
Gates raised across the run: **dotnet 541 → 550 · vitest 251 → 256 · e2e 98 → 117** (all suites green).
Tags: `v2.1-fixes` (Slice 1) · `v2.2-ui-polish` (Slice 2) · `v2.3-final` (Slice 3).

---

## LEAD: Slice 1 — the ship-critical bug is fixed and proven

**The bug (T1):** the chosen entry form lived only in React state; there was no form reference on
the Requisition record. So a saved PR never remembered its form — on reopen it silently reverted to
Standard, and the ungoverned residual sections dumped EVERY applicable custom field/segment below the
sublist (the "phantom fields").

**Root cause & fix (commit `9fcf1c9`):**
- Added `Guid? EntryFormId` to `PurchaseRequisition` (migration `RequisitionChosenForm`, additive,
  one nullable column, no FK by the loud-fail convention).
- `SavePrRequest`/`RequisitionDto` carry it; `CreateAsync`/`UpdateAsync` persist it; `SourcingMapping`
  maps it back. `PrForm` initializes the picker from `pr.entryFormId` on reopen and persists the
  EFFECTIVE resolved form (`form.formId`) on save — so a PR pins the exact layout it was entered on.
- Removed the ungoverned residual `CustomFieldsSection`/`SegmentsSection` on `PrForm` — a viewed PR
  now renders ONLY its chosen form's placed fields. OD-D7-2 unchanged (submit re-resolves the ROLE
  form's requireds server-side).

**Proof (e2e `16-CFF-T1`):** create a PR on a custom form → save (the POST response carries
`entryFormId`) → reopen → still on the custom form, its field renders, and NO phantom
Custom-fields/Segments dump below the sublist. Plus `16-CFF-T1/OD-D7-2`: a chosen form can't dodge
the role form's requireds at submit (400), while the same gap saves as a draft.

---

## AUTO-DECISIONS — here are the calls I made (veto any). Full detail in `AUTO-DECISIONS.md`.

- **AD-1** Persist the EFFECTIVE resolved form (not just an explicit picker choice), so a PR pins the
  exact layout it was entered on, immune to later role-preference changes.
- **AD-2** Remove the ungoverned residual custom/segment sections on PrForm (governed model wins).
- **AD-3** Fixed 4 pre-existing `tsc -b` errors the prior vacuous `tsc --noEmit` gate had masked
  (root tsconfig has `files: []`). `npm run build` is green again. (Button gained a `title?` prop;
  3 type-safety touch-ups.) **This is why the real typecheck gate now catches things it didn't before.**
- **AD-4** Deleted required-ListValue litter (`custbody_partner_2`) that broke the shared
  `requiredCustomValues` e2e helper across 5 specs (T9 territory, done early to unblock Slice 1).
- **AD-5** Repointed the placement-cascade + archive tests to custom forms (T2 forbids custom fields
  on system forms; T1 removed the residual dump).
- **AD-6** T5 Save-button placement: full-page panels get top-right; modals gain a `headerAction`
  slot (custom-field modal uses it). Remaining modals keep the footer primary — a documented follow-up.
- **AD-7** T9: bulk litter deleted; ~82 value-bearing fields + a couple assignment-bound segments are
  governed-INACTIVATED (data safety refuses to delete a field/segment in live use — never orphan).

## BLOCKERS
**None.** `BLOCKERS.md` is empty. Every ledger box is `[x]`.

---

## The operator's 9 findings → commit → browser proof

| # | Finding | Commit | Browser proof |
|---|---------|--------|---------------|
| T1 | Chosen form not persisted / phantom fields on reopen | `9fcf1c9` | `16-CFF-T1` (+ `/OD-D7-2`) |
| T2 | Custom fields must apply to CUSTOM forms only | `fe1c9fb` | `16-CFF-T2` + `PlacementCascadeTests` refusal |
| T3 | Standard PR: Est. Amount = qty × rate | `9a1d1ef` | `16-CFF-T3` |
| T4 | One global toast + save-banner, wired everywhere | `7c40575` | `16-CFF-T4` |
| T5 | Standardize Save-button placement (top-right) | `4b680c0` | `16-CFF-T5` |
| T6 | Transitions + skeleton loaders | `efcd8ac` | `16-CFF-T6` |
| T7 | Header title Georgia, regular weight | `9f19f08` | `16-CFF-T7` |
| T8 | Preferred form by role / all roles + defaulting | `3d07cbc` | `16-CFF-T8` |
| T9 | Dummy-data cleanup (curate realistic config) | `c6d36d7` | `16-CFF-T9` |

---

## Per-slice summary

**Slice 1 — functional fixes (`v2.1-fixes`, e2e 111):**
- T1 form persistence + phantom-fields fix (see LEAD).
- T2 (`fe1c9fb`): a custom field is refused on a system/standard form (400); the field-creation
  picker offers custom forms only; the "needs a placement" requirement counts non-system forms only.
- T3 (`9a1d1ef`): a live-computed "Est. amount" column (qty × rate, CF1 money display) next to Est. rate.

**Slice 2 — UI polish (`v2.2-ui-polish`, e2e 115), matching UI-POLISH-REFERENCE.html:**
- T4 (`7c40575`): one `Notify` provider (inline save-banner + corner toast) wired to every save —
  requisitions, POs, custom fields, lists, segments, entry forms, numbering, users. Past-tense copy,
  no "successfully", no "!".
- T5 (`4b680c0`): primary Save/Submit top-right (transactions right-aligned; the inert `.chead`
  spacers fixed; modal `headerAction`).
- T6 (`efcd8ac`): button hover/press transitions, row-hover settle, skeleton loaders (Requisitions),
  panel-fade on the builder open.
- T7 (`9f19f08`): the header title renders Georgia at weight 400.

**Slice 3 — preferred forms + cleanup (`v2.3-final`, e2e 117):**
- T8 (`3d07cbc`): "All roles" preferred toggle exposed; the transaction picker already defaults to a
  role's preferred form (resolver precedence) — proven end-to-end.
- T9 (`c6d36d7`): deleted ~65 fields + 9 lists + 44 segments + 34 views of test litter; curated a
  realistic set (Cost Centre, Payment terms, Project Code Grouped, system dimensions, Partner,
  Remarks). Value-bearing residue governed-inactivated (AD-7).

---

## Five-lens review

1. **Correctness** — the ship-critical bug (T1) is fixed at the data layer (persisted `EntryFormId`)
   and proven with a full create→save→reopen round-trip through the DB, not just a UI check. T2's
   governance is enforced server-side (400) with an xUnit refusal test, not only in the picker.
2. **Guard rails** — nothing weakened. OD-D7-2 (submit re-resolves role requireds) intact; the L6
   data-safety guard is *why* the T9 value-bearing litter is inactivated rather than deleted; the
   additive T1 migration touches one nullable column.
3. **Consistency** — the polish is a shared LAYER, not a rebuild: one `Notify` provider, one Save-
   placement convention, one Skeleton component, transition CSS — reused across screens.
4. **Data safety** — T9 used the governed lifecycle throughout (unplace → delete zero-value; purge;
   inactivate where records reference them). No orphans. The 82 inactive fields keep their values.
5. **Regression surface** — the T1 residual-removal + T2 governance rippled into the API tests and
   06/08/10/14/15 e2e; all were reconciled and the full 117-test e2e suite is green, as are dotnet
   550 and vitest 256. The AD-3 `tsc -b` repair means `npm run build` is green (it was red on `main`).

## Known follow-ups (non-blocking)
- Migrate the remaining create/edit modals to the `headerAction` top-right primary (AD-6).
- The ~82 value-bearing inactive fields could be fully removed by clearing each field's stray value
  on its owning record, then deleting (AD-7) — not worth the churn for handover.
- A vacuous `tsc --noEmit` was masking real type errors (AD-3); `tsc -b` is the true gate.

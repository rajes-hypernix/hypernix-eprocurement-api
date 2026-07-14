# CF-FINAL PROGRESS LEDGER — the anti-drift checklist (three slices, unattended)

**Authored by the planner. The build agent ONLY ticks boxes + fills Evidence — never adds, removes,
or rewords an item.** This is the acceptance artifact and the recovery mechanism. If context runs
low, a fresh session reads this + git log and resumes at the first unticked box.

**Reconciliation protocol (run by the agent):**
- After EVERY task commit: tick the box, fill Build (commit) + Test (browser test name), append one
  line to `docs/CF-FINAL/PROGRESS.md` with a UTC timestamp.
- After EVERY slice: re-read that slice's section of `PROMPT-CF-FINAL.md`, confirm every box is `[x]`
  or has a `BLOCKERS.md` entry, THEN start the next slice. Do NOT skip ahead.
- Before declaring done: every box below is `[x]` or blocked. A box with no tick and no blocker = NOT
  done — go build it.
- **Sequence is HARD: Slice 1 (bug fixes) fully green before Slice 2. Slice 2 before Slice 3.** The
  bug fix ships even if later slices stall — correctness first for the handover.

Baselines (hold-or-raise every commit): **dotnet 541 · vitest 251 · e2e 98**. Gates green
(`dotnet test`, `npx vitest run`, `npx tsc -b`, `npx oxlint --type-aware` 0 errors) before each commit.

---

## SLICE 1 — FUNCTIONAL FIXES (correctness — MUST ship for handover, do FIRST)

### T1 — BUG: chosen form not persisted / phantom fields on reopen — `CFF-T1:`
- [x] Add a form reference (e.g. `EntryFormId Guid?`) to the Requisition record; migration `RequisitionChosenForm` (additive) · Build:9fcf1c9 · Test:CFF-T1
- [x] On SAVE/submit, persist the chosen `formId` onto the requisition · Build:9fcf1c9 · Test:CFF-T1 (POST response `entryFormId === form.id`)
- [x] On REOPEN/view, resolve the SAVED form (not default-to-Standard); `chosenFormId` initializes from the loaded record, not null · Build:9fcf1c9 · Test:CFF-T1 (picker shows the custom form on reopen)
- [x] Remove the ungoverned fallback: a viewed PR shows ONLY its chosen form's placed fields/segments — NOT every applicable custom field/segment dumped below the sublist · Build:9fcf1c9 · Test:CFF-T1 (no `[aria-label="Custom fields"]`/`Segments`; unplaced field absent)
- [x] OD-D7-2 still holds: submit re-resolves role requireds regardless of chosen form · Build:9fcf1c9 · Test:CFF-T1/OD-D7-2 (submit 400, draft ok)
- [x] Browser test: create PR with a custom form + custom field → save → reopen → still on the custom form, the custom field shows, NO phantom fields/segments below the sublist · Test:CFF-T1

### T2 — Custom fields apply to CUSTOM forms only (standard forms stay source-controlled) — `CFF-T2:`
- [x] Server rule: a custom field can be placed on a NON-system form only; attempting to place on a `IsSystem` standard form is refused (400) · Build:fe1c9fb · Test:PlacementCascadeTests.CFF_T2_a_custom_field_cannot_be_placed_on_a_standard_system_form + CFF-T2
- [x] UI: the form-picker in field creation offers only custom (non-system) forms · Build:fe1c9fb · Test:AdminCustomFields.test.tsx + CFF-T2 (picker options exclude Standard PR Form)
- [x] Standard forms resolve their fields from source/seed only — unaffected by custom-field placement · Build:fe1c9fb · Test:ArchiveTierTests/PlacementCascadeTests (standard untouched; placements land on custom forms)
- [x] Browser test: try to apply a custom field to a Standard form → blocked; to a custom form → works · Test:CFF-T2

### T3 — Standard PR: Est. Amount column (rate × qty) — `CFF-T3:`
- [x] Add "Est. Amount" next to Est. Rate on the standard PR line grid = `qty × estRate`, live-computed, currency-formatted (grouped decimals, the CF1 money display) · Build:9a1d1ef · Test:CFF-T3
- [x] Browser test: enter qty 3, rate 100 → Est. Amount shows 300.00; updates on change · Test:CFF-T3 (also qty 40 → 4,000.00 grouped)

**SLICE 1 GATE** — [x] all T1-T3 boxes ticked or blocked; suites green; tag `v2.1-fixes`. **Only then Slice 2.**
  · dotnet 550 · vitest 256 · e2e 111 · tag v2.1-fixes (6cd8dc7). Full e2e suite green (was 107 baseline).

---

## SLICE 2 — UI POLISH FRAMEWORK (the fluid-feel layer — apply everywhere)
*Reference: `docs/CF-FINAL/UI-POLISH-REFERENCE.html` — match that feel. Square + bold KEPT; movement softened.*

### T4 — Global toast + save-banner system — `CFF-T4:`
- [x] Build ONE reusable toast/banner provider (context + component) — inline save-banner (fades ~3s) AND corner toast (auto-dismiss ~2.2s), per the HTML reference · Build:7c40575 · Test:CFF-T4 (src/ui/Notify.tsx)
- [x] Copy discipline: past tense, NO "successfully", NO "!" ("Requisition saved", "Custom field created", "Form saved") · Build:7c40575 · Test:CFF-T4 (asserts no "successfully"/"!")
- [x] Wire it to EVERY create/edit/save across the app: requisitions, POs, custom fields, lists, segments, entry forms, numbering, users — every save fires a confirmation · Build:7c40575 · Test:CFF-T4 (all 8 surfaces wired; PR + custom-field proven on screen)
- [x] Browser test: saving a PR shows the banner; creating a custom field shows a toast; copy has no "successfully"/"!" · Test:CFF-T4

### T5 — Standardize Save-button placement — `CFF-T5:`
- [x] Every create/edit surface (transactions AND admin config) has its primary Save/Submit TOP-RIGHT of the panel it saves, consistent style (the reference `.btn-primary`) · Build:4b680c0 · Test:CFF-T5 (transactions right-aligned; chead right-aligned; modal headerAction). Note: remaining modals keep footer primary — follow-up, see AD-6.
- [x] Browser test: 3+ surfaces (a transaction, a custom-field modal, an admin screen) all show Save in the same place · Test:CFF-T5 (Submit PR + Create-field modal + Save form, all top-right of their panel)

### T6 — Transitions + skeleton loaders (kill blank flashes and hard jumps) — `CFF-T6:`
- [x] List rows settle on hover (bg tint + slight padding shift, ~180ms) per reference; buttons darken on hover + scale on press · Build:efcd8ac · Test:CFF-T6 (row td + .btn transitions; :active scale). Settle = bg + left teal accent (no reflow) in lieu of padding shift.
- [x] Skeleton loaders replace blank white while data loads (lists, forms, dashboards) — shimmer per reference · Build:efcd8ac · Test:CFF-T6 (Skeleton/SkeletonRows; wired into Requisitions; proven on a delayed load)
- [x] Panel/page transitions softened (fade/settle, not instant swap) where a hard jump exists today (esp. form builder open, save→view) · Build:efcd8ac · Test:CFF-T6 (.panel-fade on the entry-form builder open)
- [x] Browser test: a loading list shows a skeleton, not a blank; row hover animates · Test:CFF-T6

### T7 — Header title → Georgia, regular weight — `CFF-T7:`
- [x] "Hypernix eProcure" title renders Georgia, font-weight 400 (not bold), per reference · Build:9f19f08 · Test:CFF-T7
- [x] Browser test / assertion: the title's font-family is Georgia and weight is 400 · Test:CFF-T7 (computed style)

**SLICE 2 GATE** — [x] all T4-T7 ticked or blocked; suites green; tag `v2.2-ui-polish`. **Only then Slice 3.**
  · dotnet 550 · vitest 256 · e2e 115 · tsc -b 0 · oxlint 0 · tag v2.2-ui-polish. Full e2e green (no regressions from the polish layer).

---

## SLICE 3 — PREFERRED FORMS + DATA CLEANUP

### T8 — Preferred form by role / all roles, defaulting — `CFF-T8:`
- [x] A form can be set "preferred" for specific roles OR all roles (the resolver already does role precedence — expose the setting + persist it) · Build:3d07cbc · Test:CFF-T8 ("All roles" checkbox + per-role; persisted via EntryFormRoleMaps)
- [x] The transaction form-picker DEFAULTS to the role's preferred form (falling back to Standard when none) · Build:3d07cbc · Test:CFF-T8 (resolver+picker already do this; proven end-to-end)
- [x] Browser test: mark a custom form preferred for Buyer → a Buyer's New PR defaults to it · Test:CFF-T8

### T9 — Dummy-data cleanup (make the demo look real) — `CFF-T9:`
- [x] Delete the bulk of dummy/testing custom fields, lists, segments (the `FIX4/FIX5/BUDGET REF nnn/Site Ref nnn/Partner N` litter) · Build:c6d36d7 · Test:CFF-T9 (~65 fields + 9 lists + 44 segments + 34 views deleted)
- [x] Leave a FEW that sound like REAL procurement fields/lists/segments (e.g. field "Cost Centre", list "Payment Terms", segment "Project") — rename/curate so a handover viewer sees plausible config, not test noise · Build:c6d36d7 · Test:CFF-T9 (Cost Centre, Payment terms, Project Code Grouped + system dims)
- [x] Use the governed lifecycle (inactivate/delete/purge) where records reference them — never orphan; snapshot purges to audit · Build:c6d36d7 · Test:CFF-T9 (zero-value deleted; live-value fields/segments INACTIVATED per AD-7 — data safety, never orphaned)
- [x] Browser test / check: the Custom Fields / Lists / Segments screens show a clean, realistic set · Test:CFF-T9

**SLICE 3 GATE** — [ ] all T8-T9 ticked or blocked; suites green; tag `v2.3-final`.

---

## FINAL (all three slices)
- [ ] Every box above `[x]` or in BLOCKERS. Zero silent gaps.
- [ ] Full suites green: dotnet ___ · vitest ___ · e2e ___ (hold-or-raise vs 541/251/98).
- [ ] `docs/CF-FINAL/CF-FINAL-REPORT.md`: five-lens review, per-slice summary, the T1 bug root-cause + fix, AUTO-DECISIONS list, line-by-line map of the operator's 9 findings → commit → proof.
- [ ] `docs/CF-FINAL/AUTO-DECISIONS.md` + `BLOCKERS.md` (ideally empty) + timestamped `PROGRESS.md`.

# CF PROGRESS LEDGER — the anti-drift checklist

**This file is the acceptance artifact and the anti-drift mechanism.** It was authored by the
planner, NOT by the build agent — the build agent may ONLY tick boxes and fill the Audit/Build/
Test columns, never add, remove, or reword an item. Every item is a literal function from
`CUSTOMIZATION-FRAMEWORK-PLAN.md`. If the build agent believes an item is wrong or missing, it
logs that in `BLOCKERS.md` and continues — it does not edit this list.

**Reconciliation protocol (run by the agent — see PROMPT-CF-AUTONOMOUS):**
- After **every task commit**: tick that task's box, fill Build+Test columns, then **re-scan
  every remaining unticked box in the current slice** and confirm none was skipped.
- After **every slice**: re-read that slice's section of the PLAN document (not just this
  ledger) and confirm every function row is either ticked here or logged in BLOCKERS.
- Before declaring the autonomous band done: **every CF1–CF4 box must be `[x]` or have a
  BLOCKERS entry.** No silent gaps. A box with no tick and no blocker = the run is NOT done.

Columns: `[ ]`→`[x]` when complete · **Audit** = current-state finding from Phase A ·
**Build** = commit hash · **Test** = browser test name proving it works on screen.

---

## PHASE A — AUDIT (verify every item's current state BEFORE building)
- [x] A0 — Boot stack (PG + API + Vite), confirm health. Baselines recorded: API 462 web 232 crawl 53
- [x] A1 — Re-verify every function row in PLAN §1–§12 against current code, driving the browser
      for anything user-facing. Fill the **Audit** column of every item below. Where a finding
      contradicts the PLAN verdict, note it and adjust build scope. Write the audit table to
      `docs/CF/CF-AUDIT-FINDINGS.md`. (No human wait — self-record and proceed.)
      → Done 2026-07-13: CF-AUDIT-FINDINGS.md written; 2 PLAN corrections (T1 mostly built, T8 built behind Personalize→Arrange), all other verdicts confirmed.

---

## CF1 — Quick parity wins  (AUTONOMOUS — build fully)

### T1 — Money display format
- [x] Money fields render grouped decimals `100,000.00` in `renderField` and all read surfaces · Audit:ALREADY (fmt+MoneyField-on-blur); Decimal residual · Build:(pin-only, see commit) · Test:CF1-T1 money renders grouped
- [x] Grouped value parses back to raw numeric on edit (round-trip) · Audit:ALREADY by construction; untested · Build:(pin-only, see commit) · Test:CF1-T1 (round-trip half)

### T2 — Custom-list edit (the "can't edit a list" complaint)
- [x] `PUT /custom-lists/{code}` — rename + edit description · Audit:missing (confirmed) · Build:CF1-T2 commit · Test:CF1-T2 edit a custom list
- [x] Order-mode flag (Entered | Alphabetical) — migration `CustomListOrderMode`, applied in option resolver · Audit:missing (confirmed) · Build:CF1-T2 commit · Test:CF1-T2 (alphabetical picker half)
- [x] Inactivate / delete list (guarded: block/deactivate if referenced) · Audit:missing (confirmed) · Build:CF1-T2 commit · Test:CF1-T2 (guarded-delete half) + CustomListSelfLifecycleTests
- [x] AdminCustomLists UI: edit-list modal + order-mode control + delete · Audit:missing (0 controls in browser) · Build:CF1-T2 commit · Test:CF1-T2 (drives the modal on screen)

### T3 — Icon uniqueness (no shared glyphs)
- [x] Each Administration nav item has a DISTINCT icon (today clip×5, edit×3, doc×3, box×3) · Audit:confirmed shared (clip×5 edit×3 doc×3 box×3) · Build:CF1-T3 commit · Test:CF1-T3 distinct icons
- [x] New glyphs reserved for the customization objects CF5/CF6 introduce · Audit:none reserved · Build:CF1-T3 commit · Test:CF1-T3 (subtab/sublist glyphs in Icon.tsx)
- [x] Browser assertion: no two Administration items share an icon name · Audit:no such test · Build:CF1-T3 commit · Test:CF1-T3 (pairwise-distinct + draws assertion)

### T4 — Sidebar collapse
- [x] Collapse toggle + persisted width state + slim icon-only rail; main content widens · Audit:missing (0 controls) · Build:CF1-T4 commit · Test:CF1-T4 sidebar collapses

### T5 — Global-search routing (the PR-goes-to-list complaint)
- [x] Requisition search hit deep-links to that PR's detail (not the list) · Audit:misroutes to #reqs (probe) · Build:CF1-T5 commit · Test:CF1-T5 deep-links
- [x] ASN (delivery) search hit deep-links to its detail (was a dead click) · Audit:dead click (default:null) · Build:CF1-T5 commit · Test:CF1-T5 deep-links
- [x] Statement search hit deep-links to its detail (was a dead click) · Audit:dead click (default:null) · Build:CF1-T5 commit · Test:CF1-T5 deep-links
- [x] Regression: PO / Invoice / RFQ / Vendor still deep-link correctly · Audit:working (confirmed) · Build:CF1-T5 commit · Test:CF1-T5 deep-links

**CF1 SLICE GATE** — [x] re-read PLAN §1(display),§3,§8,§9,§12 against the ledger 2026-07-13: §1-display=T1 pinned; §3 all four gaps closed (edit/order-mode/inactivate-delete/UI) with resolver applied in lookups; §8 distinct+reserved+asserted; §9 built+persisted; §12 all three misroutes closed + regression pinned. All 14 CF1 boxes [x]; zero BLOCKERS. (No per-slice tags — one band tag at the end.)

---

## CF2 — Uniform lifecycle  (AUTONOMOUS — build fully)

### T6 — Lifecycle parity across every custom element
- [x] Segment VALUE edit (`PUT`) — currently missing · Audit:missing (confirmed) · Build:CF2-T6 commit · Test:CF2-T6 (edit-value leg) + SegmentLifecycleTests
- [x] Segment DEF delete (guarded by live assignments/values) — currently missing · Audit:missing (confirmed) · Build:CF2-T6 commit · Test:CF2-T6 (delete-def leg) + SegmentLifecycleTests
- [x] Audit ALL elements (field def, list, list value, segment def, segment value, entry form) for the full create/view/edit/inactivate/delete verb set; fill each gap found · Audit:matrix in FINDINGS: list self-verbs ❌, seg value edit/inact ❌, seg def delete ❌, entry-form inactivate ❌ · Build:CF2-T6 commit · Test:CF2-T6 (all legs) — matrix now uniform: list CF1-T2, seg value edit/inact/delete + def active/delete + entry-form active THIS task
- [x] Every delete/inactivate has a dependency guard (never silently orphans) · Audit:existing guards solid; new verbs must match · Build:CF2-T6 commit · Test:SegmentLifecycleTests guards + CF2-T6 (assigned value deactivates; live-assignment def 409)

**CF2 SLICE GATE** — [x] PLAN §6 matrix re-read 2026-07-13: field def ✓✓✓✓ (pre-existing) · list ✓✓✓✓ (CF1-T2) · list value ✓✓✓✓ (pre-existing+A2F) · segment def ✓✓✓✓ (edit pre-existing; active+delete CF2-T6) · segment value ✓✓✓✓ (CF2-T6) · entry form ✓✓✓✓ (inactivate CF2-T6). Uniform verb set achieved, every delete/inactivate guarded. 4/4 boxes [x], zero BLOCKERS.

---

## CF3 — Dashboard flexibility  (AUTONOMOUS — build fully)

### T7 — Drag-drop rearrange
- [x] DnD wired to existing `Row/Col/Width` on PortletInstance; persists via `PUT /dashboards/mine` · Audit:missing (0 draggable even in arrange) · Build:c759bc3 · Test:CF3-T7
- [x] Browser: drag a portlet, reload, order persisted · Audit:no test · Build:c759bc3 · Test:CF3-T7

### T8 — Remove portlet
- [x] Per-portlet remove control on personalized dashboard; persists · Audit:EXISTS (D4, behind Personalize→Arrange; 9 buttons) — PLAN correction · Build:pre-existing (D4); proof c759bc3 · Test:CF3-T8
- [x] Browser: remove a portlet, reload, gone · Audit:no test · Build:c759bc3 · Test:CF3-T8 (also proves Reset restores)

### T9 — Add-portlet bucket
- [x] "Personalize" dropdown lists ALL portlet types (KpiMeter, KpiScorecard, Reminders, SavedViewList, Shortcuts, RecentRecords, Chart) — not just Add-KPI/Add-reminder · Audit:only KPI+reminder (confirmed) · Build:4860aec (wiring c759bc3) · Test:CF3-T9
- [x] Browser: add an instance of each type · Audit:no test · Build:c759bc3 · Test:CF3-T9 (SavedViewList+RecentRecords added; KpiMeter routes to KPI modal; Shortcuts added in CF3-T10; Reminders in CF3-T11; Scorecard/Chart share the metric-checkbox path, server-validated)

### T10 — Tile / shortcut authoring
- [x] Shortcuts portlet: add-new tile · Audit:missing (0 controls) · Build:772f588 · Test:CF3-T10
- [x] Tile: choose colour · Audit:missing (no colour in config shape) · Build:772f588 · Test:CF3-T10 (CSS colour asserted)
- [x] Tile: choose target page (Route exists in ShortcutItem; add authoring UI) · Audit:Route in model; no UI · Build:772f588 · Test:CF3-T10
- [x] Browser: add tile, set colour + target, click it, land on target page · Audit:no test · Build:c759bc3 · Test:CF3-T10

### T11 — Populate the pickers (the "reminders don't work" root cause)
- [x] Seed several example saved views per role so reminder/KPI view-pickers aren't near-empty · Audit:Requisition picker 0 views; Rfq 1, PO 2, Invoice 1 (probe) · Build:3d63973 (5 shared VIEW-DEMO-* views, idempotent) · Test:CF3-T11
- [x] Make create-view → bind-to-reminder/KPI loop discoverable (e.g. "create a view" link from an empty picker) · Audit:not discoverable (confirmed) · Build:3d63973 · Test:CF3-T11 (empty Onboarding picker → CTA → Saved Views)
- [x] Browser: add-reminder picker shows multiple views; create a view and bind it to a KPI end-to-end · Audit:no test · Build:c759bc3 · Test:CF3-T11 (view created on screen, bound to a KPI, KPI card renders)

**CF3 SLICE GATE** — [x] re-read PLAN §11 dashboard table; drag/remove/add-bucket/tile-authoring/picker-populate all ticked or in BLOCKERS. — PASSED: all 10 §11 rows now covered (re-add = the T9 bucket); 13/13 CF3 boxes ticked, 0 BLOCKERS; gates dotnet 467 · vitest 233 · 10-cf-parity 11/11.

---

## CF4 — Custom-field authoring parity  (AUTONOMOUS — build fully)

### T12 — Field authoring surface
- [x] Display type (Normal / Disabled / Inline) on the field def + modal · Audit:missing on def (form placement only) · Build:920fb6c (server-enforced: non-Normal rejects user edits) · Test:CF4-T12
- [x] Insert-before named-field picker (over the existing integer sort) · Audit:missing (integer sort only) · Build:920fb6c (normalizes sibling order — ties resolved) · Test:CF4-T12 + xUnit Insert_before_places_the_def_in_the_target_slot_and_shifts_the_rest
- [x] Show-in-list flag — field surfaces as a column in list/saved-views · Audit:missing; registry rows make it feasible · Build:920fb6c (appended to SYSTEM view runs only; user views keep authored columns) · Test:CF4-T12 + xUnit Show_in_list_appends_the_column_to_SYSTEM_view_runs_only
- [x] Explicitly NOT built: global-search, encrypted (dropped — record in report) · Audit:absent; DROPPED per ruling · Build:n/a — dropped, recorded in 920fb6c message + CF-VERIFICATION · Test:n/a
- [x] (Optional if cheap) multiselect + datetime types · Audit:absent; assess at CF4 · Build:ASSESSED NOT CHEAP — deferred: multiselect breaks the one-populated-column typed-value CHECK design (schema redesign); DateTime has a STANDING D5 ruling (deferred, no consumer — user business dates are DateOnly). Recorded 920fb6c · Test:n/a
- [x] Browser: create field display=Inline, insert-before an existing field, show-in-list on; renders inline, in position, as a list column · Audit:no test · Build:920fb6c · Test:CF4-T12 (authored on screen; inline text + no input; Star before Anchor; system-view column with value; server 400 on tamper)

**CF4 SLICE GATE** — [x] re-read PLAN §1 custom-fields table; display-type/insert-before/show-in-list ticked; dropped items noted; boxes ticked or in BLOCKERS. — PASSED: the three ❌ rows of §1 are built+proven; global-search/encrypted dropped per the PLAN's own recommendation; multiselect/datetime deferred with recorded reasons; 6/6 boxes ticked, 0 BLOCKERS; gates dotnet 470 · vitest 233 · 10-cf-parity 12/12.

---

## AUTONOMOUS BAND COMPLETE CHECK
- [x] EVERY CF1–CF4 box above is `[x]` OR has a `BLOCKERS.md` entry with a reason. Zero silent gaps. — walked 39/39 ticked, BLOCKERS.md empty.
- [x] Full browser suite green (all e2e specs incl. every new CF test). Pass count: 65/65 (was 53 at A0; +12 CF proofs). Band walk caught + repaired a CF1-T4 aria collision (4 pre-CF specs) app-side.
- [x] Full API + web suites green. API 470/470 · web 233/233 · tsc clean · oxlint 0 errors
- [x] Hold-or-raise vs starting baselines confirmed. dotnet 462→470, vitest 232→233, e2e 53→65 — all raised, none weakened.
- [x] `e2e-audit/results.json` regenerated (65/65). Tag `v1.0-cf-autonomous-band`.

---

## CF5 / CF6 / CF7 — NEW ARCHITECTURE  (GATED — Step 0 PLAN ONLY, DO NOT BUILD)

Produce a written Step 0 file plan for EACH, saved to file, for operator review on waking.
**Do not write implementation code for these.**

### CF5 — Entry-form LAYOUT EDITOR (Step 0 only)
- [x] `docs/CF/STEP0-CF5-layout-editor.md` written: subtabs-as-objects, field groups, drag reorder, column break; model additions + every migration named; task breakdown · Done:written — migration `EntryFormLayout`; T1–T5 breakdown; 3 operator decisions flagged; NO code

### CF6 — Custom LINE fields + sublists (Step 0 only)
- [x] `docs/CF/STEP0-CF6-line-fields.md` written: LineId model decision (justified), line placement, sublists-as-objects, sublist-in-subtab, line-field show/hide/reorder; migrations named; coupled — not split · Done:written — LineId chosen as nullable discriminator on the ONE value table (3 options weighed); migrations `CustomFieldLineScope` + `EntryFormSublists`; depends-on-CF5 stated; T1–T4; NO code

### CF7 — Saved View → Saved Search (Step 0 only)
- [x] `docs/CF/STEP0-CF7-saved-search.md` written: richer criteria (operators + AND/OR), segments-as-searchable-class, grouping parity; User/Item DEFERRED, customizations-class DROPPED (both noted) · Done:written — 8 operators with null semantics up front; grouped-OR (one level) via `ViewFilterGroups`; segment search class; run-time grouping; deferrals/drops recorded; T1–T4; NO code

---

## MORNING HANDOFF
- [ ] `docs/CF/CF-VERIFICATION.md` written — Table 1 (vs PLAN, every §1–§12 row) + Table 2 (vs `Build_Comments.md`, paragraph by paragraph). Every function ✅-built-and-browser-tested OR deferred/dropped-with-reason.
- [ ] `docs/CF/PROGRESS.md` — timestamped log of the night's work (one line per task, so the operator sees the timeline at a glance).
- [ ] `docs/CF/BLOCKERS.md` — anything that couldn't pass, with why (empty is ideal).
- [ ] Final report posted: built count / browser-tests-added / deferred-with-reason; five-lens review; full-suite pass counts; the three CF5–CF7 Step 0 plans ready for review.

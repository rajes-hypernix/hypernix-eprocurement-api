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
- [ ] Each Administration nav item has a DISTINCT icon (today clip×5, edit×3, doc×3, box×3) · Audit:confirmed shared (clip×5 edit×3 doc×3 box×3) · Build:___ · Test:___
- [ ] New glyphs reserved for the customization objects CF5/CF6 introduce · Audit:none reserved · Build:___ · Test:___
- [ ] Browser assertion: no two Administration items share an icon name · Audit:no such test · Build:___ · Test:___

### T4 — Sidebar collapse
- [ ] Collapse toggle + persisted width state + slim icon-only rail; main content widens · Audit:missing (0 controls) · Build:___ · Test:___

### T5 — Global-search routing (the PR-goes-to-list complaint)
- [ ] Requisition search hit deep-links to that PR's detail (not the list) · Audit:misroutes to #reqs (probe) · Build:___ · Test:___
- [ ] ASN (delivery) search hit deep-links to its detail (was a dead click) · Audit:dead click (default:null) · Build:___ · Test:___
- [ ] Statement search hit deep-links to its detail (was a dead click) · Audit:dead click (default:null) · Build:___ · Test:___
- [ ] Regression: PO / Invoice / RFQ / Vendor still deep-link correctly · Audit:working (confirmed) · Build:___ · Test:___

**CF1 SLICE GATE** — [ ] re-read PLAN §1(display),§3,§8,§9,§12; every box above ticked or in BLOCKERS; CF1 tag if using per-slice tags.

---

## CF2 — Uniform lifecycle  (AUTONOMOUS — build fully)

### T6 — Lifecycle parity across every custom element
- [ ] Segment VALUE edit (`PUT`) — currently missing · Audit:missing (confirmed) · Build:___ · Test:___
- [ ] Segment DEF delete (guarded by live assignments/values) — currently missing · Audit:missing (confirmed) · Build:___ · Test:___
- [ ] Audit ALL elements (field def, list, list value, segment def, segment value, entry form) for the full create/view/edit/inactivate/delete verb set; fill each gap found · Audit:matrix in FINDINGS: list self-verbs ❌, seg value edit/inact ❌, seg def delete ❌, entry-form inactivate ❌ · Build:___ · Test:___
- [ ] Every delete/inactivate has a dependency guard (never silently orphans) · Audit:existing guards solid; new verbs must match · Build:___ · Test:___

**CF2 SLICE GATE** — [ ] re-read PLAN §6 lifecycle matrix; every element now has uniform verbs or a logged reason; boxes ticked or in BLOCKERS.

---

## CF3 — Dashboard flexibility  (AUTONOMOUS — build fully)

### T7 — Drag-drop rearrange
- [ ] DnD wired to existing `Row/Col/Width` on PortletInstance; persists via `PUT /dashboards/mine` · Audit:missing (0 draggable even in arrange) · Build:___ · Test:___
- [ ] Browser: drag a portlet, reload, order persisted · Audit:no test · Build:___ · Test:___

### T8 — Remove portlet
- [ ] Per-portlet remove control on personalized dashboard; persists · Audit:EXISTS (D4, behind Personalize→Arrange; 9 buttons) — PLAN correction · Build:___ · Test:___
- [ ] Browser: remove a portlet, reload, gone · Audit:no test · Build:___ · Test:___

### T9 — Add-portlet bucket
- [ ] "Personalize" dropdown lists ALL portlet types (KpiMeter, KpiScorecard, Reminders, SavedViewList, Shortcuts, RecentRecords, Chart) — not just Add-KPI/Add-reminder · Audit:only KPI+reminder (confirmed) · Build:___ · Test:___
- [ ] Browser: add an instance of each type · Audit:no test · Build:___ · Test:___

### T10 — Tile / shortcut authoring
- [ ] Shortcuts portlet: add-new tile · Audit:missing (0 controls) · Build:___ · Test:___
- [ ] Tile: choose colour · Audit:missing (no colour in config shape) · Build:___ · Test:___
- [ ] Tile: choose target page (Route exists in ShortcutItem; add authoring UI) · Audit:Route in model; no UI · Build:___ · Test:___
- [ ] Browser: add tile, set colour + target, click it, land on target page · Audit:no test · Build:___ · Test:___

### T11 — Populate the pickers (the "reminders don't work" root cause)
- [ ] Seed several example saved views per role so reminder/KPI view-pickers aren't near-empty · Audit:Requisition picker 0 views; Rfq 1, PO 2, Invoice 1 (probe) · Build:___ · Test:___
- [ ] Make create-view → bind-to-reminder/KPI loop discoverable (e.g. "create a view" link from an empty picker) · Audit:not discoverable (confirmed) · Build:___ · Test:___
- [ ] Browser: add-reminder picker shows multiple views; create a view and bind it to a KPI end-to-end · Audit:no test · Build:___ · Test:___

**CF3 SLICE GATE** — [ ] re-read PLAN §11 dashboard table; drag/remove/add-bucket/tile-authoring/picker-populate all ticked or in BLOCKERS.

---

## CF4 — Custom-field authoring parity  (AUTONOMOUS — build fully)

### T12 — Field authoring surface
- [ ] Display type (Normal / Disabled / Inline) on the field def + modal · Audit:missing on def (form placement only) · Build:___ · Test:___
- [ ] Insert-before named-field picker (over the existing integer sort) · Audit:missing (integer sort only) · Build:___ · Test:___
- [ ] Show-in-list flag — field surfaces as a column in list/saved-views · Audit:missing; registry rows make it feasible · Build:___ · Test:___
- [ ] Explicitly NOT built: global-search, encrypted (dropped — record in report) · Audit:absent; DROPPED per ruling · Build:___ · Test:___
- [ ] (Optional if cheap) multiselect + datetime types · Audit:absent; assess at CF4 · Build:___ · Test:___
- [ ] Browser: create field display=Inline, insert-before an existing field, show-in-list on; renders inline, in position, as a list column · Audit:no test · Build:___ · Test:___

**CF4 SLICE GATE** — [ ] re-read PLAN §1 custom-fields table; display-type/insert-before/show-in-list ticked; dropped items noted; boxes ticked or in BLOCKERS.

---

## AUTONOMOUS BAND COMPLETE CHECK
- [ ] EVERY CF1–CF4 box above is `[x]` OR has a `BLOCKERS.md` entry with a reason. Zero silent gaps.
- [ ] Full browser suite green (all e2e specs incl. every new CF test). Pass count: ___
- [ ] Full API + web suites green. API ___ · web ___
- [ ] Hold-or-raise vs starting baselines confirmed.
- [ ] `e2e-audit/results.json` regenerated. Tag `v1.0-cf-autonomous-band`.

---

## CF5 / CF6 / CF7 — NEW ARCHITECTURE  (GATED — Step 0 PLAN ONLY, DO NOT BUILD)

Produce a written Step 0 file plan for EACH, saved to file, for operator review on waking.
**Do not write implementation code for these.**

### CF5 — Entry-form LAYOUT EDITOR (Step 0 only)
- [ ] `docs/CF/STEP0-CF5-layout-editor.md` written: subtabs-as-objects, field groups, drag reorder, column break; model additions + every migration named; task breakdown · Done:___

### CF6 — Custom LINE fields + sublists (Step 0 only)
- [ ] `docs/CF/STEP0-CF6-line-fields.md` written: LineId model decision (justified), line placement, sublists-as-objects, sublist-in-subtab, line-field show/hide/reorder; migrations named; coupled — not split · Done:___

### CF7 — Saved View → Saved Search (Step 0 only)
- [ ] `docs/CF/STEP0-CF7-saved-search.md` written: richer criteria (operators + AND/OR), segments-as-searchable-class, grouping parity; User/Item DEFERRED, customizations-class DROPPED (both noted) · Done:___

---

## MORNING HANDOFF
- [ ] `docs/CF/CF-VERIFICATION.md` written — Table 1 (vs PLAN, every §1–§12 row) + Table 2 (vs `Build_Comments.md`, paragraph by paragraph). Every function ✅-built-and-browser-tested OR deferred/dropped-with-reason.
- [ ] `docs/CF/PROGRESS.md` — timestamped log of the night's work (one line per task, so the operator sees the timeline at a glance).
- [ ] `docs/CF/BLOCKERS.md` — anything that couldn't pass, with why (empty is ideal).
- [ ] Final report posted: built count / browser-tests-added / deferred-with-reason; five-lens review; full-suite pass counts; the three CF5–CF7 Step 0 plans ready for review.

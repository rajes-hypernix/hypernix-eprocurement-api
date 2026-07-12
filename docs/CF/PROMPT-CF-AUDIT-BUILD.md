# PROMPT — CF-AUDIT-BUILD: check what's built, build what's missing, verify line-by-line

You are Claude Code on eProcure (.NET 10 / EF Core 10 / React+TS / PostgreSQL). This prompt
runs the **entire Customization Framework programme** as the operator asked: for every function
in the two source documents, **check whether it's built, build it if it's not, then test
everything and compare line-by-line against both documents before handing back for review.**

Read first, in order:
1. `docs/CF/README-FIRST-CF.md` — session contract, rulings, boundaries.
2. `docs/CF/CUSTOMIZATION-FRAMEWORK-PLAN.md` — the parity analysis + build plan (the verdicts
   and the CF1–CF7 slice breakdown). THIS IS YOUR SPEC.
3. `docs/CF/Build_Comments.md` — the operator's original feedback, verbatim.
4. `CLAUDE.md`, `docs/AUTHORIZATION-MATRIX.md`, `docs/design-framework/CHARTER.md` — conventions.

This is a large programme. **Do NOT try to do it in one Step 0.** Work slice by slice (CF1 →
CF7), each with its own Step 0 file plan and its own confirmation gate. After each slice, post
a completion note (files, tests, browser-test evidence) before starting the next. The operator
may run several sessions; treat each slice as independently resumable.

---

## PHASE A — AUDIT (do this first, before any building)

Produce a **verification table** — for every function row in
`CUSTOMIZATION-FRAMEWORK-PLAN.md` §1–§12, confirm the plan's verdict against the CURRENT code
by driving the browser and hitting the API as the correct persona (Admin for config screens,
Buyer/Vendor for consumption). The plan was written from a verified snapshot, but code may have
moved — reconfirm. Output a table: `Function | Plan verdict | Your re-verified verdict | note`.
Where you find the plan wrong, say so and adjust the build scope. **WAIT for operator
confirmation of the audit table before building.**

This phase exists so we never build something that already works or skip something that's
actually broken — the exact failure mode the operator called out.

---

## PHASE B — BUILD, slice by slice

For each slice below: Step 0 file plan → confirm → build task-by-task → gates green → atomic
commits → browser tests → completion note. The plan document has the full detail per function;
this is the task spine.

### CF1 — Quick parity wins (5 tasks)
- **T1 Money display:** `Intl.NumberFormat` grouped-decimal display for Money (and Decimal
  where flagged) in `renderField` and every read surface (`100,000.00`); parse back to raw on
  edit. Storage is already `numeric(18,2)` — display only. Browser test: a Money field renders
  grouped on a PO/PR.
- **T2 Custom-list edit:** add `PUT /custom-lists/{code}` (rename + description), an
  **order-mode** field (Entered | Alphabetical — one enum column, migration
  `CustomListOrderMode`, applied in the option resolver), and **inactivate/delete-list**
  (guarded: block/deactivate if referenced, mirroring the value guard). Client + AdminCustomLists
  UI: edit-list modal, order-mode control, delete. Browser test: rename a list, switch order
  mode, see options reorder.
- **T3 Icon uniqueness:** give each Administration element a distinct glyph in `Icon.tsx` +
  `centerTabs.ts` (today `clip`×5, `edit`×3, `doc`×3, `box`×3). Add glyphs for the customization
  objects CF5/CF6 will introduce. Browser test: nav renders distinct icons (assert no two
  Administration items share an icon name).
- **T4 Sidebar collapse:** collapse toggle + persisted width state + slim icon-only rail;
  main content widens. Browser test: toggle collapses/expands, content reflows.
- **T5 Global-search routing:** add detail routes for **Requisition, ASN, Statement** and point
  `hitRoute` (`TopBarNav.tsx`) at them; every record type deep-links to its own detail.
  Browser test: search a PR code → click → land on that PR (not the list); same for ASN,
  Statement; regression-check PO/Invoice/RFQ/Vendor still work.

### CF2 — Uniform lifecycle (1–2 tasks)
- **T6 Lifecycle parity:** audit every custom element for the full create/view/edit/inactivate/
  delete verb set; fill the gaps — **segment value edit** (`PUT`), **segment def delete**
  (guarded by live assignments/values), and any other element missing a verb. Every
  delete/inactivate has a dependency guard (never silently orphan). Browser tests: edit a
  segment value; attempt to delete a referenced segment (blocked/deactivated), delete an unused
  one (removed).

### CF3 — Dashboard flexibility (5 tasks)
- **T7 Drag-drop rearrange:** wire a DnD interaction to the existing `Row/Col/Width` on
  `PortletInstance`; persist via `PUT /dashboards/mine`. Browser test: drag a portlet, reload,
  order persisted.
- **T8 Remove portlet:** per-portlet remove control on the personalized dashboard; persist.
  Browser test: remove a portlet, reload, gone.
- **T9 Add-portlet bucket:** a "Personalize" dropdown listing ALL portlet types (KpiMeter,
  KpiScorecard, Reminders, SavedViewList, Shortcuts, RecentRecords, Chart) — not just the
  current Add-KPI/Add-reminder buttons — add an instance of the chosen type. Browser test: add
  each type.
- **T10 Tile/shortcut authoring:** the Shortcuts portlet gets add-new, **colour choice**, and
  **target-page** selection (Route already exists in `ShortcutItem`). Browser test: add a tile,
  set colour + target, click it, land on the target page.
- **T11 Populate the pickers:** seed several example saved views per role (so the reminder/KPI
  view-pickers aren't near-empty — the root of "reminders don't work"), and make the
  create-view → bind-to-reminder/KPI loop discoverable in the UI (e.g. a "create a view" link
  from the empty picker). Browser test: the add-reminder picker shows multiple views; create a
  view and bind it to a KPI end-to-end.

### CF4 — Custom-field authoring parity (1 task)
- **T12 Field authoring:** add **display type** (Normal/Disabled/Inline) to the field def +
  modal, an **insert-before** named-field picker (over the existing sort), and a **show-in-list**
  flag that surfaces the field as a column in list/saved-views. Explicitly DO NOT add
  global-search or encrypted (dropped). Optional if cheap: multiselect + datetime types.
  Browser test: create a field with display=Inline, insert-before an existing field, show-in-list
  on; verify it renders inline, in the right position, and as a list column.

### CF5 — Entry-form LAYOUT EDITOR (own Step 0 — new architecture)
- Turn the form *registry* into a **layout designer**. **Subtabs become managed objects**
  (create/rename/reorder/show-hide), **field groups** are creatable (body + subtab), fields get
  **drag reorder** and **column break** / same-as-previous. This replaces the current
  list-with-buttons with an actual editor. Model additions expected (subtab entity, field-group
  entity or structured placement); name every migration. Browser tests: create a subtab, create
  a field group, drag fields between groups/subtabs, set a column break, save, reopen, layout
  preserved; the resolved form for a role reflects the new layout.
- **STOP and present the Step 0 for this slice separately** — it's the biggest single build and
  the operator wants to see the plan before you write it.

### CF6 — Custom LINE fields + sublists (own Step 0 — new architecture, coupled)
- **Line-scoped custom fields:** add `LineId` to the custom-field value model (new sparse
  line-value path or `LineId` nullable on the existing table — decide and justify in Step 0),
  line-level placement, and the sublist render path. **Sublists become objects** selectable into
  a subtab; line fields get show/hide/reorder. Migration(s) named. Browser tests: define a line
  field, place it on a PO line sublist, enter a value per line, see it persist and render; it
  flows into saved-view/KPI where applicable.
- **Do NOT split line-fields from sublists** — a sublist is line fields on a subtab; they are
  one feature. **STOP and present this Step 0 separately.**

### CF7 — Saved View → Saved Search (own Step 0)
- **Richer criteria** (operators: equals/contains/gt/lt/between/in; AND/OR grouping),
  **segments as a searchable record class** ("all Projects where…"), and summary/grouping
  parity with NetSuite saved search. **Entity classes User/Item are DEFERRED** — build the
  generalization so they slot in later, but don't build them now. **"Customizations as a search
  class" is DROPPED.** Browser tests: build a multi-criteria view with AND/OR; search over a
  segment class; group results; bind the result to a KPI.
- **STOP and present this Step 0 separately.**

---

## PHASE C — LINE-BY-LINE VERIFICATION (the acceptance gate)

After all slices commit, produce **`docs/CF/CF-VERIFICATION.md`** — the acceptance artifact.
It has two comparison tables:

**Table 1 — against `CUSTOMIZATION-FRAMEWORK-PLAN.md`:** every function row from §1–§12, with:
`Function | Plan verdict (before) | Status now | Browser test name | Commit`. Every row must be
✅ built + browser-tested, OR explicitly on the deferred/dropped list (User/Item entities,
customizations-search-class, global-search/encrypted) with the reason. No silent gaps.

**Table 2 — against `Build_Comments.md` (the operator's own words):** walk the operator's
document paragraph by paragraph and map each request to where it's satisfied (task + browser
test) or deferred/dropped (reason). This is the "compare line by line with my document" the
operator explicitly asked for — his document is the final authority, not just the plan.

Then run the **full browser suite** (all e2e specs including every new CF test) and the full
API + web suites. Paste the pass counts. Confirm hold-or-raise vs the starting baselines.
Regenerate `e2e-audit/results.json`. Tag `v1.0-customization-framework`.

**Final report:** the two verification tables' summary (X functions built, Y browser tests
added, Z deferred-with-reason), the five-lens review of the programme, the full-suite pass
counts, and an explicit statement: *every function in both source documents is accounted for —
built-and-tested or deferred-with-reason — nothing silently dropped.* Only then hand back for
the operator's own review and testing.

---

## Start now
Begin with **PHASE A — the audit table**. Do not build anything until the operator confirms the
audit. Then CF1 Step 0. Slice by slice, browser test everything, and remember: on this
programme a function is not "done" until it's proven in the browser and checked against the
operator's own document.

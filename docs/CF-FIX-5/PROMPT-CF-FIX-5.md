# BUILD PROMPT — CF-FIX-5: Entry Forms + Segments corrections (post-v1.9 testing)

You are Claude Code on eProcure. Operator tested v1.9 (CF-FIX-4) and found nine issues — two are
functional BUGS (verified root causes below), the rest are UX/redesign + two prefix corrections
that the v1.9 run never received (the T3-FIX2 block didn't reach the prompt file on disk). Read
this whole prompt, **START WITH STEP 0** (file plan per task), and **WAIT for confirmation** before
code. Screenshots in `docs/CF-FIX-5/reference-screenshots/`.

Read first: `CLAUDE.md`, `CHARTER.md`, `EntryFormService.cs` (esp. `ResolveAsync`),
`AdminEntryForms.tsx`, `PrForm.tsx` and the PO/GRN/Invoice create screens, `SegmentApplication`
domain + `AdminSegments.tsx`, `SearchSelectField.tsx`.

**Baselines (hold-or-raise):** dotnet 541 · vitest 251 · e2e 98. Atomic commits `CF-FIX5-Tn:`, all
gates green before each. Browser proofs in `15-cf-fix5.spec.ts`. Hard boundaries unchanged. Named
migrations only.

---

## T1 — BUG: chosen form on a transaction doesn't render its custom fields — `CF-FIX5-T1:`
**Verified root cause:** `EntryFormService.ResolveAsync(string recordType, ...)` takes ONLY the
record type — not a form code. So when the user picks a specific form on the transaction (T5's
picker), the resolver has no way to receive that choice; it resolves by record-type + role and
returns the ROLE's default/standard form, ignoring the pick. Result (operator's screenshots 2-3):
the new field-GROUP shows (the screen renders the picker) but the chosen form's CUSTOM FIELDS don't
— because the standard form resolves instead of the chosen one.
**Fix:** `ResolveAsync` accepts an optional `formCode`. When provided, resolve THAT form's layout
(its groups + fields, native AND custom). When absent, fall back to today's role resolution.
**Keep OD-D7-2:** the chosen form is layout-only — at SUBMIT the server still enforces the role
form's required fields (a chosen form can't dodge requireds). Wire the create screens (PR/PO/GRN/
Invoice) to pass the picked `formCode` to the resolver.
Browser test: create a form for Requisition with a custom field; on New PR pick that form; the
custom field RENDERS on the transaction (not just the group); submit still enforces role requireds.

## T2 — Form picker is the FIRST field on the transaction — `CF-FIX5-T2:`
**Operator (screenshot 1):** the "ENTRY FORM" selector currently sits BELOW the header field groups;
it should be the **first thing** the user selects (choosing the form determines the whole layout).
Move the form picker to the TOP of the create screen — above the Header — for PR/PO/GRN/Invoice.
Use the standardized `SearchSelectField`. Selecting a form re-renders the layout beneath it.
Browser test: on New PR the form picker is the first control; changing it re-renders the header.

## T3 — BUG/decision: drag-drop still drops at the END; DECIDE — fix properly or remove — `CF-FIX5-T3:`
Drag still ignores drop position (lands last in the group) despite T3-FIX2's intent — that block
never reached the v1.9 run. **Operator's instruction: fix it properly OR remove drag entirely and
keep ONLY the up/down arrows.**
- **Preferred: FIX.** Root cause is the drop handler computing target group but not target index.
  Make the drop compute the insertion index from release position and renumber `Sort`, so the field
  lands where dropped (the arrows already do correct index math — match it).
- **If a correct fix is not cleanly achievable in reasonable effort: REMOVE drag** (remove the
  drag handlers/affordances) and keep the up/down arrows as the sole reorder mechanism. Arrows work
  and are the operator-approved fallback. Do NOT ship half-working drag.
- Decide in Step 0 which path; if FIX, prove it with a test asserting drop-between-rows lands at
  that position; if REMOVE, prove arrows fully cover reorder within and across groups.

## T4 — Item sublist layout: NetSuite-style top-to-bottom = left-to-right; add custom line fields — `CF-FIX5-T4:`
**Operator (screenshots 4 vs 5):** the current sublist arrangement (horizontal chips with ←/→)
"looks horrible." Redesign to match NetSuite's Sublist Fields (screenshot 5): a **vertical top-to-
bottom list** where **top = leftmost column** on the actual line grid, reorderable up/down (and
show/hide via a Show checkbox like NS). AND: the admin must be able to **add custom LINE (custcol_)
fields to the sublist here** — line-scope custom fields append to the sublist and are reorderable
among the columns. (Line columns stay FLAT — no field groups, per prior ruling.)
Browser test: sublist columns show as a vertical list, top row = leftmost on the line grid; reorder
moves the column; add a custcol_ line field and it appears as a sublist column.

## T5 — Remove ALL instructional/help text from these screens — `CF-FIX5-T5:`
**Operator:** too much explanatory/help prose scattered across the entry-form and segment screens
(the grey "Resolution follows a fixed global role precedence…", "Line columns are FLAT…", "Removing
an application is refused…", "Layout only — required fields…", "The ID is assigned automatically…",
etc.). **Remove all of it.** The UI should be clean — controls without paragraphs of explanation.
Keep only essential inline labels (field names, button text). No helper sentences, no rulings-as-
subtext. Applies to Entry Forms, the transaction form picker, and Segments screens.
Browser test / assertion: the named help-text strings are gone from the rendered screens.

## T6 — Redesign the form builder: list → open in a full page (not list+builder cramped together) — `CF-FIX5-T6:`
**Operator (screenshot 7, NetSuite screenshot 5):** the current Entry Forms screen crams the form
LIST and the BUILDER on one page — cramped. Redesign NetSuite-style:
- The Entry Forms screen shows the **list of forms** with, per row (on the right), **Open / Edit /
  Copy** actions.
- Any action opens the builder in a **dedicated full page** where the list is NOT shown — just the
  builder, with a **Back** control to return to the list.
- **Unsaved-changes guard:** clicking Back (or leaving) with unsaved changes shows a warning ("all
  changes will be lost"); the user confirms or cancels. Only leave/discard on confirm.
Browser test: the list shows Open/Edit/Copy per form; opening one hides the list and shows only the
builder + Back; Back with unsaved changes warns.

## T7 — Segments: apply to BOTH header AND line (not one-or-the-other) — `CF-FIX5-T7:`
**Verified (screenshot 6):** `SegmentApplication` has one `LineLevel` bool per (SegmentDef,
RecordType) row, and the unique constraint forbids applying the same segment to both header and
line of the same record type — so today it's header XOR line, and only PO exposed "Apply per-line."
**Fix:** a segment can apply to **both** the header AND the line of the same record type. Allow two
applications per (segment, record type) — one header, one line — OR restructure to (RecordType,
Level) with Level ∈ {Header, Line}. Every record type that has lines should offer both Apply
(header) and Apply per-line; header-only types offer just header. Migration
`SegmentApplicationHeaderAndLine` (adjust the unique constraint from (Def,RecordType) to
(Def,RecordType,Level) or equivalent — confirm shape in Step 0; guard against dropping existing
assignments).
- **Same dimension id for both levels** (see T8) — applying header and line uses the SAME
  `custseg_` id, exactly like NetSuite treats one dimension applied at two levels.
Browser test: apply a segment to Requisition header AND (a line-bearing type's) line; both stick;
the same segment id serves both.

## T8 — Prefix corrections the v1.9 run missed: `customform_` and `custseg_` — `CF-FIX5-T8:`
The v1.9 run never received the prefix instructions (the T3-FIX2 block didn't reach the file).
Apply now, consistent with `custbody_`/`custcol_`/`custlist_`:
- **Entry forms:** user-input internal id with fixed **`customform_`** prefix (affix inside the
  field, like custom fields). Required, unique, legal chars, immutable. Existing `ef_*` codes
  grandfathered (don't rename). New forms get `customform_`.
- **Segments:** user-input internal id with fixed **`custseg_`** prefix. **The same `custseg_` id
  applies whether the segment is on header or line** — it's one dimension (T7), never two ids.
  Existing `seg_*` codes grandfathered.
- Confirm in Step 0 whether these need migrations or are create-path + display only (likely the
  latter — derive-at-create + display, grandfather existing).
Browser test: new form id `project` → `customform_project`; new segment id `region` →
`custseg_region`, same id shown on both its header and line applications.

---

## SEQUENCING
Bugs first, then redesign: **T1 (chosen-form bug) → T3 (drag decision) → T7 (segment both levels)
→ T8 (prefixes) → T2 (picker to top) → T4 (sublist layout) → T6 (builder full-page) → T5 (strip
help text last, so it doesn't fight the other UI changes).**

## PER-TASK & FINAL
Each: Step-0 confirmed → build → gates green → atomic commit → browser test → note. Final report:
five-lens review; the T3 drag decision (fixed or removed, with why); the T1 and T7 root-cause
fixes; a line-by-line map of the operator's 9 findings → commits → proofs. Tag `v2.0-cf-fix-5`.

**Process note for this run:** after unzipping, CONFIRM this prompt file contains all of T1–T8
before starting (the last round's prompt file was stale on disk and a whole block was missing). If
any task below is absent from the file you're reading, STOP and tell the operator the file is
incomplete rather than proceeding.

Do not begin coding until I confirm your Step 0.

---

## APPENDIX — operator findings verbatim
1. Drag-drop doesn't work — field becomes last in the group regardless of drop position. If it
   can't be made to work, remove drag and keep the arrows.
2. On the transaction (e.g. Requisition) the form selection should be the FIRST field to select —
   it's currently below the field groups.
3. Custom fields added to a new form don't display at transaction level even when that form is
   chosen — only the original fields show, though the new field GROUP shows.
4. Item sublist arrangement looks horrible — should be a top-to-bottom list where top = most-left
   column, like NetSuite; also need the option to add custom line fields here.
5. Too much instructional/help text on all these screens — remove all of it.
6. Don't like the form builder — list + builder cramped on one page. Follow NetSuite: list of
   forms, with Open/Edit/Copy per row; action opens a full page (no list), with Back; warn on Back
   with unsaved changes.
7. Segments should apply to BOTH body and line, not only one — currently only PO can apply line,
   the rest only one level.
8. Entry-form internal id convention isn't followed — should be `custform_`/`customform_`.
9. Custom segment id should be `custseg_`, and the SAME id for both header and line (it's a
   dimension), like NetSuite.

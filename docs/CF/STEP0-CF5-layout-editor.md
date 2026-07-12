# STEP 0 — CF5: Entry-form LAYOUT EDITOR (plan only — no code built)

**Closes:** PLAN §4 (body-field portion) + §5 (subtab objects). Sublists and line
fields are explicitly NOT here — they are CF6 (coupled slice, per the PLAN's own
"splitting will thrash" warning). The boundary: CF5 ships everything a layout
designer does to BODY fields; CF6 adds the line dimension onto CF5's containers.

**Operator's words this answers (Build_Comments):** the "Customize Form" experience —
create field groups, create/show/hide subtabs as real objects, drag-reorder fields,
column break; today the screen is a form registry, not a designer.

## Current model (what exists, verified in code)

- `EntryFormDef` (Code/Name/RecordType/IsSystem/Active) + `EntryFormField`
  (FieldKey→registry, Subtab **as a string label**, FieldGroup **as a string**,
  Sort, DisplayType, RequiredOnForm, DefaultValue, SourceFieldKey, FullWidth,
  Label, Placeholder) + `EntryFormRoleMap`. Standard form seeded, read-only,
  parity baseline.
- The D7 renderer already honours Subtab/FieldGroup/Sort/FullWidth — CF5 turns
  those strings into MANAGED OBJECTS and gives them an editor.

## Model additions (all additive — the Standard form seed keeps rendering unchanged)

New entities, one migration: **`EntryFormLayout`**

1. `EntryFormSubtab` — Id, FormDefId(FK), Name, Sort, Hidden(bool).
   Subtabs become per-form objects: creatable empty, reorderable, hideable.
   `EntryFormField.Subtab` (string) is REPLACED by `SubtabId (Guid?)` —
   null = main body. Migration backfills: distinct existing Subtab strings per
   form → subtab rows; fields re-pointed by name match.
2. `EntryFormGroup` — Id, FormDefId(FK), SubtabId(Guid? — null = body group),
   Title, Sort, ColumnBreak(bool — this group starts a new column).
   `EntryFormField.FieldGroup` (string) → `GroupId (Guid)`; same backfill
   pattern (distinct FieldGroup strings → group rows).
3. `EntryFormField` gains `ColumnBreak (bool)` — field-level "start second
   column" inside its group (NetSuite's column break; "same as previous" is the
   ABSENCE of a break, no extra flag needed).

**Migration named:** `EntryFormLayout` (the ONE CF5 migration — creates two
tables, adds SubtabId/GroupId/ColumnBreak, backfills from the string columns,
then drops `Subtab`/`FieldGroup` strings). Backfill is deterministic and
idempotent; the Standard form's seed is regenerated to author objects directly.

## API surface (rides the existing EntryFormsController)

- Subtabs: `POST/PUT/DELETE /entry-forms/{id}/subtabs[/{subtabId}]` (+ Hidden
  toggle in PUT). Delete guard: subtab with placed fields → 409 (move or hide
  instead) — same never-silently-drop discipline as lists/segments.
- Groups: `POST/PUT/DELETE /entry-forms/{id}/groups[/{groupId}]`, same guard.
- Field placement stays inside the existing `PUT /entry-forms/{id}` payload
  (fields now carry SubtabId/GroupId/ColumnBreak/Sort).
- Validation (server, loud): SubtabId/GroupId must belong to the form; a
  hidden subtab's REQUIRED fields keep gating submit (hidden ≠ not required —
  the D7 rule "hidden means hidden, not forbidden" extends here; the editor
  warns when hiding a subtab containing required fields).

## UI (AdminEntryForms becomes the designer)

- Left: form structure tree — Body → groups; subtabs → groups; counts per node.
- Canvas: the CURRENT D7 preview rendering, but in EDIT mode every field chip is
  draggable (the CF3-T7 HTML5 DnD pattern, already proven) between groups and
  subtabs; group/subtab headers get rename/reorder/hide/delete controls; "Add
  group", "Add subtab", "Column break" toggles on group + field.
- Keep the existing table editor as the fallback ("Details" toggle) — the
  designer is additive, not a rewrite of a working surface.

## Task breakdown (each with a browser gate, CFn-Tm discipline)

- CF5-T1: model + `EntryFormLayout` migration + backfill + seed regeneration.
  Gate: Standard form renders BYTE-IDENTICAL before/after (existing parity test
  re-run + snapshot diff).
- CF5-T2: subtab objects CRUD + hide/show. Browser: create subtab, place field,
  hide it, PR form shows/hides accordingly.
- CF5-T3: field groups CRUD + column break. Browser: create group, break
  columns, layout renders two columns.
- CF5-T4: drag reorder (fields between groups/subtabs). Browser: drag a field
  to another subtab, reload, placement persists, PR form follows.
- CF5-T5: guards + required-on-hidden-subtab warning. Browser: delete-blocked
  subtab, warning on hide.

## Risks / operator decisions wanted

1. Replacing Subtab/FieldGroup strings with FKs touches the D7 renderer and the
   Standard-form seed — the parity snapshot gate (T1) is the safety net. OK?
2. Hidden subtab + required field: warn-but-allow (proposed) vs block. Proposed:
   warn, because roles differ — a field required for Buyer may live on a subtab
   hidden for Approver.
3. Two-column canvas only (matches current CSS grid g2). NetSuite allows more;
   parity doesn't need it.

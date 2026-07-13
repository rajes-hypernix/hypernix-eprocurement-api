# BUILD PROMPT — CF-FIX-4: Entry Forms redesign + Segments parity

You are Claude Code on eProcure. This is a large, coordinated slice: redesign Entry Forms into a
real layout editor with field-groups-as-objects and a shared placement model, seed standard forms
for all transaction types, drive placement from field creation with a mandatory cascade, add a
form-picker on transactions — then apply the SAME model to Segments. It's big; **do Step 0 in full,
present it, and WAIT for confirmation.** Several decisions are already LOCKED (below) — do not
re-litigate them.

Read first: `CLAUDE.md`, `CHARTER.md`, `EntryForms.cs` (domain), `EntryFormSeed.cs`,
`EntryFormService.cs`, `AdminEntryForms.tsx`, `CustomFields.cs`, `CustomFieldService.cs`,
`AdminCustomFields.tsx`, `Segments` domain + `AdminSegments.tsx`, `SearchSelectField.tsx`,
`ImpactReportDialog.tsx` (CF-FIX-3), the reference-registry providers (CF-FIX-3).

**Baselines (hold-or-raise):** dotnet 516 · vitest 243 · e2e 89. Atomic commits `CF-FIX4-Tn:`, all
gates green before each. Browser proofs in new `e2e-audit/tests/14-cf-fix4.spec.ts`. Hard boundaries
unchanged. Migrations are expected here (named per task) — an UNnamed one → STOP and report.

---

## LOCKED DECISIONS (confirmed by operator — build these, don't reconsider)

- **L1 — ONE shared placement object.** A field's/segment's placement on a form (which group, what
  order) is a SINGLE record. BOTH the creation-cascade AND the Entry Forms drag-editor read/write
  the same placement. Create-time sets it; the layout editor moves it; same object, no drift.
- **L2 — FLAT field groups** (NetSuite-style, not nested). No subgroups. (A guaranteed top-level
  Header group is clean flat; nesting is deferred until a real need appears.)
- **L3 — The Header invariant (SECURITY/INTEGRITY, enforce server-side):** every form ALWAYS has a
  "Header" field group. It **cannot be deleted, ever.** On **standard/system** forms it cannot even
  be renamed (parity baseline). On **non-standard** forms it can be renamed but never removed. This
  guarantees that at field-creation time there is ALWAYS at least one valid group to place into — the
  mandatory cascade can never reach an impossible state. Enforce as an invariant (a form cannot
  persist without its Header group; delete-group refuses Header with 409; rename refused on system
  forms) — NOT a UI-only default.
- **L4 — Item sublist only, no expense sublist.** Users who want an expense line name an item after
  the expense code. Standardize on the single item sublist.
- **L5 — RFQ excluded** from standard forms (it's a step-by-step wizard, handled differently). Standard
  forms are seeded for **PR, PO, GRN, Invoice** only.
- **L6 — REMOVE-FROM-FORM IS DATA-SAFE (invariant, enforce + test).** Removing/unticking a field (or
  segment) from a form's layout deletes ONLY its placement row (the L1 placement object) — it NEVER
  touches the field's stored VALUES. The value stays on every existing record, stays queryable, stays
  visible in saved views, and still renders on any OTHER form that includes the field. Layout is not
  data. A naive "on remove, clean up" that cascades into value deletion is a SEVERE bug — guard against
  it explicitly and prove with a test (below, T3). This matches NetSuite: removing a field from a form
  is a layout op with zero data impact. (To actually hide/remove a field's values, the user uses the
  field LIFECYCLE — Inactivate / Archive / Purge — not the form editor. See T8.)

---

## PART A — ENTRY FORMS

### T1 — Field groups + subtabs as OBJECTS; the Header invariant — `CF-FIX4-T1:`
Today `EntryFormField.FieldGroup` and `.Subtab` are STRINGS (the screenshot's loose "Header" text +
stray checkbox). Make them managed objects, per-form.
- New entities: `EntryFormGroup (Id, FormDefId, Name, Sort, IsHeader bool, SubtabId Guid?)` and
  `EntryFormSubtab (Id, FormDefId, Name, Sort, Hidden bool)`. A group lives on the body (SubtabId
  null) or inside a subtab. `EntryFormField` gains `GroupId (Guid)` and `SubtabId (Guid?)`, replacing
  the strings. Migration `EntryFormLayoutObjects`: create the two tables; backfill existing string
  groups/subtabs into rows (distinct values per form → group/subtab rows; every form gets an
  `IsHeader=true` "Header" group; fields re-pointed by name match); then drop the string columns.
  Standard PR form must resolve **byte-identical** before/after (parity STOP gate — if not identical,
  STOP and report).
- **Enforce L3 (Header invariant)** in the domain + service: form creation always makes a Header
  group; delete-group 409s on `IsHeader`; rename 409s on Header when the form `IsSystem`.
- Groups/subtabs CRUD endpoints (create/rename/reorder/delete-with-guard), admin-scoped.

### T2 — Standard forms for PR (exists), PO, GRN, Invoice — `CF-FIX4-T2:`
- Seed a standard/system `EntryFormDef` for **PurchaseOrder, Grn, Invoice** mirroring the existing
  `EntryFormSeed` PR pattern: each with a Header group (L3), populated from that record type's native
  field registry (the `FieldKind.Native` fields for that type). RFQ excluded (L5).
- These are `IsSystem=true` parity baselines — Header fixed, read-only structure, the template every
  new form for that type copies from ("New form (copy of Standard X Form)").
- Migration `StandardFormsSeed` (seed data; confirm whether it needs a migration or is pure seed — if
  pure seed, no migration).
Browser test: Entry Forms shows a Standard form for PR, PO, GRN, Invoice; each has a Header group.

### T3 — Redesign the Entry Forms layout editor (drag-drop, groups, subtabs, sublist) — `CF-FIX4-T3:`
Replace the current table-with-buttons (screenshots 1-2) with a real designer:
- **Body fields** grouped under field-groups; **drag-drop reorder**, and **dragging a field into a
  different group updates its GroupId** (L1 — the placement object is what moves). Drag is limited/
  sensible: a field belongs to exactly one group; dropping it in another re-parents it and the group
  assignment updates (the operator's exact ask).
- **Field-group management on-screen:** create group, rename (Header rename blocked per L3), reorder,
  delete (guarded — can't delete a group with fields without moving them; can't delete Header).
- **Subtabs:** create/rename/hide/reorder; a subtab contains its own field-groups-and-fields; drag a
  field (or group) into a subtab. Hidden subtab hides its fields but required still gates submit.
- **Sublist:** the item sublist (L4) is rearrangeable (column order) but has **NO field groups**
  (sublist columns are flat — the operator was explicit).
- **The "add field" dropdown and any list-style pickers here** use the standardized `SearchSelectField`
  (same as custom fields — the operator's ask; also applies to segment pickers in Part B).
Browser test: create a group, drag a field from Header into it (GroupId updates, persists on reload);
create a subtab, move a field in; reorder sublist columns; Header cannot be deleted.
**Data-safety test (L6 — required):** put a value on a PR via a form, then REMOVE that field from the
form's layout; assert the PR still holds the value (queryable, in saved views, on any other form with
the field) — only the placement row was deleted, zero value rows touched. This test is the guard
against a placement-delete cascading into value-deletion.

### T4 — Field creation drives placement: the mandatory cascade — `CF-FIX4-T4:`
Extend custom-field creation (CF-FIX-2 gave "applies to record types"; now add form + group):
- When creating/editing a custom field: after choosing record type(s), **for each chosen record type
  the user must choose which form(s)** (a record type can have multiple forms) **and, per chosen form,
  which field group.** **Mandatory cascade** (enforce server-side): record chosen → form required;
  form chosen → group required. Because of L3 there is ALWAYS a Header group to choose, so the cascade
  can always complete.
- This WRITES the shared placement object (L1) — the same rows the T3 editor manipulates. Creating a
  field with (Requisition → Standard PR Form → Header) creates the placement; the field then appears
  on that form in that group, and can be rearranged later in T3's editor. **One source of truth.**
- Default when no placement chosen is NOT allowed for a chosen record type (cascade is mandatory) —
  but a field can be created applying to a record type and be placed later ONLY if the UI offers an
  explicit "place later" that still lands it in Header (never an orphaned field). Decide in Step 0:
  strict-mandatory (must place now) vs must-choose-form-but-defaults-to-Header. **Recommend: form is
  mandatory, group defaults to Header but is changeable** — mandatory form, safe default group.
- UI: use the searchable multi-select for the form picker; make multi-form-multi-group legible.
Browser test: create a field on Requisition → must pick a form → group defaults to Header → field
appears on that form in Header → rearrange it into another group in the editor → placement persists.

### T5 — Form picker on the transaction — `CF-FIX4-T5:`
On a transaction (PR, PO, GRN, Invoice) where **multiple forms exist** for that type, the user can
**choose which form** to use. Respect the existing server rule (OD-D7-2: a chosen form can't dodge its
role form's required fields — the server re-resolves and enforces). If only one form exists, no picker
(or a disabled indicator). Browser test: with 2 PR forms, the picker appears and switches layout.

---

## PART B — SEGMENTS (apply everything from fields/lists + the placement model)

### T6 — Segment lists standardized + value-creation redesign + lifecycle parity — `CF-FIX4-T6:`
- All segment value pickers/dropdowns use `SearchSelectField` (standardize, like fields/lists).
- Redesign the segment value-creation screen (screenshot 4) to match the friendly fields/lists UX
  (staged save if it currently auto-saves; searchable pickers; clean value entry).
- Apply the CF-FIX-3 **lifecycle/impact-report/three-tier** discipline to segments (the
  SegmentReferenceProvider that shipped registered-but-empty in CF-FIX-3 now becomes real): a segment
  (and a segment value) can be inactivated/deleted/purged with the same impact report and guards.
  The screenshot already shows "Removing an application is refused while assignments exist" — extend
  that to the full three-tier model.

### T7 — Segment placement mirrors field placement (header→form+group, line→no group) — `CF-FIX4-T7:`
Apply L1's placement model to segments:
- A segment applied to a record **header** lets the user choose **form(s) + field group(s)** (multiple
  forms, multiple groups) — the SAME shared placement object and cascade as fields (T4). The segment
  then appears on those forms in those groups and is rearrangeable in the T3 editor.
- A segment applied to a **line** has **NO field group** (lines are flat, per the operator — same as
  sublist columns in L4). Line application is just apply/remove (as today, screenshot 4) plus the
  standardized UX.
- Redesign the "Applied to" screen (screenshot 4) so header-apply triggers the form+group cascade and
  line-apply stays simple. User-friendly, consistent with the field creation flow.
Browser test: apply a segment to Requisition header → choose form + group → it appears on that form,
rearrangeable; apply to a line → no group asked, appears as a line dimension.

---

## PART C — the ARCHIVE lifecycle tier (the NetSuite "remove field, values survive only in audit")

### T8 — Archive: a reversible tier between Inactivate and Purge — `CF-FIX4-T8:`
The operator wants NetSuite's behavior: retire a field so its values **disappear from all live surfaces
(forms, saved views, queries) but survive in the audit trail, recoverably.** This is DISTINCT from
remove-from-form (L6, pure layout, data untouched) and from Purge (CF-FIX-3, irreversible delete). It's
a new middle tier in the CF-FIX-3 lifecycle:

- **Inactivate** (exists): hidden from NEW entry; values still fully VISIBLE in views/queries/records.
- **Archive** (NEW): the field's values are **hidden from every live surface** — form display, saved
  views, saved-view filters, queries/search, record display — but **preserved verbatim in storage and
  captured in the audit log** (an audit entry recording the archive action and scope). **Reversible:**
  un-archive restores full visibility. Values are NEVER deleted by archive — only hidden.
- **Purge** (exists, CF-FIX-3): values permanently removed from historical records, snapshot in audit,
  irreversible, higher permission.

**Implementation:**
- `CustomFieldDef` gains an `Archived` state (distinct from `Active`/inactivated). Migration
  `CustomFieldArchiveState` (one nullable column / status extension — confirm shape in Step 0).
- **The hiding is enforced at the read/resolve layer, centrally** — the field-value read path, the
  saved-view runner, the search/query surface, and form resolution ALL must exclude archived-field
  values. **Do this via ONE central filter** (an archived-field predicate the value-read path applies),
  NOT scattered checks, so a future new surface inherits the hiding automatically — same anti-rot
  discipline as the CF-FIX-3 registry. If a new query surface is added later and reads values through
  the standard path, it hides archived values with no extra work.
- Archive writes an audit entry (action, field, who, when). Un-archive writes its own audit entry.
- **Permission:** Archive is admin-tier (same as manage-custom-fields is fine — it's reversible, unlike
  Purge which is higher). Confirm in Step 0.
- **Applies to custom LIST values and SEGMENTS too** where the same "hide-everywhere-but-keep" semantics
  make sense — at minimum wire the field case fully; note the list/segment extension.

Browser tests: put a value on a PR; **archive** the field → the value no longer appears on the form, in
a saved view that used it, or in search — but an audit entry records the archive; **un-archive** →
value reappears everywhere. Confirm archive did NOT delete the stored value (it's recoverable). Confirm
this is DIFFERENT from remove-from-form (L6): remove-from-form leaves the value visible via other forms/
queries; archive hides it everywhere.

---

## SEQUENCING
Part A is the foundation (the placement model + groups-as-objects), Part B reuses it, Part C (Archive)
extends the CF-FIX-3 lifecycle. Order:
**T1 (objects + Header invariant) → T2 (standard forms) → T3 (editor, incl. L6 data-safety) →
T4 (creation cascade) → T5 (transaction picker) → T6 (segment lists/lifecycle) → T7 (segment
placement) → T8 (Archive tier).**
T1 is the load-bearing migration — present its Step 0 in most detail and note the parity STOP gate.
T8's central-hiding filter is the anti-rot piece — present how it hooks the value-read path.

## PER-TASK & FINAL
Each task: Step-0 confirmed → build → gates green → atomic commit → browser test → note. **T1 and T4
additionally:** present the placement-model schema and the cascade enforcement for confirmation before
writing their code, since they're the architectural spine. Final report: five-lens review; the
placement model explained (one object, two surfaces); the Header-invariant enforcement; the standard-
forms parity result; a line-by-line map of the operator's asks → commits → proofs. Tag `v1.9-cf-fix-4`.

Do not begin coding until I confirm your Step 0 — and hold T1/T4 code until I separately confirm the
placement schema.

---

## APPENDIX — operator's asks verbatim
- Standardize on item sublist; no expense sublist (name an item after the expense code).
- Standard forms for PR, PO, GRN, Invoice; RFQ handled separately (step-by-step) — ignore for now.
- Create field groups; the current "Header free-text + stray checkbox" is wrong. Design group creation
  like parent-child list values. [Operator later confirmed FLAT groups, not nested.]
- New field default has no group; choosing a record type requires choosing which form(s) and which
  field group; a record can have multiple forms — make selecting this user-friendly; mandatory: record
  → form → group. Then rearrange in Entry Forms later. Current entry-form design isn't user-friendly —
  redesign.
- Subtabs: fields with field-groups inside a subtab.
- Sublist: rearrangeable, but NO field groups.
- Drag-drop up/down to rearrange body fields; a body field is limited to its group, but if the new
  position sits in another group, its group assignment updates.
- The current-fields dropdown on forms: standardize the list style (like custom fields); apply to
  segments too.
- Segments: apply everything done for fields/lists; standardize the lists; redesign value-creation and
  header/line assignment; header → choose form + field group (multiple forms/groups); line → no group;
  appears on entry form to rearrange. Make the input screen user-friendly.
- On the transaction, choose which form based on how many forms exist.
- A standard "Header" field group is ALWAYS present; renamable on non-standard forms; guarantees at
  least one group exists when any field is created (security).
- Remove-from-form must be data-safe: unticking a field from a form keeps its value in the DB tagged to
  the record; the value survives (still visible via other forms/queries) — only the form no longer shows
  it. [Layout ≠ data. This is L6.]
- Separately, the NetSuite "retire the field so values vanish from views/search but remain in the audit
  log, recoverably" behavior = the new ARCHIVE tier (T8), distinct from remove-from-form and from Purge.

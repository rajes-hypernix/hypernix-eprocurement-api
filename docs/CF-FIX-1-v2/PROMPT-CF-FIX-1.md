# BUILD PROMPT — CF-FIX-1: Custom Fields + Custom Lists testing fixes

You are Claude Code on eProcure (.NET 10 / EF Core 10 / React+TS / PostgreSQL). These are the
operator's testing findings on the Custom Fields and Custom Lists admin screens (from
`Custom_Fields.docx` / `Custom_List.docx`, transcribed verbatim in §APPENDIX). Some are quick
copy/rename fixes; several are real feature work (7 new field types with per-type validation, a
searchable list-style select, value-level parent-child, manual internal id). Read this whole
prompt, then **START WITH STEP 0** — list every file you'll create or modify per task, one line
each, then **WAIT for my confirmation** before writing code.

Read first: `CLAUDE.md`, `docs/design-framework/CHARTER.md` (FieldSpec single-contract rule),
and the current code for `CustomFieldService`, `CustomListService`, `AdminCustomFields.tsx`,
`AdminCustomLists.tsx`, `renderField.tsx`, `useFieldOptions.ts`.

**REFERENCE SCREENSHOTS — the operator attached NetSuite target screens in
`docs/CF-FIX-1/reference-screenshots/`. VIEW THEM — they are the visual target, not the current
build.** These show what "like NetSuite" concretely means for this work:
- `CF-field-entry-form.png` — NetSuite's custom-field entry form: the target property layout
  (Label, ID, Type, Store Value, Show in List, Help, Subtab, Display Type). Match this property
  SET and arrangement (adapted to the Ledger design tokens — do NOT copy NetSuite's visual style,
  copy its structure/capability).
- `CF-type-dropdown-full.png` — the full NetSuite Type dropdown. **This is the source of the
  operator's type list and the AUTHORITATIVE type NAMES** (see A4 — use NetSuite's labels).
- `CF-list-source-picker.png` — how NetSuite picks WHICH list backs a List-type field (the
  searchable source picker).
- `CF-type-searchable.png` — the type dropdown as a **searchable, type-to-filter list**. This is
  the "proper list style, I can type to search, no shortform" the operator wants (A5).
- `CL-list-page.png` — NetSuite custom LIST page (name, id, description, values table).
- `CL-order-option.png` — the "Order Values By: Entered Order / Alphabetical" control (B4).
- `CL-values-table.png` — the values table with auto-numbered ids (B2).
- `CL-parent-child.png` — **the "do something like this" sketch**: NetSuite list values with a
  **Parent column**, each value row selecting another value in the SAME list as its parent, to
  build a tree (B5). This is the concrete target for value-level parent-child.

Match the STRUCTURE and CAPABILITY these show; render them in the eProcure Ledger design language
(teal/cream, Inter, square corners), never NetSuite's chrome.

**Baselines (hold-or-raise, verified on the operator's machine):** dotnet 482 · vitest 234 ·
e2e 71. Every task: atomic commit prefixed `CF-FIX1-Tn:`, all gates green (dotnet test, vitest,
tsc, oxlint 0 errors) before commit. Every user-facing change gets a browser test in
`e2e-audit/tests/`. Hard boundaries unchanged: never touch Payment Vouchers, Contract Mgmt,
NetSuite integration, ApprovalThresholdMyr; no unplanned migrations (the ones named here are the
only ones expected — if you think another is needed, STOP and report).

---

## GROUP A — CUSTOM FIELDS

### A1 — Label & copy fixes (quick, mechanical) — commit `CF-FIX1-T1:`
On `AdminCustomFields.tsx` (and any DTO label/help strings):
1. Rename the field option **"Show In List (Column On the Default List View)"** → **"Show On
   Default List"** everywhere it appears (label, help, any tooltip). The underlying flag/behaviour
   is unchanged — text only.
2. Rename **"Required (At Value – Save Only)"** → **"Mandatory"**. Text only; the save-time
   enforcement is unchanged.
3. **Remove the subtitle** "Fields your team defines — they render, filter and aggregate exactly
   like built-ins." (line ~47) — delete it entirely, no replacement.
4. **Remove the explanatory paragraph** "A field with values is never deleted — deactivating hides
   it from entry screens and the view builder while its data persists; saved views still…" (line
   ~79 area) — delete the whole block. The operator does not want lifecycle explanations on screen.
   The BEHAVIOUR stays (deactivate-when-valued); only the on-screen prose is removed.
5. **Full record-type names everywhere** — replace the short enum names in ALL user-facing copy
   (the record-type tabs/labels on this screen and anywhere a custom-field record type is shown):
   - `Requisition` → **Requisition** (already full — keep)
   - `Rfq` → **Request For Quote**
   - `PurchaseOrder` → **Purchase Order**
   - `Invoice` → **Invoice** (keep)
   - `Asn` → **Advance Shipment Notice**
   - `Vendor` → **Vendor** (keep)
   - `Onboarding` → **Onboarding** (keep)
   This is a DISPLAY mapping only — the enum values and `RecordType` storage stay unchanged. Build
   one shared display-name helper (e.g. `recordTypeLabel(rt)`) and use it everywhere a record type
   is rendered, so this never drifts. Browser test: the RFQ tab reads "Request For Quote", the PO
   tab reads "Purchase Order", the ASN tab reads "Advance Shipment Notice".

### A2 — Remove "Insert Before" — commit `CF-FIX1-T2:`
Remove the **Insert Before** option from the custom-field create/edit modal entirely. Field
location/placement is set later when the user customises forms (CF5 layout editor already owns
placement). Remove the UI control, the request field, and the sibling-order-normalization logic
that backed it — but leave the plain `Sort` integer intact (it's still the default order). Do NOT
remove anything the form layout editor depends on. Browser test: the modal no longer shows Insert
Before; creating a field still works and lands in the list.

### A3 — Manual internal id (user-input, like NetSuite) — commit `CF-FIX1-T3:`
Today `Code` auto-derives from the label (`cf_` + slugified label). Change to **user-input**:
- The create modal gets an **Internal ID** text field. Auto-suggest a value from the label as the
  user types (so it's convenient), but let the user override it. Prefix `cf_` is applied/enforced
  by the system (the user types the meaningful part, or the system keeps the `cf_` convention —
  match NetSuite's feel: user controls the id, system guarantees the namespace).
- **Validation:** required; unique across custom fields (the existing `AnyAsync(d => d.Code ==
  code)` check stays, now against the user value); allowed characters only (letters, digits,
  underscore); immutable after create (unchanged — it's already immutable, just now user-set).
- Keep it a per-record-type namespace exactly as today. Browser test: create a field with a chosen
  Internal ID; try a duplicate → blocked with a clear message; try illegal chars → blocked.

### A4 — Field type expansion + per-type validation — commit `CF-FIX1-T4:` (the big one)
Today: `CustomFieldDataType { Text, LongText, Int, Decimal, Money, Date, Bool, ListValue }` (8),
backed by six sparse value columns (`ValueText, ValueNumber, ValueMoney, ValueDate, ValueBool,
ValueListCode`) with two CHECK constraints (`ExactlyOne`, `KindMatch`).

**Target type list (the operator's set — use the NetSuite display NAMES from
`CF-type-dropdown-full.png`, since the operator is matching NetSuite):** Free-Form Text, Long
Text, Image, Integer Number, List/Record, Decimal Number, Currency, Percent, Check Box, Date,
Date/Time, Document, Hyperlink (with a label), Email Address, Phone Number. Use these exact
labels in the type picker (the operator explicitly wants full professional names, no shortforms).
The ENUM member names in code stay code-style (see mapping); only the DISPLAYED label matches
NetSuite. Confirm the label↔enum mapping in Step 0.

**Map each target type to storage — DO NOT add a column per type; reuse the sparse columns and add
only what's genuinely new.** Proposed mapping (confirm in Step 0):

| Target type | Enum member | Backing column | Validation |
|---|---|---|---|
| Text | Text (exists) | ValueText | max length |
| Long Text | LongText (exists) | ValueText | max length (longer) |
| Integer Number | Int (exists) | ValueNumber | whole number only |
| Decimal Number | Decimal (exists) | ValueNumber | numeric |
| Currency | Money (exists) | ValueMoney | numeric(18,2), grouped display (CF1-T1) |
| List | ListValue (exists) | ValueListCode | must be a live list value |
| Checkbox | Bool (exists) | ValueBool | true/false |
| Date | Date (exists) | ValueDate | **dd/mm/yyyy only — reject other formats** |
| **Date/Time** | **DateTime (NEW)** | **ValueDateTime (NEW `timestamptz`)** | dd/mm/yyyy HH:mm |
| **Percent** | **Percent (NEW)** | ValueNumber (reuse) | numeric, 0–100, display with % |
| **Email Address** | **Email (NEW)** | ValueText (reuse) | valid email regex |
| **Telephone Number** | **Telephone (NEW)** | ValueText (reuse) | phone format validation |
| **Hyperlink** | **Hyperlink (NEW)** | ValueText (url) + **ValueLabel (NEW, nullable)** for the link text | valid URL |
| **Image** | **Image (NEW)** | ValueText (stored-file id/ref) | image mime types only |
| **Document** | **Document (NEW)** | ValueText (stored-file id/ref) | any attachment; ties to FileStore |

**Migration `CustomFieldTypeExpansion`:**
- Add enum members: `DateTime, Percent, Email, Telephone, Hyperlink, Image, Document`.
- Add columns: `ValueDateTime (timestamptz null)`, `ValueLabel (text null, for Hyperlink label)`.
- **Extend BOTH CHECK constraints** (`CK_CustomFieldValues_ExactlyOne` and `_KindMatch`) to cover
  the new type→column rules. This is the load-bearing part — the ExactlyOne check must count
  `ValueDateTime`; the KindMatch check must map every new DataType to its column. Get this exactly
  right or inserts fail. (`ValueLabel` is NOT a value column — it's a companion to Hyperlink's
  ValueText, so it is EXCLUDED from the ExactlyOne count; document this in the migration.)
- Types that reuse `ValueText`/`ValueNumber` (Email, Telephone, Hyperlink, Image, Document,
  Percent) key off the DataType in KindMatch, e.g. `("DataType" IN ('Text','Email','Telephone',
  'Hyperlink','Image','Document') AND "ValueText" IS NOT NULL)`.

**Per-type validation (server-side, authoritative — the operator explicitly wants this):**
- Date: parse strictly as `dd/MM/yyyy`, reject anything else with a clear 400.
- Date/Time: `dd/MM/yyyy HH:mm`.
- Email: RFC-ish regex; reject invalid.
- Telephone: digits/space/`+`/`-`/`()`; reasonable length.
- Hyperlink: valid absolute URL (http/https); the label is free text.
- Percent: numeric, 0–100.
- Integer: whole number (no decimals).
- Currency/Decimal: numeric within precision.
- Image: mime must be an image type; Document: any file — both go through the existing FileStore
  ownership/validation path (SEC discipline — do not open a new unvalidated upload route).
- Every validation has a matching client hint (input mode, placeholder, inline error) AND a server
  enforcement (the client hint is UX; the server is the guarantee). Add xUnit tests per type: valid
  value saves, each invalid form is rejected.

**Image/Document note:** these store a StoredFile reference in ValueText. Reuse the existing
FileStore + FileAccessPolicy (the same ownership-scoped path vendor onboarding files use). If
wiring the upload into the custom-value save is more than a bounded change, STOP and report — do
NOT build a new file pipeline. If it's bounded, do it; if not, ship the other 6 new types and
defer Image+Document with a logged reason (they're the two that depend on the file path).

Browser test: create one field of each NEW type, enter a valid value, enter an invalid value and
see it rejected on screen (esp. Date rejecting `2026-13-40` and a US-format date; Email rejecting
`notanemail`; Hyperlink showing its label).

### A5 — Proper searchable list-style select — commit `CF-FIX1-T5:` (shared with Group B)
The operator dislikes the current plain `<select>`: can't type to search, uses shortforms. Build a
**standardized searchable select component** used everywhere the app picks from a list — the
type-picker, the record-type picker, custom-field List values, and the custom-list value pickers:
- Type-to-filter (search within options).
- Full names, no shortforms.
- Consistent with the Ledger design tokens (teal/cream, square corners, Inter) — NOT a raw browser
  select.
- **Three variants** the operator named: (1) **normal list** (single select), (2) **multi-select**,
  (3) **multi-select with relationships** (dependent/cascading — the child options filter by the
  chosen parent, using the existing `ParentValueCode` mechanism). Build the component to support all
  three; wire (1) everywhere now; (2) and (3) where the data model already supports them (list
  values with parents). If a variant has no consumer yet, build the component capability but note
  where it's not yet wired.
- This is a FieldSpec-family component — it must go through the single render pipeline, not a
  one-off. Follow the charter's single-contract rule; do not create a rival Field component.
Browser test: open a list picker, type to filter, select; open the dependent variant, pick a parent,
confirm children filter.

---

## GROUP B — CUSTOM LISTS

### B1 — Internal ID standardization (drop Code/Name confusion) — commit `CF-FIX1-T6:`
The operator finds Code-vs-Name confusing and wants **one concept: Internal ID**, same as custom
fields, everywhere — "don't need Code."
- On the LIST: present a single **Internal ID** (user-input, like A3) + **Name** + Description. The
  "Internal ID" is what today is `Code`; stop showing a separate technical Code alongside Name. If
  the model keeps `Code` internally that's fine, but the UI shows ONE id field labelled Internal ID,
  user-set with the same validation as A3 (required, unique, legal chars, immutable).
- Keep behaviour identical; this is a UI-and-labelling consolidation so the user sees one id, not two.
Browser test: create a list — one Internal ID field, no separate Code; duplicate id blocked.

### B2 — List VALUES get automatic numeric internal ids — commit `CF-FIX1-T7:`
Today list values carry a `Code` (user/seed-set). The operator wants value internal ids
**automatic: 1, 2, 3… based on entry order** (not user-typed, unlike the list-level id).
- When adding values, assign the value's internal id automatically as an incrementing integer in
  entry order (1,2,3…). The user types only the Label; the id is system-assigned and shown read-only.
- This is the STORED code records carry — confirm existing records/seeds still resolve (the seed
  currently sets string codes like "MY"; decide and document: either keep existing string codes for
  seeded system lists and auto-number only NEW user lists' values, OR migrate — pick the
  non-breaking option and justify in Step 0. Auto-number new values; do not renumber existing
  referenced values, that would orphan stored data).
Browser test: add three values, their ids read 1/2/3 in entry order, id is read-only.

### B3 — "Depends on" — explain, verify, harden — commit `CF-FIX1-T8:`
The operator wants the dependent-list ("Depends on") mechanism explained and made robust. In your
Step 0 / report, explain in plain terms how it works TODAY:
- A list can have `ParentListCode` (STATE depends on COUNTRY); a value has `ParentValueCode` (the
  state "Selangor" points to country "MY"). When a field renders a dependent list, the child options
  filter by the parent field's chosen value (via `useFieldOptions(parentValue)` and the
  `(CustomListId, ParentValueCode)` index).
Then HARDEN it — audit and cover these scenarios (add tests):
- A dependent value whose parent value is later deactivated/deleted (orphan) — define & enforce
  behaviour (block delete of a referenced parent, or cascade-deactivate — mirror the never-silently-
  orphan discipline; pick and document).
- A cycle in list-level dependencies (A depends on B depends on A) — reject.
- A value pointing to a non-existent parent value — reject at save.
- Changing a list's ParentListCode after values exist — guard or re-validate.
Browser test: build COUNTRY→STATE, confirm child filters by parent; attempt an orphan/cycle and see
it blocked.

### B4 — Order-mode selector on CREATE (not just EDIT) — commit `CF-FIX1-T9:`
The **"Show options in Entered order / Alphabetical"** selector currently appears only when editing
a list. Make it appear on **CREATE** as well, defaulting to Entered. Same control, same stored flag
(the CF1 `CustomListOrderMode`), just surfaced in the create modal too. Browser test: create a list,
pick Alphabetical at create time, values render alphabetically.

### B5 — Value-level parent-child authoring (the operator's sketch) — commit `CF-FIX1-T10:`
This is the "set a parent from previously-entered values" feature. When entering values for a list,
the user can pick, for a value, one of the **previously-entered values in the same list** as its
parent (self-referential parent-child within one list's values — distinct from B3's list-to-list
dependency; here it's value-to-value within a list, e.g. a category tree).
- The value-entry UI offers a parent picker populated with the already-entered values of THIS list
  (using the searchable select from A5).
- **Security/validation (the operator explicitly asks — analyse for all scenarios):**
  - A value cannot be its own parent (same line) — reject.
  - No cycles (A→B→A, or deeper) — reject at save with a clear message.
  - Parent must be an existing value in the same list — reject dangling.
  - Deactivating/deleting a value that is a parent of others — guard (block or cascade, mirror B3).
  - (Analyse and cover any other: e.g. depth limits, moving a subtree.)
Model note: `CustomListValue.ParentValueCode` already exists — this is largely wiring the authoring
UI + the validation guards onto existing storage. Browser test: build a 2-level value tree; attempt
self-parent and a cycle, both blocked.

---

## SEQUENCING
Do the quick wins first so the operator sees progress, then the heavy items:
A1 → A2 → B4 → B1 → A3 (manual id — do fields + lists id together conceptually) → B2 → B3 → B5 →
A5 (the shared select — build once, it serves A4's List type and B5's parent picker) → A4 (the
type expansion — largest, do last with A5's select available).

Actually build A5 (searchable select) BEFORE A4 and B5 since they consume it. Final order:
**A1, A2, B4, B1, A3, B2, A5, A4, B3, B5.** Confirm or adjust in Step 0.

## PER-TASK & FINAL
- Each task: Step-0 confirmed → build → gates green → atomic commit → browser test → completion note.
- Final report: five-lens review (vendor/buyer/solution-architect/senior-programmer/CTO), the B3
  "how depends-on works" explanation the operator asked for, the full validation matrix for A4
  (type → client hint → server rule → test), suite counts (hold-or-raise vs 482/234/71), and a
  line-by-line map of every numbered item in `Custom_Fields.docx` and `Custom_List.docx` to where
  it's satisfied.
- Tag `v1.6-cf-fix-1`.

Do not begin coding until I confirm your Step 0 file plan.

---

## APPENDIX — the operator's verbatim testing notes

### Custom_Fields.docx
1. Rename "Show In List (Column On the Default List View)" to "Show On Default List"
2. Rename "Required (At Value – Save Only)" to "Mandatory"
3. Remove Insert Before option — field location is set when customizing forms.
4. Types needed: Text, Long Text, Image, Integer Number, List, Decimal Number, Currency, Percent,
   Checkbox, Date, Date/Time, Document (attachments), Hyperlink (with label option), Email Address,
   Telephone Number — with necessary validation per type (e.g. Date only dd/mm/yyyy, reject others;
   other types validated per their properties).
5. Dislikes the List select — can't type to search, uses shortform. Create a proper list style,
   standardized, for normal list AND multi-select AND multi-select-with-relationships.
6. No field for internal id — make it user-input rather than auto, like NetSuite.
7. Remove the "A field with values is never deleted — deactivating hides it from entry…" text and
   the subtitle "Fields your team defines — they render, filter, aggregate…" — no such explanations
   on screen.
8. Use full professional names — Rfq → Request For Quote, Purchase Order, Invoice, Advance Shipment
   Notice, Vendor, Onboarding.

### Custom_List.docx
1. Difference between Code and Name? Standardize using Internal ID like Custom Field; drop Code.
2. "Depends on" is interesting — explain how it works; make the core robust, cover all scenarios.
3. Values also have Code and Name — make internal id automatic for list values: 1,2,3 by entry order.
4a. "Show Options in Entered/Alphabetical" only appears on Edit, not Create — make it visible on
    Create too.
4b. Same dislike of the selection list look — inconsistent with design, can't type to search.
5. Parent-child between list values — when inputting values, set one of the previously-entered
   values as a parent. Security: cannot set the same value as its own parent (same line); analyse
   other scenarios needing validation.

# PROMPT — D0: Field & Pattern Inventory (zero code changes)

Read README-FIRST.md first. D0 is pure analysis — its output is the charter
that keeps D1 honest. It may run in parallel with Slice H (no code touched).

## Deliverable
A single committed document: docs/design-framework/D0-INVENTORY.md, plus an
updated docs/design-framework/CHARTER.md (created from README-FIRST's design
principles, expanded where this inventory sharpens them).

## Task 1 — Field census
Sweep web/src for every form element: every <input>, <select>, <textarea>,
<button>, plus the 12 answer types in lib/answers.tsx and lib/formTypes.ts.
For each DISTINCT configuration produce a row:
  Element | Data type | States used (required/disabled/readonly/error/help/
  prefix/suffix/default) | Special behaviour (dependent filtering, mono code,
  currency, date, search, multi) | Files using it | Count
Collapse duplicates; the point is the distinct-configuration list, with
counts proving frequency. Include the three rival local Field components
(ManualVendorForm, OnboardingForm, PrForm) — union their feature sets into
one row set and mark which features each lacks.

## Task 2 — Primitive catalogue (derived, not invented)
From Task 1, derive the D1 primitive list. For each primitive: name,
FieldSpec surface it needs, which census rows it absorbs. Every census row
must map to exactly one primitive; every primitive must absorb at least one
census row. Rows that fit no clean primitive → a "misfits" section with a
recommendation (usually: a primitive variant, occasionally: leave bespoke
with justification).
Expected shape (verify against reality, do not assume): TextField,
TextAreaField, NumberField, MoneyField (currency-aware), DateField (ISO in,
dd/MM/yyyy display, timezone label where instant), SelectField,
DependentSelectField (ParentValueCode pattern), MultiSelectField,
YesNoField, CodeField (JetBrains Mono), AttachmentField, TableField,
plus Button variants (primary/ghost/danger/small) and the pill/badge set.

## Task 3 — Pattern census
The composition layer: form sections/field groups, subtab usage, modal
variants (plain, confirm, reason-modal), drawers, toasts/notices, empty
states, status pills (every distinct pill palette in use), table patterns
(sortable? expandable rows? bulk select?), the tooltip usages. Same format:
distinct pattern | where used | count. This feeds D2 but is captured now
while the sweep is warm.

## Task 4 — FieldSpec contract (proposed, for my sign-off)
Draft the TypeScript FieldSpec interface that Task 2's primitives consume:
key, label, dataType, required, disabled, readOnly, help, placeholder,
defaultValue, options source (static | customList code | dependent),
validation surface, and the extension points D5/D7 will need (displayType,
sourcing) marked as reserved-but-unused. Justify each member against a
census row. This contract is the single most load-bearing artifact of the
framework — it gets my explicit sign-off before D1 begins.

## Task 5 — Gap list
What the census shows the product LACKS that D1 must not accidentally build
anyway (per charter rule 6): note candidates for later slices instead.

## Report format
The inventory document itself, plus a five-lens review (per README-FIRST),
plus the three numbers: distinct field configurations found, primitives
derived, census rows unabsorbed. Commit as
"design-d0: field and pattern inventory + FieldSpec proposal". No other
files change.

# STEP 0 — CF6: Custom LINE fields + sublists as objects (plan only — no code built)

**Closes:** PLAN §2 (line-scoped custom fields — "genuine new architecture") and the
sublist portion of §4/§5 (pick-sublist-for-a-subtab, line-field show/hide/reorder).
**ONE coupled slice — do not split** (the PLAN's own instruction): line fields
without sublist placement have nowhere to render; sublist objects without line
fields have nothing custom to show.

**Depends on CF5:** subtabs as objects (a sublist is PLACED IN a subtab).
CF6 must not start before CF5's `EntryFormLayout` migration has landed.

## The LineId model decision (justified)

`CustomFieldValue` is keyed `(RecordType, RecordId)`; lines (PrLine, RfqLine,
PoLine) are children with their own Guids. Three options considered:

- (a) **Separate `CustomFieldLineValue` table** — clone of the value table +
  `LineId`. Rejected: duplicates the six-typed-column machinery, the CHECKs,
  the hydration, and every service path — permanent double maintenance.
- (b) Widen the key with a nullable discriminator on the EXISTING table:
  add `LineId (Guid?)` to `CustomFieldValue`; null = header value (today's
  rows unchanged — no backfill needed). Unique index becomes
  `(FieldDefId, RecordId, LineId)` with NULLS NOT DISTINCT semantics
  (Postgres 15+: `NULLS NOT DISTINCT`, available on PG16). **CHOSEN**: one
  value pipeline, one hydration path, zero data migration, the orphan-integrity
  test extends to lines with one added clause.
- (c) JSON blob per line. Rejected outright — violates the "typed core, no JSON
  values, ever" charter rule.

`CustomFieldDef` gains `Scope` (enum: `Header` | `Line`, default Header) — a
line field is authored on the SAME Admin surface with a Scope choice (NetSuite's
body vs column field distinction). Scope is immutable after creation (like
Code/RecordType/DataType, and for the same corruption reason).

## Sublists as objects

New entity `EntryFormSublist` — Id, FormDefId(FK), SubtabId(FK — a sublist LIVES
in a subtab; body placement not offered, matching NetSuite), SublistKey (closed
set per record type: e.g. Requisition → `Lines`; PurchaseOrder → `Lines`;
Rfq → `Lines`, `Invitees`), Sort, Hidden.

`EntryFormSublistField` — Id, SublistId(FK), FieldKey (native line column OR a
Scope=Line custom field code), Sort, Hidden, Label(override, null → registry).
This is the "show/hide/reorder line fields" surface. Native line columns enter
the registry as new `FieldKind.NativeLine` rows (seeded per record type from
the existing typed line DTOs — the same census discipline as D1).

## Migrations (all named)

1. `CustomFieldLineScope` — `CustomFieldDef.Scope` + `CustomFieldValue.LineId`
   + replaced unique index (NULLS NOT DISTINCT) + extended CHECK (unchanged
   columns; LineId is orthogonal to the one-populated-column rule).
2. `EntryFormSublists` — the two new tables.
   (Two migrations because they land with different tasks; both in CF6.)

## API surface

- Defs: existing custom-fields endpoints + `scope` in the DTO/request; list
  filterable by scope. Line-scoped defs REFUSE `ShowInList` (header-list
  semantics don't apply) — loud 400.
- Values: `PUT /custom-values/{recordType}/{recordId}` payload gains optional
  per-line dictionaries: `{ values: {...}, lines: { [lineId]: {...} } }`.
  Validation: LineId must belong to the record (server-checked via the record
  type's line ownership — extends `IRecordReachability`); same typed
  validation; Required applies per line at value-save.
- Sublists: CRUD under `/entry-forms/{id}/sublists[...]`, field rows under
  `/sublists/{id}/fields`. Same belongs-to-form guards as CF5 containers.

## UI

- AdminCustomFields: Scope selector on create (Header/Line); line defs badge.
- The CF5 designer: a subtab node offers "Add sublist" (from the record type's
  closed SublistKey set); sublist node lists its columns (native + custom line
  fields) with show/hide/reorder — the SAME chip interaction as body fields.
- Record surfaces: line tables (PrForm lines, PO lines) render the resolved
  sublist columns; custom line cells edit inline (Normal) or render per the
  def's DisplayType (CF4 semantics carry over unchanged).

## Task breakdown (browser-gated)

- CF6-T1: `CustomFieldLineScope` migration + Scope on def + line-value write
  path + reachability line check. Gate: xUnit line-value round-trip + orphan
  sweep extended; browser: admin creates a Line-scope field, buyer enters a
  per-line value on a PR line, reload persists.
- CF6-T2: NativeLine registry census + `EntryFormSublists` migration + sublist
  CRUD. Browser: sublist appears under a subtab with native columns.
- CF6-T3: sublist field show/hide/reorder + custom line columns render. Browser:
  hide a native column + add the custom column, PR line table reflects both.
- CF6-T4: end-to-end journey — author line field → place → populate → line
  table shows it for the mapped role only (role resolution unchanged).

## Risks / operator decisions wanted

1. View/aggregate pipeline stays HEADER-ONLY this slice (line values are not
   searchable/summable yet) — honest deferral, noted in the ledger; line-level
   search belongs to a later CF7 extension. Agree?
2. Closed SublistKey set per record type (no user-defined sublists) — NetSuite
   custom sublists are saved-search-backed; ours would be CF7+ territory.
3. GRN/ASN lines exist too — first delivery is PR/PO/RFQ lines (the operator's
   named pain); Asn lines follow the same pattern when pulled.

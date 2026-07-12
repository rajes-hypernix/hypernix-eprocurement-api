# PROMPT — D5: Custom Fields

Governing constraints: docs/design-d0-d1/README-FIRST.md in full.
Prerequisites: D3 registry (Kind=Custom reserved), D4 metric/KPI chain, RM
catalog. This is the slice the metadata-consuming-primitives ruling was made
for: a custom field renders through the IDENTICAL FieldSpec pipeline as every
built-in field — no second path, or the slice has failed its charter.

Headline gate: an ADMIN creates "Warranty Expiry" (Date) on PurchaseOrder in
a Setup screen; a BUYER populates it on a real PO, filters a PO saved view
by it, and pins a KPI counting POs where it expires within 90 days — same
session, zero deployments, performed by personas as a permanent spec.

## Step 0 — Report and WAIT
File plan per phase, five-lens review, and five discovery answers:
(a) INITIAL DATATYPE SET: propose which FieldSpec DataTypes D5 supports at
    launch, each justified. Recommendation to argue with: Text, LongText,
    Int, Decimal, Money, Date, Bool, ListValue (rides CustomListId +
    the existing dependent machinery). RecordRef and DateTime deferred to
    BACKLOG unless you find a concrete near-term consumer — speculative
    types violate rule 6 even here.
(b) VALUE WRITE PATH + AUTHORIZATION: propose the endpoint shape for
    reading/writing a record's custom values and its action model. My
    prior: one generic pair (GET/PUT /api/custom-values/{recordType}/{id})
    whose authorization DYNAMICALLY rides the record type's existing
    edit/view actions (the per-metric View* machinery from D4, reused) —
    argue if the record-edit piggyback is better. Vendor principals: which
    record types' values can they read/write, if any, this slice? (Prior:
    read-only where they can read the record; no vendor writes until a
    concrete need — deny by default.)
(c) RENDER SURFACE: where custom fields appear on Transaction/Entity pages
    — propose a "Custom fields" section per archetype fed by the defs
    (FieldSpec-built), and name exactly which screens light up in this
    slice (PO detail is the gate; enumerate the others that come free via
    the archetypes vs any that need wiring).
(d) LIFECYCLE SEMANTICS: def deactivation rules — my priors to confirm or
    argue: defs with values are NEVER hard-deleted (deactivate only);
    deactivated defs hide from entry surfaces and the view builder but
    existing values persist; a saved view referencing a deactivated key
    fails loudly per D3's rule (operator fixes the view). Required-flag
    semantics THIS slice: enforced at value-save only, NOT gating record
    lifecycle transitions (that is D7 form-engine territory) — state it in
    the docs so nobody assumes otherwise.
(e) CATALOG ROWS: propose (prior: A6x ManageCustomFields = Admin, carried
    by the def CRUD endpoints; value endpoints ride dynamic record
    actions, no new rows). No-orphan rule as always.

## Phase 1 — Schema (one migration: CustomFields)
CustomFieldDef (Code UQ e.g. cf_warranty_expiry, Label, RecordType enum,
DataType enum, CustomListId FK nullable, Required, HelpText, Active, Sort,
CreatedUtc/UpdatedUtc) and CustomFieldValue (FieldDefId FK Restrict,
RecordType, RecordId, the SPARSE TYPED value columns — one per supported
DataType, ValueDate a real date, ValueMoney numeric(18,2) — UpdatedUtc,
UQ(FieldDefId, RecordId), CHECK constraint: exactly the def's column
populated). Def creation auto-inserts the FieldRegistry row (Kind=Custom,
DataType mirrored); the reflection drift-test extends to assert
Custom-kind rows always have a living def. Polymorphic (RecordType,
RecordId) is the accepted cost: delete-path cleanup wired into the owning
aggregates' delete flows where they exist, plus an orphan-check integrity
test (values whose record is gone = red). Up→Down→Up proven; seed loops
idempotent per the D4 repair pattern.

## Phase 2 — API + registry integration
Def CRUD (Setup surface) + the value endpoints per (b), all
action-annotated. D3/D4 integration is mostly FREE and must be PROVEN, not
rebuilt: a Custom-kind registry row becomes filterable in the view builder,
aggregatable in /aggregate and /series (a Money custom field sums; a Date
custom field buckets), with tests pinning each — including the honest-null
semantics (records with no value row = excludedNullCount, never zero).
Sweeps unchanged; RoleMatrix auto-cases counted.

## Phase 3 — UI
- Setup screen (SetupPage archetype): def list per record type, create/edit
  (label, type, required, help, list binding for ListValue), deactivate
  with the (d) semantics surfaced in the UI copy.
- The "Custom fields" section on the (c) surfaces: defs → FieldSpec array →
  the D1 pipeline, saved via the value endpoints; required enforced at
  save; ListValue renders the same dependent-select machinery as built-ins.
- View builder: Custom-kind fields appear grouped/labelled as custom;
  KPI/Add-KPI flows inherit them with zero changes (prove it).

## Phase 4 — THE GATE + docs
The full persona chain as a permanent spec + screenshots: admin creates the
def → buyer populates on a PO → filters a view by it → pins the expiring-
in-90-days KPI (count where ValueDate <= @today+90d — if the relative-token
set needs a +Nd form, that is a gate-driven token addition per D3's rule,
justified in the report) → number matches. Docs: DATA-MODEL (tables, grain,
the sparse-column pattern and CHECK), PRIMITIVES (custom-field section
pattern), matrix, BACKLOG (deferred DataTypes; reporting SQL views per
record type EXPLICITLY deferred with rationale — no consumer until the
NetSuite/external-analytics era, and the registry path already serves
views/KPIs; the D6 note that segments, not fields, are the answer for
dimensions).

## Verification
Baselines from D4 close (API 410, web 211, e2e 44) held or raised; full
crawl at close; CI per phase; migration proof on a FRESH database (the D4
lesson is now standing procedure); five-lens review — the CTO lens states
what "SPSB adds a field" now costs versus a change request.

## Out of scope
Custom segments (D6); form-behaviour/required-gating-transitions (D7);
custom records (L4 — but Phase 1's enums must not foreclose the reserved
Custom member); reporting SQL views (BACKLOG with rationale); RecordRef/
DateTime unless (a) justifies; any new npm dependency.

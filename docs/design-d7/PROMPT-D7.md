# PROMPT — D7: Form-Behaviour Engine & Numbering

Governing constraints: docs/design-d0-d1/README-FIRST.md in full. Standing
procedures: fresh-DB replay, idempotent seeds, exit-code gating,
commit-per-phase CI. Baselines at start (D6 close): API 427, web 218,
e2e 46 — record exact, hold or raise.

D7 is the last framework engine: forms carry BEHAVIOUR (which fields, in
what groups/subtabs, with what display type, mandatory-ness, defaults,
sourcing) per role — and numbering becomes admin configuration over the
Slice G generator. D5's parked disclaimer ("required gating is form-engine
territory") comes due here, honestly scoped.

Frozen gate, adaptable per Step 0(a): an admin builds a role-specific PO
form hiding finance fields, auto-selected for that role; PO numbering
format changed from Setup and the next PO proves it.

## Step 0 — Report and WAIT
File plan per phase, five-lens review, and six discovery answers:
(a) CONSUMING SURFACES (expect OPERATOR-DECISIONs): survey which screens
    can honestly consume entry-form definitions this slice. PrForm
    (TransactionPage, FieldSpec-driven) is the natural entry-form consumer
    — its current spec array becomes the seeded "Standard PR Form". The
    frozen gate names a PO form, but PO detail is a grandfathered
    non-composing page and POs have no entry path (transition-only, the
    D5 finding). Bring options with costs: (i) PO detail migrates to
    EntityPage and consumes a DISPLAY-form definition (fields visible/
    hidden per role) — the gate as written; (ii) the role-form concept is
    gate-proven on the PR form (hide dimension/finance fields for a role)
    + numbering proven on PO, with PO display-forms joining the PoScreens
    migration row. Recommend with evidence; the gate's SUBSTANCE (admin
    composes a role form, role gets it automatically, no deploy) must
    survive whichever shape.
(b) FIELD UNIVERSE PER FORM: a form definition addresses FieldRegistry
    keys (all three Kinds — native, custom, segment) for its record type.
    Survey which native keys are honestly form-controllable on the chosen
    surfaces (some fields are structural — code, status, totals — and
    must be marked non-removable/system in the definition). Propose the
    per-record-type system-field set.
(c) DISPLAY TYPES + REQUIRED, the honest enforcement boundary: display
    types (normal/disabled/inline/hidden) and requiredOnForm are enforced
    where the form renders (FieldSpec displayType — the D1 reserved
    member finally activates) and at that form's save/submit. State the
    server-side boundary honestly: write contracts carry no form id, so
    per-form server validation is NOT claimable this slice — server keeps
    enforcing def-level rules (D5 required, domain guards); the per-form
    layer is UI-enforced with the boundary documented in PRIMITIVES, or
    propose carrying formCode on the write DTOs if that is cheap and
    honest. Recommend.
(d) DEFAULTS + SOURCING: propose the defaulting surface (static value per
    form field; @today-token for dates riding the D3 parser — no
    expression language this slice, rule 6) and confirm dependent
    sourcing = the existing ParentValueCode/dependent-select machinery
    referenced per form field, not a new mechanism.
(e) SUBTABS (the operator addition): form definitions own layout —
    sections/field-groups AND named subtabs as pure layout containers.
    Confirm the archetypes' existing subtab slots can render
    definition-driven subtabs; state what happens to sublists (they keep
    their built-in subtab homes; admin-defined subtabs hold FIELDS only —
    custom sublists are the recorded L4 boundary).
(f) NUMBERING + CATALOG: NumberingScheme admin (prefix, year segment,
    digits, per record type) as config over NumberSequences — the Slice G
    FOR-UPDATE generator unchanged, scheme consulted at format time;
    propose collision/uniqueness rules on format change (next code proves
    it; history untouched). Catalog rows (prior: A69 ManageEntryForms
    Admin, A70 ManageNumbering Admin — argue if the honest shape differs;
    form READS ride the record's View* dynamically like everything else).

## Phase 1 — Schema (one migration: EntryForms)
EntryFormDef (Code UQ, Name, RecordType, IsPreferred/role mapping via
EntryFormRoleMap UQ(RecordType-scoped, Role), IsSystem, Active, Utc),
EntryFormField (FormDefId FK, FieldKey→registry-validated, Subtab,
FieldGroup, Sort, DisplayType enum, RequiredOnForm, DefaultValue,
SourceFieldKey nullable), NumberingScheme (RecordType UQ, Prefix,
YearSegment, Digits, Active, Utc). Seed the Standard forms per (a)'s
ruling reproducing today's layouts EXACTLY (parity: the seeded standard
form renders byte-identical to the hardcoded spec it replaces — crawl
proves it) + today's numbering formats. Registry-liveness validation on
save/render per the D3/D5 rule. Fresh-DB replay.

## Phase 2 — API + engine
Form CRUD + role-map + resolve endpoint (GET the caller's form for a
record type: role-preferred → standard fallback); numbering CRUD + the
format-time consultation in CodeGenerator (gap-free FOR-UPDATE untouched);
catalog rows + matrix; tests: resolution matrix (role-preferred beats
standard, multi-role deterministic — state the rule), registry-liveness,
display-type/required semantics at the (c) boundary, numbering format
change → next code proves + concurrency test still green, RoleMatrix
auto-cases, sweeps unchanged.

## Phase 3 — UI
AdminEntryForms (SetupPage): form list per record type, the form composer
(field palette from the registry grouped by Kind, add/remove/reorder,
group + subtab assignment, display type, required, default, role
assignment) — composed FROM D1 primitives (the adoption tests hold you to
it); AdminNumbering (SetupPage). The (a)-ruled consuming surfaces render
from resolved definitions — PrForm's spec array replaced by the resolver
with the seeded standard form (parity), role forms kick in per the map.

## Phase 4 — THE GATE + docs
Persona spec 08-entry-forms.spec.ts + screenshots: admin composes the
role form per (a)'s ruling (fields hidden, one made required, one
defaulted, a custom field placed on an admin-defined subtab), assigns the
role; that persona loads the surface and gets the composed form (hidden
gone, required blocks save empty, default pre-filled); numbering: admin
changes the PO format in Setup, buyer triggers the next PO, the code
proves it, history untouched. Docs: DATA-MODEL, PRIMITIVES (the (c)
enforcement boundary VERBATIM + the L4 sublist boundary), matrix, BACKLOG
(expression defaults; per-form server validation if deferred; PO
display-forms row if (a)=ii). Full crawl at close.

## Out of scope
Custom sublists / L4; workflow/approval routing (L5); expression language;
print templates; conditional field logic (show-if — gate-driven future);
any new npm dependency.

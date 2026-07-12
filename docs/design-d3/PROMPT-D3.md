# PROMPT — D3: Saved Views Engine

Governing constraints: docs/design-d0-d1/README-FIRST.md applies in full
(charter, five lenses, no-dummy doctrine, baselines, commit discipline).
Prerequisite, now real: the RM catalog + /api/auth/permissions — the
executor scopes against server-enforced permissions, not conventions.

D3 delivers the query definition layer: one saved view powers list screens
now, and portlets/reminders/KPIs in D4. Design it as that shared heart.

## Step 0 — Report and WAIT
File plan per phase, five-lens review, and four discovery answers:
(a) OPERATOR SET, derived not invented: inventory every client-side filter
    across the list screens (Requisitions' 6 facets, RfqList tabs,
    invoice/PO/onboarding filters, search boxes). Derive the minimal
    operator set that expresses them (expect: eq, neq, contains, gt/gte/
    lt/lte, between, in, isEmpty; date-relative like "this month" only if
    a real filter needs it). Each operator cites the filter it absorbs.
(b) FIELD REGISTRY SEED: per record type (PR, RFQ, PO, Invoice, ASN/GRN,
    Vendor, Onboarding), the native FieldKeys to seed — from the DTOs the
    list screens actually render, keyed per DATA-MODEL.md. Kind=Native;
    Custom/Segment are reserved enum members (D5/D6 add rows, zero reshape).
(c) EXECUTOR SCOPING: for each record type, name the existing scoped query
    the executor will BUILD ON (e.g. the vendor-scoped RFQ list rule, the
    Slice F conventions). The executor must never compose a fresh
    unscoped query — it decorates the same scoped sources the services
    use. Plus: executing a view requires the caller's View* action for the
    record type (catalog-checked), 403 otherwise.
(d) PROOF SCREEN: recommend which retrofitted ListPage screen becomes
    saved-view-driven this slice (RfqList is the likely candidate — already
    on ListPage; Requisitions is richer but unmigrated and has its own
    BACKLOG gate). One screen, fully; not two, partially.

## Phase 1 — Schema + registry (one migration: SavedViews)
Tables per the framework data model, typed rows, no filter blobs:
- FieldRegistry (RecordType, FieldKey UQ per type, Kind enum
  Native/Custom/Segment, Label, DataType; def FKs nullable for later kinds)
- SavedView (Code UQ, Name, RecordType, OwnerUserId nullable,
  IsShared, IsSystem, CreatedUtc/UpdatedUtc)
- SavedViewFilter (SavedViewId FK Restrict, FieldKey, Operator enum,
  Value, Value2 nullable, Sort)
- SavedViewColumn (SavedViewId FK Restrict, FieldKey, Label nullable,
  Sort, SortDirection nullable)
Registry seeded from (b) in the migration. FieldKey validation against the
registry on save AND on execute (a view referencing a dead key fails
loudly, never silently drops the filter). Down() drops cleanly.
Catalog rows: ManageOwnSavedViews (all roles — vendors included, their
scoping makes it safe), ManageSharedViews (Buyer, Admin — sharing is
publication), plus the run endpoint riding the record type's View* action.

## Phase 2 — Executor + API
- POST /api/views (create/update), GET /api/views?recordType=, DELETE own,
  and the heart: GET /api/views/{id}/run — typed rows shaped by the view's
  columns, built on the scoped sources from (c). Rows only; aggregation is
  D4's seam (leave a documented extension point, build nothing).
- Owner sees own + shared + system; only owner edits own; ManageSharedViews
  gates the IsShared flag.
- Tests: operator correctness per type (dates, money, enums), the scoping
  matrix (vendor runs a shared RFQ view → only invited RFQs; TechEvaluator
  runs a vendor-type view → 403 via View* check), registry validation
  (unknown FieldKey → 400 on save, loud error on run), and the
  drift-proofs: every registry row's DataType matches its DTO property
  (reflection test), anonymous sweep unchanged.

## Phase 3 — View builder UI + the proof screen
- ViewPicker on ListPage: system/shared/mine, default per screen.
- ViewBuilder (modal or panel per the Setup patterns): add criteria rows
  (field → operator → value, value inputs rendered by DataType through the
  D1 primitives — a date criterion gets DateField, a list criterion gets
  the select), choose/order columns, name, save, share (gated).
- The proof screen from (d) becomes saved-view-driven: its default view is
  a seeded SYSTEM view reproducing today's default list exactly (crawl
  green proves parity), its existing quick filters keep working (mapped
  onto the view mechanism or layered — report which).
- GATE, demonstrated with evidence in the report: as the buyer persona,
  build "Open RFQs closing this month" (or the (d)-equivalent) entirely in
  the UI, save, share; as a second internal persona, pick it and see the
  same rows; as the vendor persona, run a shared view and see only your
  own reachable records. Screenshots or crawl-step transcript.

## Phase 4 — Enforcement + docs
Adoption note in PRIMITIVES.md (ListPage screens consume views; new list
screens must not hand-roll filter persistence), DATA-MODEL.md gains the
four tables with grain declared, BACKLOG updated (aggregation seam → D4;
Requisitions migration row remains; vendor-portal view UI exposure → its
own row if not landed).

## Verification
Baselines from RM-P1 close (API 369, web 206, crawl 42) held or raised;
full crawl after the proof-screen switch; CI link per push; migration
Up→Down→Up proven; five-lens review — the CTO lens states what a "report"
now costs to create.

## Out of scope
Aggregation/metrics (D4); portlets (D4); custom fields/segments (D5/D6 —
registry enum members only); any change to Consolidate/BidForm; replacing
any list endpoint (the run endpoint is additive); scheduled/emailed views.

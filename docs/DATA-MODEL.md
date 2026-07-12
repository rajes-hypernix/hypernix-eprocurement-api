# DATA-MODEL.md — the analytics contract

**This document is a contract, not internal notes.** As of Slice H the data model is an
analytics substrate, and three readers depend on what this file says being true:

1. **The analytics / reporting layer** (D3 Saved Views, D4 metrics) — validates FieldKeys
   and builds aggregations against the grain, keys and dimension/fact split declared here.
2. **The NetSuite integration slices** (S3–S6) — map portal entities to NetSuite records
   along the lineage chain below; the named FKs are the join contract.
3. **The partner's engineering team** — inherits this as the description of record.

If you change a table's grain, key, an FK name, or a column's nullability, update this
file in the same commit. A report or integration built on a stale contract ships wrong
numbers silently.

## Conventions

- Every top-level entity has `Guid Id` (server-generated PK) and a human-readable `Code`
  from a gap-free server sequence. Money is `decimal` → Postgres `numeric(18,2)`.
- **Owned line collections carry their own stable `Guid Id`** (Slice H T1) — they are
  fact grain, not ordinal children. Their key is client-assigned (`ValueGeneratedNever`);
  see `docs/CONVENTIONS.md` → "Owned collection keys and ValueGeneratedNever".
- **Dates**: a business *date* is `DateOnly` (Postgres `date`); an *instant* is `DateTime`
  kind=UTC (`timestamptz`). Display localises (Malaysia = Asia/Kuching, UTC+8, no DST).
- Nine aggregate roots carry an `xmin` optimistic-concurrency token (Slice G T2):
  Rfq, PurchaseRequisition, Award, PurchaseOrder, Invoice, Asn, Grn, Vendor,
  VendorOnboardingApplication. A stale write → HTTP 409.
- "Reference-by-id, no nav" = a real FK column across an aggregate boundary with no EF
  navigation property, so aggregate boundaries stay clean.

---

## The lineage chain (canonical — drawn once)

The procurement spine, each hop with its FK column and nullability. This is the join path
for the three-way match (Sprint 5) and the SuiteAnalytics story.

```
PurchaseRequisition
  └─ PrLine (owned; stable Id since T1)
        ▲ PrLineSourcing.PrLineId  ──FK→ PrLines.Id            (T8; DEFERRABLE; NOT NULL)
PrLineSourcing  ──FK→ Rfq.Id  (RfqId, NOT NULL)                 ── the PR→RFQ demand link
Rfq
  └─ RfqLine (owned; stable Id)
Bid            ──FK→ Rfq.Id (RfqId), Vendor.Id (VendorId)       (one per (Rfq,Vendor))
Award          ──FK→ Rfq.Id (RfqId, unique: one award/RFQ)
  └─ AwardAllocation (owned; stable Id)  ──FK→ Vendor.Id (VendorId)
PurchaseOrder  ──FK→ Award.Id (AwardId, NULLABLE), Vendor.Id, Rfq.Id (RfqId NULLABLE)
  └─ PoLine (owned; stable Id).AwardAllocationId → AwardAllocation.Id   (T2; ref-by-id, NULLABLE)
Asn            ──FK→ PurchaseOrder.Id (PoId), Vendor.Id
  └─ AsnLine (owned; stable Id)
Grn            ──FK→ Asn.Id (AsnId, NOT NULL), PurchaseOrder.Id (PoId)
  └─ GrnLine (owned; stable Id)
Invoice        ──FK→ PurchaseOrder.Id (PoId), Grn.Id (GrnId NULLABLE), Vendor.Id
  └─ InvoiceLine (owned; stable Id)
```

**Legitimately-null hops (do not treat null as a data defect):**

- `PurchaseOrder.AwardId` — null for a PO not generated from an award (direct/legacy).
  `AwardCode` string retained for display (removal backlogged).
- `PoLine.AwardAllocationId` — null where the PO line predates T2 or wasn't cut 1:1 from
  an allocation.
- `PurchaseOrder.RfqId` — null for a non-RFQ PO.
- `Invoice.GrnId` — null when the invoice's PO has **zero or more than one** GRN
  (ambiguous receipt; T3 never guesses which).
- `RfqLine` has no FK back to a PR line — RFQ lines can be created directly, not only
  sourced; the PR→RFQ link is `PrLineSourcing`, not every RfqLine.

---

## Per-table reference — grain · key · dimensions · facts

Grain = what one row is. **Dim** = descriptive/grouping columns. **Fact** = measures.

### Suppliers

**Vendor** — grain: one vendor. Key: `Id` / `Code`.
- Dim: `Type` (SWEC/Non-SWEC), `Status`, `Region`, `State`, `City`, **`Country` (ISO-2
  code, Slice H T6 — conformed; label resolved for display)**, `Categories`, `LlrcTier`.
- Fact: `Rating`, `CreditLimit`. **Performance is NOT stored** — see VendorPerformanceView.
- `VendorAddress` (owned): `Country` also ISO-2 (T6).
- `VendorCertification` (owned): `ValidTo` is **free-text string, NOT a date** — bare
  years/placeholders ("2027", "—"). Excluded from T4 typing; not a reliable date
  dimension until remodelled (backlogged with the capture-form fix).

**VendorPerformanceView** (derived, Slice H T7 — keyless SQL view over facts, never stored):
- Grain: one vendor. Read-only.
- `Otd` — **always NULL** this slice: no promised-delivery date exists to measure against.
- `Breaches` — **always NULL**: a short receipt is not a modelled compliance breach.
- `LeadDays` — actual avg **days** `PurchaseOrder.IssuedUtc → Grn.ReceivedDate`; NULL with
  no qualifying pairs. Not the vendor's self-reported `Bid.Lead`.
- `Quality` / `Response` / `WinRate` — %, NULL when the denominator (receipts / invitations)
  is zero.
- `SpendYtd` — net (pre-tax) value of Paid invoices this calendar year. `Pos` — count of
  non-Draft POs.

### Sourcing

**PurchaseRequisition** — grain: one requisition. Key: `Id` / `Code`.
- Dim: `Department`/`DepartmentCode`, `Location`/`LocationCode`, `Category`/`CategoryCode`,
  `Job`/`JobCode` (free-text label + controlled code; the code columns have **no backing
  Custom List yet** — candidates for conformance, backlogged), `CostCentre`, `Project`.
- Date: `RaisedOn`, `RequiredOn` (`DateOnly`, T4 — the display strings were retired).
- Fact/instant: `SubmittedUtc` (T5).
- `PrLine` (owned): dim `ItemCode`; fact `Qty`, `EstUnitPrice`; `LifecycleStatus`.

**PrLineSourcing** — grain: one PR-line→RFQ sourcing link (append-only, never deleted).
Key: `Id`. FKs: `PrLineId`→PrLine (T8), `RfqId`→Rfq. `LinkStatus`, `QtySourced`,
`CreatedUtc`/`ClosedUtc`.

**Rfq** — grain: one RFQ. Key: `Id` / `Code`.
- Dim: `Envelope`, `Status`, `OwnerUserId`.
- Planned instant: `ClosesUtc` (planned deadline), `OriginalClosesUtc` (baseline at
  release; extension analytics). **Actual instants (T5):** `ReleasedUtc`, `ClosedUtc`
  (early close — never overwrites the planned `ClosesUtc`), `AwardedUtc`.
- `RfqLine`, `RfqInvitation` (`InvitedUtc`/`RespondedUtc`, `Status`), `RfqEvent`
  (append-only typed lifecycle log: `EventType`, `OccurredUtc`).

**Bid** — grain: one (Rfq,Vendor). `Submitted`, `SubmittedUtc`. Owned: `BidLine`,
`BidAnswer`, `BidAttachment` (all stable Id).

**Award** — grain: one award (unique per RFQ). Instant: `ApprovedUtc`. `TotalValue` is
**derived** from allocations, not stored (DBA-10). Owned: `AwardAllocation`.

### Procure-to-Pay

**PurchaseOrder** — grain: one PO. Dim: `Status`, `Incoterm`, `Currency`. Lineage:
`AwardId` (T2), `AwardCode` (display). Instants (T5): `IssuedUtc`, `AcknowledgedUtc`.
Owned: `PoLine` (`AwardAllocationId` lineage; `ReceivedQty`/`InvoicedQty` facts).

**Asn** — grain: one shipment. Dates (T4): `ShippedDate`, `ExpectedDate` (`DateOnly`).
Instant (T5): `ReceivedUtc`. Owned: `AsnLine`.

**Grn** — grain: one goods receipt. Date (T4): `ReceivedDate` (`DateOnly`, Malaysia
business day). Owned: `GrnLine` (`Condition` Good/Short — the Quality/Breaches source).

**Invoice** — grain: one invoice. Date (T4): `Date` (`DateOnly`). Instants (T5):
`SubmittedUtc`, `ApprovedUtc`. Lineage: `GrnId` (T3). `Total`/`Subtotal`/`Sst`/`Wht`
are **derived**, not stored. Owned: `InvoiceLine`.

**PaymentVoucher** — grain: one voucher (NetSuite-side, placeholder this build). Not yet
a fact source.

**StoredFile** — typed ownership (Slice G T5): `OwnerKind` (Bid/OnboardingDocument/
OnboardingAnswer/Internal), `OwnerVendorId`, `OwnerEntityId` — download scoping reads
the column, not inference.

**AuditEntry** (append-only) — `EntityType`, `EntityId` (the Code), `Action`, typed
`FromState`/`ToState` (via `WriteTransitionAsync`) or generic `Before`/`After`,
`UtcTimestamp`. Never updated or deleted.

**Statement / SOA** — derived (not stored): per-vendor running ledger over POs, GRNs and
invoices, aging buckets, GRNI accrual.

---

## Known-null ranges & trust boundaries (read before writing a report)

The substrate is **trustworthy going forward, honest about the past.** A report that
ignores this section will silently drop or misstate history.

**Transition timestamps (T5)** — stamped inside the domain methods from the Slice H
deployment (2026-07) onward. Backfilled onto historical rows ONLY from clean sources:
- Populated on history: `Rfq.ReleasedUtc` / `ClosedUtc` (from `RfqEvent`),
  `PurchaseRequisition.SubmittedUtc` (from the typed `AuditEntry`), `Award.ApprovedUtc`
  (pre-existing).
- **NULL on pre-slice history** (no clean source; not fabricated): `Rfq.AwardedUtc`,
  `PurchaseOrder.IssuedUtc` / `AcknowledgedUtc`, `Invoice.SubmittedUtc` / `ApprovedUtc`,
  `Asn.ReceivedUtc`. These become reliable for transitions occurring **after the Slice H
  deploy**. A cycle-time chart (e.g. PR→PO days) must filter to post-deploy records or
  annotate the gap — it cannot assume these are populated before 2026-07.

**Derived performance (T7)** — `VendorPerformanceView`:
- `Otd`, `Breaches` — null pending unblockers (a promised-delivery date; a real breach
  register). Backlogged.
- `LeadDays` — null until POs carry `IssuedUtc` (i.e. issued after the T5 deploy); it
  **self-activates** as real POs flow. Do not read "no lead data" as "instant delivery".
- `Quality` / `Response` / `WinRate` — null (not 0) when a vendor has no receipts /
  invitations in the window (rolling 12 months / current year, `CURRENT_DATE`-relative).

**Excluded from typing** — `VendorCertification.ValidTo` (see Suppliers): free-text,
not a date dimension.

---

## Enforcement

The contract is defended by executable tests, not just prose:
- Foreign keys (incl. PrLineSourcing→PrLine, T8) — Postgres-backed integrity tests.
- Owned-line stable keys — `StableLineKeysTests`.
- The golden status constraint (transitions only via guarded domain methods) —
  `ArchitectureTests` source-scan.
- Each backfill (Award→PO, Invoice→GRN, transition timestamps, country conformance) —
  a Postgres-backed test over the shared statements the migration runs.

## Saved views engine (D3)

Four typed tables — the shared query-definition layer (list screens now; D4
portlets/reminders/KPIs consume the same engine). No filter blobs anywhere.

- **FieldRegistry** — grain: one row per queryable field per record type
  (`RecordType` + `FieldKey` unique). `FieldKey` = the LIST DTO property name
  (PascalCase). `Kind` ∈ {Native, Custom, Segment}: all three are live —
  Custom rows arrive with D5 defs (CustomFieldDefId set), Segment rows with
  D6 applications (SegmentDefId set); the slices added rows, never reshaped
  (as designed at D3). Seeded from
  `Application/Views/FieldRegistrySeed.cs` (THE single source: migration loop,
  test seeding, and the reflection drift-test that pins every row's DataType
  to its DTO property type all read it). 67 native rows at D3.
- **SavedView** — grain: one row per view. `Code` from the VIEW sequence
  (system seeds carry literal codes, e.g. VIEW-SYS-0001 "All RFQs").
  `OwnerUserId` null for system views; `IsShared` is publication (flipped only
  via the A61-gated share endpoint); `IsSystem` views are read-only.
- **SavedViewFilter** — grain: one row per criterion member. Typed columns
  FieldKey/Operator/Value/Value2 (Value2 only for Between). Composition rule:
  same-FieldKey Eq/In rows OR (membership, mirroring the facets the operator
  set was derived from); everything else ANDs. Values may be the three ruled
  relative-date tokens (@today/@startOfMonth/@endOfMonth), resolved at run.
- **SavedViewColumn** — grain: one row per output column position
  (FieldKey, optional Label override, Sort, optional SortDirection).

Field keys are validated against the registry on save AND on execute — a view
referencing a dead key fails loudly (400), never silently drops the filter.
The executor decorates the SAME scoped service list methods the screens use
(vendor scoping/masking inherited by construction); aggregation is D4's
documented seam — deliberately not built.

## Dashboards + portlets (D4)

- **Dashboard** — grain: one row per dashboard. Exactly ONE owner axis, enforced
  by a DB check constraint: a ROLE DEFAULT (`OwnerRole` set + `IsRoleDefault`,
  Admin-managed via A64) XOR a user's personalized copy (`OwnerUserId` set —
  copy-on-write from the resolved role-default union; multi-role principals get
  the deduplicated union of their roles' defaults, stacked). Six role defaults
  seeded from `Application/Dashboards/DashboardSeed.cs` (single source shared
  with tests; deterministic ids).
- **PortletInstance** — grain: one row per placement. `PortletType` ∈ {KpiMeter,
  KpiScorecard, Reminders, SavedViewList, Shortcuts, RecentRecords, Chart,
  MyInvitations (OD-D4-2)}; `Col/Row/Width` = the 2-column arrange grid;
  `SavedViewId` nullable FK for view-backed portlets. **`ConfigJson` is the
  framework's ONE sanctioned JSON** — its schema per portlet type is typed in
  `Application/Dashboards/PortletConfigs.cs` and validated on every save (and by
  the seed drift-test); nothing else on any business entity stores JSON.
- **Metric layer** — no tables: `SystemMetricService` computes from existing
  facts, IClock-driven (never DB CURRENT_DATE), each metric carrying a
  RequiredAction and its null behaviour (backfill-null inputs → "not yet
  available", never zero — the Slice H posture). View aggregation
  (`/aggregate`, `/series`) rides the D3 executor's scoped pipeline; the
  IQueryable escape hatch remains the documented seam in SavedViewService.

## Custom fields (D5)

- **CustomFieldDef** — grain: one row per definition. `Code` (`cf_*`, UQ) doubles
  as the FieldRegistry FieldKey and the FieldSpec key — one identity across
  storage, filtering and rendering. Code/RecordType/DataType are IMMUTABLE;
  def creation inserts the Kind=Custom registry row in the same transaction.
  Lifecycle (ruled): values ever written → deactivate-only forever (values
  persist; entry surfaces and the builder palette hide it; referencing views
  fail loudly); zero values → hard-delete removes the registry row.
- **CustomFieldValue** — grain: one row per (def, record); UQ(FieldDefId,
  RecordId); **an absent row IS the honest null** (aggregates count it as
  ExcludedNullCount, never zero). **Sparse-column deviation, ruled:** SIX typed
  columns serve the EIGHT DataTypes — Text+LongText share `ValueText` (length
  is UI semantics, not storage) and Int+Decimal share `ValueNumber
  numeric(18,4)` (wholeness is a save-time rule); plus `ValueMoney
  numeric(18,2)`, `ValueDate date`, `ValueBool`, `ValueListCode` (the
  CustomListValue CODE, like built-in selects). Type-safety is preserved
  entirely by two CHECKs: exactly-one-column-populated AND
  populated-column-matches-the-DataType (denormalized from the immutable def).
  No JSON values anywhere.
- **Polymorphic (RecordType, RecordId)** — the accepted cost: no owning
  aggregate hard-deletes today, so the orphan integrity probe
  (`CustomFieldOrphanTests`) is the standing guard; any future delete flow
  that strands values turns it red and inherits the cleanup obligation.
- Deferred (BACKLOG): RecordRef (first honest consumer is L4 custom records),
  DateTime (every user-entered business date is DateOnly per Slice H).

## Custom segments (D6)

Four typed tables — the DIMENSION engine. A segment is a conformed reporting
dimension: named values (keys), applied per record type, assigned per record
(or per line), sliced in any view/KPI/series through the same registry
machinery as every other field.

- **SegmentDef** — grain: one row per dimension. `Code` (`seg_*`, UQ) is the
  FieldRegistry FieldKey wherever the segment is applied. `IsSystem` marks the
  four PR-dimension mirrors (below) — read-only via A68 (the convergence
  BACKLOG row owns changes to them). `HasHierarchy` stores intent;
  `SegmentValue.ParentValueId` stores the tree flat-with-parent (ruled) —
  rollup reporting is BACKLOG, gate-driven.
- **SegmentValue** — grain: one row per dimension KEY. `Code` derives from the
  label via `SourcingMapping.DimCode` — the SAME derivation the PR's dimension
  columns use, so codes are dimension-keys-by-construction (the ruled (iii-a)
  condition 1). UQ(SegmentDefId, Code).
- **SegmentApplication** — grain: one row per (def, record type). `LineLevel`
  opts a type into per-line assignment (PO lines are the one line-level proof
  surface this slice). Applying inserts the Kind=Segment registry row in the
  same transaction (the D5 lockstep) — USER segments only; system segments
  deliberately carry no registry rows this slice (PR views already filter on
  the native columns — one key, one field, no rival). Un-applying with live
  assignments is refused: dimension keys are never silently dropped.
- **SegmentAssignment** — grain: one row per (def, record[, line]);
  UQ(SegmentDefId, RecordType, RecordId, LineId). **An absent row IS the
  honest null** — group-by surfaces it as the NAMED `Unassigned` bucket
  (key `__unassigned`) on every slice, never a dropped record.
- **The four system segments (ruled iii-a):** Department/Location/Category/Job
  remain COLUMNS on PurchaseRequisitions — the single truth — and project
  one-way into segment assignments via `SegmentProjection`, called from BOTH
  RequisitionService write paths and the seeder. The Postgres-backed probe
  (`SegmentProjectionProbeTests`) pins column ≡ assignment, so a future third
  write path that forgets the hook goes red. Direct assignment writes on PRs
  are refused ("edit the PR"). Deterministic ids: `md5(text)::uuid` in SQL ≡
  `SegmentSeed.HexGuid` in C# (hex-string parse, NOT the mixed-endian
  byte-array Guid ctor).
- Group-by (`/aggregate?groupBy=`, `/series?groupBy=`) is part of the D3/D4
  executor pipeline — same visibility, same scoped sources; a grouped
  aggregate reconciles to its total by construction (FoldGroup).

## Entry forms + numbering (D7)

Four typed tables — the last framework engine: forms carry entry BEHAVIOUR
(which fields, in what groups/subtabs, display type, required-at-submit,
defaults, sourcing) per role; numbering becomes Setup configuration over the
Slice G generator. Distinct from `FormTemplates` (RFQ bid questionnaires) —
"Entry Forms" everywhere, including on the glass.

- **EntryFormDef** — grain: one row per form. `Code` (`ef_*`, UQ) derives
  from the name (the cf_/seg_ discipline). `IsSystem` marks the seeded
  Standard forms — read-only (the segments precedent); the composer's "New
  form" copies one. `RecordType` restricted to Requisition this slice
  (OD-D7-5: a definition with no consuming surface is a dummy); each entry
  surface's migration gate widens it (BACKLOG).
- **EntryFormField** — grain: one row per (form, field); UQ(FormDefId,
  FieldKey). `FieldKey` addresses the FieldRegistry (all three Kinds) with
  **NO FK** — liveness is service-validated on save AND at resolve, failing
  LOUD (an FK would silently convert D5's ruled zero-value hard-delete into
  blocked deletes). Placeable native keys are derived MECHANICALLY from the
  write contract (`EntryFormVocabulary`): a key the save DTO doesn't carry
  (Code, HeaderStatus, Value, CostCentre, Project) is refused. `Subtab` is a
  pure layout container holding FIELDS only (sublists keep their built-in
  homes — custom sublists are the recorded L4 boundary). `FullWidth`/
  `Label`/`Placeholder` are census-derived render overrides (OD-D7-4 —
  byte-identical parity needs "Job / Cost ref", not the registry's "Job").
  `DefaultValue` on date fields accepts the shared DateTokens grammar
  (@today, @startOfMonth, @endOfMonth, @today±Nd), resolved server-side at
  resolve time, applied to NEW records only.
- **EntryFormRoleMap** — grain: one row per (record type, role);
  UQ(RecordType, Role). Resolution follows the FIXED GLOBAL ROLE PRECEDENCE
  (ruled MODIFIED: never the user record's array order) — Buyer, Approver,
  TechEvaluator, CommEvaluator, Admin, Vendor — first role the caller holds
  with an Active mapped form wins; Standard fallback. The server RE-RESOLVES
  at submit (OD-D7-2): the caller can never name a form.
- **NumberingScheme** — grain: one row per record type (UQ), the seven
  registry types seeded reproducing today's formats verbatim (incl. SWK-V,
  VOB). Consulted at FORMAT time only — the gap-free FOR-UPDATE sequence
  upsert is untouched, sequences are keyed (prefix, bucket) and NEVER reset:
  a prefix change starts (or reattaches to) its own counter, digits changes
  continue the same counter, and YearSegment=false buckets under year 0 so
  year-less codes cannot collide across years — uniqueness by construction,
  all four cases pinned (NumberingTests). System-artifact prefixes (GRN,
  BID, AWD, VU, USR, FORM, DASH, VIEW) stay literal (BACKLOG row).
- The seeded **Standard PR Form** reproduces PrForm's retired hardcoded
  HEADER_SECTION exactly (EntryFormSeed = the single source; parity pinned
  seed-side in EntryFormSeedTests and render-side in the web parity test).

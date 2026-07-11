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

# Data Model — Analytics-Readiness Standard

> Long-term goal: a built-in analytics layer like NetSuite SuiteAnalytics (dimensional
> slice/dice, drill-down, cycle-time, spend analysis). That layer is only as good as the OLTP
> schema we commit now. Build the schema as if a star-schema/ELT projection will be layered on
> top later with **zero refactor of business tables**. Normalize now; shape in analytics views
> later. Do not build the star schema yet — build a schema that makes it mechanical.

These principles are **binding** for every table this module adds or touches.

## 1. Append-only event/lineage backbone — never destroy history
- **No hard deletes** of business facts. Cancellation/return is a **state + timestamp**, not a
  `DELETE`. `PrLineSourcing` rows are never deleted — `LinkStatus` + `ClosedUtc` + `Reason`.
- State transitions are **recorded, not just applied.** Beyond mutating `PrLineStatus`, persist
  each transition (via the existing `AuditEntry` with **typed columns** — entity type, entity
  id, from-state, to-state, reason, actor, `OccurredUtc` — not a free-text blob). This is what
  makes cycle-time analytics ("PR submitted → sourced → awarded in N days") possible. If
  `AuditEntry` is currently free-text, add typed columns; do not lose the structured signal.

## 2. Lineage is the spine of spend analytics — keep every hop durable
The full chain must be traceable end to end by stable IDs:
`PR line → (PrLineSourcing) → RFQ line → (award allocation) → PO line → GRN line → Invoice line`.
Every hop is a durable link row keyed by surrogate IDs. Any PO must be traceable back to its
originating PR line, and any PR line forward to its spend. Merged RFQ lines keep **one link per
source PR line** so consolidation never collapses provenance.

## 3. Keys: surrogate + business, both stable and immutable
- **Surrogate PK = GUID**, system-generated, never reused, never meaningful.
- **Business key = human Code ID** (PR-2026-0412, RFQ line code), server-sequence generated,
  **immutable once assigned**, never recycled. Codes are display/reference; never join on them
  internally — join on the GUID.

## 4. Conformed dimensions — controlled, not free-text  ⚠ current gap
Analytics requires that you can group reliably by Department, Category, Location,
Job/Cost-centre, Requestor, Vendor, Item, UoM. Free-text strings ("Maintenance" vs
"maintenance " vs "Maint.") destroy grouping.
- **SWEC Category is already a proper hierarchy entity (`SwecCategory`)** — extend that
  discipline to the others.
- PR/RFQ lines should carry a **stable dimension reference (FK or controlled code)** for
  Department, Category, Location, Job/Cost-centre — not raw free text. Where a full reference
  entity is overkill for v1, use a **controlled code + display label** pair, with the code as
  the analytics grain.
- **Do this additively:** keep any existing display field, add the typed/coded field as the new
  source of truth, backfill seeds. (See §7 for the current free-text fields to upgrade.)

## 5. Dates and times: typed and UTC — never strings  ⚠ current gap
- All system timestamps: `DateTimeOffset`/UTC (`CreatedUtc`, `UpdatedUtc`, transition times).
  Set via the injected `IClock`, never `DateTime.Now`.
- **Business dates** (RaisedDate, RequiredDate) must be **real date types** (`DateOnly`), not
  `"dd/MM/yyyy"` strings. String dates cannot be range-filtered, bucketed, or used for
  on-time/lead-time analytics. Store typed; format for display in the UI only.

## 6. Money: decimal + currency + as-of base amount
- Money is `decimal` (EF Core 10) — never float/double.
- Every monetary amount carries its **currency code**. For cross-currency spend analytics,
  capture a **base-currency-equivalent snapshot at transaction time** (amount + rate as-of), so
  historical reports don't drift when FX changes. Vendor master already supports multiple
  currencies — be consistent.
- **Derived totals are derived, not stored** unless there is a measured need; PR/RFQ totals roll
  up from lines deterministically. Don't persist aggregates that can drift from their lines.

## 7. Concrete upgrades to the CURRENT model (do additively in Slice A)
The existing `PurchaseRequisition`/`PrLine` carry analytics-hostile shapes. Fix as part of the
clean foundation, **without breaking existing readers/seed**:
| Field today | Problem | Fix |
|---|---|---|
| `PrLine` (no Id) | no grain key for facts/lineage | add `Guid Id` (REQUIRED) |
| `RaisedDate`/`RequiredDate : string "dd/MM/yyyy"` | not range-queryable | add `DateOnly` source-of-truth fields; backfill; UI formats |
| `Department`/`Category`/`Location`/`Job : string` | not groupable | carry a controlled code/FK as the analytics grain (additive) |
| `Status : string "Approved"` (free) | unstable label | use the typed lifecycle enum; keep stable stored values |
| `Value : decimal` (stored on header) | can drift from lines | derive from lines; attach currency |
| `AuditEntry` free-text (if so) | unstructured | typed columns: entity, id, from→to, reason, actor, OccurredUtc |

## 8. Grain — declare it for every fact/link table
One sentence per table stating its grain, in the entity XML doc-comment:
- `PrLine` — one requisition line (the demand grain).
- `PrLineSourcing` — one (PR line → RFQ line) sourcing event.
- award allocation — one (RFQ line → vendor) award decision.
Unambiguous grain = unambiguous joins and no double-counting in analytics.

## 9. Enums stored as stable tokens, not display strings
Persist enums as int or a fixed string token; map to human labels in the UI/lookup. Renaming a
label must never rewrite history.

## 10. Indexing for the obvious analytics filters (light, not premature)
Index FKs and the columns analytics will filter/group on: dates, dept/category/location codes,
status, vendor, PR/RFQ ids. Add as part of the migration; don't over-engineer beyond the known
query shapes.

## 11. Keep OLTP and analytics concerns separate
Don't denormalize business tables for reporting convenience. A future analytics schema (views,
materialized views, or an ELT projection into a star) reads from these clean tables. The job
now is clean, normalized, fully-keyed, append-only facts + conformed dimensions — nothing more.

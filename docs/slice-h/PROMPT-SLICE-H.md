# PROMPT — SLICE H: Analytics Substrate

Read README-FIRST.md first. This closes the audit's analytics findings
(AN-1..AN-7, DBA-5, DBA-10) and unblocks Payment Vouchers, NetSuite sync,
and the SPSB reporting roadmap.

## Step 0 — Report and WAIT
File plan per task. Plus three discovery answers with file:line evidence:
(a) Which web components consume the string date fields (Invoice.Date,
    Asn.ShippedDate/ExpectedDate, Grn.ReceivedDate, PR.RaisedDate/
    RequiredDate, VendorCertification.ValidTo)? Enumerate every one — T4
    changes their DTO shape.
(b) Where is VendorPerformance written today, and is anything reading it
    besides the vendor detail screen?
(c) Do RfqEvent / AuditEntry already carry enough to reconstruct transition
    times for PR and PO, or is T5 adding genuinely new columns?

## T1 — Stable line keys (DBA-5)
`PrLine` already has `public Guid Id` (the precedent — follow it exactly).
These owned line types have NO stable key and are addressed by ordinal
position, which makes them unusable as fact-table grain:
  RfqLine, BidLine, BidAnswer, BidAttachment, PoLine, AsnLine, GrnLine,
  InvoiceLine, AwardAllocation
Add `public Guid Id { get; private set; } = Guid.NewGuid();` to each, and
configure it as the key in AppDbContext (OwnsMany ... HasKey).

Migration `StableLineKeys`. Existing rows get generated Guids in the Up().
Report the row count keyed per table. Down() drops the columns and restores
the prior key configuration.

CAUTION: EF owned-collection key changes can silently reorder or re-create
rows. Prove with a before/after row-count and spot-check that line contents
and their parent associations are unchanged. If any table's rows would be
destroyed and recreated, STOP and report before proceeding.

## T2 — Award → PO lineage (AN-2)
`PurchaseOrder.AwardCode` is a `string?` joined by code. Replace with a real
relationship:
- Add `AwardId` (Guid?, FK → Award, OnDelete Restrict, no nav property).
- Backfill from AwardCode by matching Award.Code. Report matched/unmatched
  counts. Unmatched rows keep AwardId null and are listed in the report.
- Keep AwardCode as a display field for now (do NOT drop it this slice) —
  removing it is a DTO/web contract change. Backlog its removal.
- Add `AwardAllocationId` (Guid?) on PoLine linking each PO line back to the
  allocation it came from, if PoService can determine it at creation time.
  If it cannot without guesswork, STOP and report — do not infer.

Migration `AwardPoLineage`.

## T3 — Invoice → GRN lineage (Slice G carry-over, backlogged)
Add `Invoice.GrnId` (Guid?, FK → Grn, Restrict). Populate on creation where
the invoice is raised against a receipted PO. Backfill existing rows only
where a single unambiguous GRN exists for the invoice's PO; leave ambiguous
ones null and report the count. This completes the three-way-match lineage
(PO · receipt · invoice) the roadmap's Sprint 5 depends on.
Close the BACKLOG row.

## T4 — Typed dates (AN-1, AN-6) — the one slice permitted to touch web
Convert these string date fields to typed columns (`DateOnly` where a date,
`DateTime` with `timestamptz` where an instant):
  Invoice.Date, Asn.ShippedDate, Asn.ExpectedDate, Grn.ReceivedDate,
  PurchaseRequisition.RaisedDate, PurchaseRequisition.RequiredDate,
  VendorCertification.ValidTo (and ValidFrom if present)

- Migration `TypedDates` parses existing `dd/MM/yyyy` strings. Any row that
  fails to parse: STOP and report; do not default it to a date.
- Kill the `AddHours(8)` Malaysia-timezone hack in DeliveryService. Store
  UTC instants; the display layer localises. State how the UI now derives
  Malaysia local time.
- DTOs expose ISO-8601. Update every web consumer found in Step 0(a) — the
  existing display format must not change on screen.
- The expiring-certifications capability now becomes possible; do NOT build
  the report, just note it.

This is the highest-risk task. Commit it alone. Crawl must be 42/42 after.

## T5 — Transition timestamps (AN-3)
Cycle-time reporting (PR→PO days, RFQ→award days) is the single most-demanded
procurement KPI family and is currently unanswerable.
- Add typed UTC columns for the transitions the domain already performs:
  e.g. PR SubmittedUtc/ApprovedUtc, RFQ ReleasedUtc/ClosedUtc/AwardedUtc,
  Award ApprovedUtc, PO IssuedUtc/AcknowledgedUtc, Invoice ReceivedUtc.
  Derive the exact set from the existing domain transition methods (Slice G
  gave every aggregate one) — one column per transition that already exists.
  Do not invent transitions.
- Stamp them inside the existing domain methods via IClock.
- RFQ: `ClosesUtc` is the PLANNED close. Early close must set `ClosedUtc`
  (actual) and never overwrite the planned value. Verify Slice I's
  OriginalClosesUtc discipline is preserved.
- Backfill from RfqEvent / AuditEntry where the record exists; leave null
  where it does not, and report the null counts per column. A null means
  "we don't know", never "now".

Migration `TransitionTimestamps`.

## T6 — Conformed vocabulary (AN-5, AN-7)
`Vendor.Country` and the address Country both default to "Malaysia" as free
text, while data elsewhere carries "MY" — a GROUP BY splits the same country
into two buckets.
- Normalize country to the controlled Custom List code (ISO-2), with the
  label resolved for display. Enforce at the service boundary.
- Same treatment for any free-text dimension that has a Custom List (report
  which: department, location, category codes).
- Migration `ConformedVocabulary` normalizes existing rows. Report the
  before/after distinct-value counts per column — that delta IS the finding.
- Do NOT change what the user sees. Labels render as before.

## T7 — VendorPerformance as derived (AN-4, DBA-10)
`VendorPerformance` stores Otd, Quality, Breaches, Lead, Response, WinRate,
SpendYtd as ints/decimals that can drift from the facts.
- Replace the stored aggregate with a SQL view (or a computed read-model
  query) over GRN, Invoice, RfqInvitation and Award facts.
- Where a metric cannot yet be computed from real facts, return null and say
  so in the DTO — never a stale stored number and never a fabricated one.
  Report exactly which metrics are computable today and which are not.
- The vendor detail screen must render identically for computable metrics;
  non-computable ones show an explicit "not yet available" state.
Migration `VendorPerformanceDerived`, Down() restores the table and backfills
from the view.

## T8 — Docs
- Close the BACKLOG rows: Invoice.GrnId (T3), PrLineSourcing→PR FK (now
  possible after T1 — add that FK too, or state why not), AddHours(8) (T4).
- Update docs/DATA-MODEL.md: declared grain per table, key per line table,
  the lineage chain PR→RFQ→Award→PO→GRN→Invoice, and which columns are
  dimensions vs facts. This document becomes the analytics contract.
- Backlog: AwardCode removal (T2), any metric T7 could not compute.

## Verification
Full API suite (279 baseline + new), web vitest 112+, tsc/oxlint clean,
crawl 42/42, CI green on push (report the run URL).
Per migration: Up→Down→Up on a freshly seeded DB, with row counts.
Final report: per-task files, migration proofs, the T4 date-parse results,
T6's distinct-value deltas, T7's computable/non-computable split, and
anything observed but not fixed.

## Out of scope
Payment Vouchers; Contracts/ACV; NetSuite; role-matrix authorization; the
design system; object storage for StoredFile.Content; building any report or
dashboard on the new substrate (the demo dashboard's hardcoded data stays
until a later slice); AwardCode column removal.

# PROMPT — SLICE G: Data Integrity & Domain Enforcement

Read README-FIRST.md first. This is the last hardening slice; after it, the
platform layer is production-grade and Slice H (analytics substrate) unlocks
Payment Vouchers and the NetSuite integration work.

## Step 0 — Report and WAIT
List every file you intend to touch, grouped by task. Answer two discovery
questions with file:line evidence:
(a) Which entities' Status properties are read (not written) outside their
    own aggregate? (Determines whether a private setter breaks anything.)
(b) Does the existing DbUpdateConcurrencyException surface anywhere today, or
    is 409-on-conflict entirely new plumbing?

## T1 — Foreign keys across the business tables (DBA-1)
Today only 7 FKs exist (Rfq→Invitations, Bid→Vendor, Award→Rfq, onboarding,
custom lists). The P2P chain has none — orphaned financial rows are possible.

Add FKs with `OnDelete(DeleteBehavior.Restrict)` and NO navigation properties
(preserve aggregate boundaries) for at least:
- PurchaseOrder → Vendor, PurchaseOrder → Rfq (nullable)
- Asn → PurchaseOrder, Asn → Vendor
- Grn → PurchaseOrder, Grn → Asn (nullable)
- Invoice → PurchaseOrder, Invoice → Vendor, Invoice → Grn (nullable)
- Bid → Rfq
- Award → Rfq (verify existing), AwardAllocation → Vendor
- PrLineSourcing → PurchaseRequisition, → Rfq
- StoredFile references (after T5)
Enumerate the full set from the model yourself and report it in Step 0 — the
list above is a floor, not a ceiling.

Migration `ForeignKeyIntegrity`. Before adding constraints, the Up() must
detect pre-existing orphans in the dev seed: if any exist, STOP and report
rather than silently failing the migration in production.
Tests: an orphaned insert throws; a legitimate insert succeeds.

## T2 — Optimistic concurrency (xmin) + 409 mapping
Zero concurrency tokens exist today. Two buyers extending an RFQ, or one
extending while another closes, is currently last-write-wins.

- Add a PostgreSQL `xmin` concurrency token to the aggregate roots that carry
  a lifecycle: Rfq, PurchaseRequisition, Award, PurchaseOrder, Invoice, Asn,
  Grn, Vendor, VendorOnboardingApplication.
  (EF Core: `e.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin")`.)
- Map `DbUpdateConcurrencyException` → HTTP 409 in ExceptionMiddleware,
  following the existing DomainRuleException → 409 convention but with a
  distinct title ("Concurrent modification") and a message telling the user
  to reload. Do not swallow it; do not retry automatically.
- Test: two contexts load the same Rfq, both extend, the second SaveChanges
  throws and the middleware yields 409.
Note in the report: this closes RFQ-LIFECYCLE-ADDENDUM E11 (extend/close race).

## T3 — Kill the 17 direct `.Status =` writes (ARC-1)
The golden constraint says transitions go through domain methods. Seventeen
service-layer assignments violate it, across eight files:
RfqService (3), DeliveryService (2), InvoiceService (3), VendorService (1),
AwardService (3), RequisitionService (1), PoService (2), EvaluationService (2).

For each aggregate, add domain transition methods with guards that throw
`DomainRuleException` on illegal transitions, mirroring the RfqInvitation
pattern from Slice I. Make the Status setter **private** on: Rfq,
PurchaseRequisition, Award, PurchaseOrder, Invoice, Asn, Vendor.

Preserve exact current behaviour — where a service today performs a
conditional transition (e.g. EvaluationService only moves Closed→Evaluation),
the guard must encode precisely that condition, not a stricter one. Any
transition the current code permits must remain permitted. Where the current
code would permit something clearly wrong, REPORT it — do not silently
tighten it in this slice.

Note: `RequisitionService.cs:149` assigns a **string** `"Cancelled"` — PR
status is stringly-typed. Convert to a proper enum as part of this task if it
is contained; if it fans out beyond the PR aggregate, report and defer.

## T4 — Architecture test (the constraint becomes executable)
Add a test that fails the build if the pattern recurs: reflect over the domain
assembly and assert that no aggregate root exposes a public `Status` setter.
Add a second test asserting no type in eProcure.Infrastructure.Services
assigns to a Status property (Roslyn analyzer, or a source-scan test over the
service files — either is acceptable; state which and why).
This test is the deliverable, not the 17 fixes: it is what stops drift.

## T5 — StoredFile typed ownership (retires Slice F's inference)
`StoredFile` has no owner column; Slice F's FileAccessPolicy scans
`"<fileId>::<name>"` answer values and OnboardingDocument.StoredFileId.

- Add to StoredFile: `OwnerKind` (enum: Bid, OnboardingDocument, OnboardingAnswer,
  Internal), `OwnerVendorId` (Guid?, null for Internal), and a nullable
  `OwnerEntityId` (Guid?) pointing at the owning record.
- Backfill in the migration from the existing inference logic (bid answer
  values + onboarding documents). Report row counts backfilled and any file
  that cannot be attributed — those become `Internal` and stay buyer/admin-only.
- Rewrite FileAccessPolicy to use the typed column. It must remain
  fail-closed: unknown/unattributable → 403 for vendors.
- The Slice F integration tests (VendorScopingTests) must pass UNCHANGED —
  that is the proof the perimeter didn't move.
Migration `StoredFileOwnership`, with a Down() that drops the columns.

## T6 — CodeGenerator race (PRG-2)
`CodeGenerator.NextAsync` does read → increment → SaveChanges with no locking;
two concurrent requests can produce the same code. Replace with a database-
level guarantee: a PostgreSQL sequence per (prefix, year), or
`SELECT ... FOR UPDATE` on the NumberSequence row inside a transaction.
State which you chose and why. Codes must remain `{PREFIX}-{YEAR}-{0000}`,
gap-free per type is NOT required (state if your approach permits gaps —
sequences do; that is acceptable and should be noted in the report).
Test: N concurrent NextAsync calls produce N distinct codes.

## T7 — Backlog + register upkeep
- Close the StoredFile ownership row in docs/BACKLOG.md (T5).
- Close the extend/close race note in docs/PERMISSIONS-REGISTER.md (T2).
- Update PERMISSIONS-REGISTER: the role-matrix pass is now the only
  authorization item outstanding; state the enumerated action count.

## Verification
- Full API suite (270 baseline + new), web vitest 112, tsc/oxlint clean.
- e2e-audit crawl 42/42 — the behaviour-preservation proof.
- Each migration: Up → Down → Up on a freshly seeded dev DB; report row
  counts and prove Down() restores data where derivable.
- Final report: per-task files, migration proofs, test counts vs baselines,
  the T3 behaviour-preservation notes (any transition you found questionable
  but did NOT change), and anything observed-but-not-fixed.

## Out of scope — do not touch
Anything in Slice H (line Guid keys, Award→PO AwardId lineage, transition
timestamps, typed dates, the AddHours(8) timezone offset, vocabulary dedup,
VendorPerformance); role-matrix authorization; object storage for files
(StoredFile.Content stays a byte[] this slice — note it, don't move it);
NetSuite; Payments; Contracts; any UI change.

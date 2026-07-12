# FULL-TEST-REPORT — Phase 1 regression sweep (LONGRUN)

**Verdict: 69/69 inventory capabilities have passing evidence** (each box in
`FULL-TEST-INVENTORY.md` now carries its exact test names — that file IS the
capability→test map; this report is the summary layer). Suites at sweep close:
**API 478/478 · web 234/234 · e2e 65/65** (two consecutive full-suite greens).
Zero silent gaps: the two capabilities that cannot be tested are BLOCKERS entries
with reasons (B2 per-role numbering — feature absent by design; B3 Paid transition —
payments deliberately stubbed, downstream consumption tested).

## Coverage by PART (capability → evidence density)

| PART | Boxes | Coverage shape |
|---|---|---|
| 1 Auth & perimeter | 8/8 | Anonymous sweep + demo hard-block + explicit dev-404 (NEW) + file ownership + action sweep + metric AUTHZ |
| 2 Role matrix | 9/9 | RoleMatrixTests theory over the full ActionCatalog + scoping suites + search filter |
| 3 Sourcing | 10/10 | Service tests per lifecycle stage + one-award pin (NEW) + consolidation vitest×13 + e2e legs |
| 4 Procure-to-pay | 5/5 | Delivery/Invoice/Statement suites; Paid → B3; payments placeholder crawled |
| 5 Onboarding | 8/8 | Token matrix, clarifications, Altman Z′ prototype vectors, transactional approve |
| 6 Custom fields | 10/10 | All-8-types loop (NEW) + CF4 trio (display/insert-before/show-in-list) API+UI |
| 7 Custom lists | 6/6 | Lifecycle + guards + order-mode + inactive-value semantics vitest (NEW) |
| 8 Segments | 6/6 | Lifecycle + per-PO-line grain pin (NEW) + Unassigned bucket + reachability |
| 9 Entry forms + numbering | 5/5 | Composer + resolution + @tokens + numbering×5; per-role numbering → B2 |
| 10 Saved views | 5/5 | Criteria/share/scoping + tokens + pagination + stable keys + three-layer auth |
| 11 Dashboards + metrics | 9/9 | Seed drift + copy-on-write + CF3 five browser proofs + metric scoping |
| 12 Global search | 3/3 | Typed hits by role + CF1-T5 deep links + regressions |
| 13 Cross-cutting UI | 5/5 | Money round-trip, icons, sidebar, FieldSpec/raw-element bans, archetype adoption |
| 14 Data integrity | 6/6 | FK floor ≥63 (NEW, live DB), CHECK 23514 (NEW), xmin×9 (NEW), timestamps, StoredFile, derived view |

## What the sweep changed

- **SWEEP-FIX-T1** (`e6cf324`) — genuine bug found by the baseline run: entry-form copy
  failed forever after the first copy (immutable derived code collided; UI swallowed the
  400). Server now de-dupes the internal code; UI surfaces copy errors; pinned by xUnit
  and proven idempotent. Details in `SWEEP-FINDINGS.md` (F1).
- **TEST-SWEEP-T1/T2** (`edcbc88` + DemoGating addition) — 8 new tests closing every
  no-coverage row: explicit dev-endpoint 404-in-Production, one-award-per-RFQ,
  all-8-datatype creation, per-PO-line segment grain, FK floor, custom-value CHECK
  violation (SQLSTATE 23514 from live Postgres), xmin token breadth (all 9 aggregates
  named), inactive-list-value option semantics.
- **F2** — one unreproduced 08-entry-forms intermittency in full-suite context;
  mitigated with self-healing setup (details in `SWEEP-FINDINGS.md`).

## Baseline movement (hold-or-raise)

API 471 → **478** · web 233 → **234** · e2e 65 → **65** (flake fixed + hardened).
No test was weakened, skipped, or deleted.

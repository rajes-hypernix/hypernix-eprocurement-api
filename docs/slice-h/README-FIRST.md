# README-FIRST — Slice H: Analytics Substrate

The last slice of the audit's register, and the foundation everything ahead
depends on: Payment Vouchers, the NetSuite sync, and the SuiteAnalytics
reporting story committed to SPSB. Constraints are non-negotiable.

1. **Additive, behaviour-preserving.** No user-visible change. Screens,
   endpoints, workflows behave identically. The e2e crawl (42/42) is the
   proof. Where a DTO must change shape (typed dates), the web must be
   updated in the SAME slice so nothing breaks — this is the one slice
   permitted to touch both sides for a single reason.

2. **Analytics-grade data hygiene is the point.** Stable keys, conformed
   dimensions, decimal money with currency, declared grain per table, typed
   dates. Every decision serves a future star schema.

3. **Derive, don't store.** Aggregates that can be computed are computed.
   No stored value that can drift from its source (the Award.TotalValue
   precedent from Hardening 1).

4. **Append-only stays append-only.** Provenance links, event logs and
   sourcing records are never deleted. Transition timestamps are added as
   typed columns, never as overwrites of planned values.

5. **One migration per concern, each with a data-restoring Down().**
   Where a value is derivable, Down() restores it. Prove Up→Down→Up on a
   freshly seeded database and report row counts.

6. **Commit per task.** Atomic, buildable, full suite green before each.
   CI must be green on push — report the Actions run in the final report.

7. **Baselines:** API 279/279, web vitest 112, tsc/oxlint clean, crawl 42/42.
   Every task adds tests; nothing drops.

8. **Report before touching.** File plan per task, then WAIT.

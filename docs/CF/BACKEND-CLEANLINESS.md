# BACKEND-CLEANLINESS — 12-dimension audit (LONGRUN Phase 2)

**Overall verdict: CLEAN-WITH-NOTES.** No ❌ on any dimension. Two ⚠️ notes are
inventoried below; neither is structural damage — one is dev-phase migration history,
one is a browser-test style point. Nothing needed a BE-CLEAN fix commit beyond the
single compiler warning already repaired inside the Phase-1 sweep commit (`edcbc88`).

| # | Dimension | Verdict | Evidence |
|---|---|---|---|
| 1 | Compiler hygiene | ✅ | `dotnet build --no-incremental`: **0 warnings, 0 errors** across all 5 projects. (One CS8604 in a new CF4 test was found and fixed during Phase 1 — build was warning-free before this run's own additions and is again.) |
| 2 | Nullable correctness | ✅ | `<Nullable>enable</Nullable>` in 5/5 csproj. `#nullable disable` appears **only in EF-generated migration files** (83 scaffolded files — standard EF output, not hand code). Zero `#pragma warning disable CS86xx` in src. 14 `!.`-forgiving uses across ~30k LoC, no clusters. |
| 3 | Async/await correctness | ✅ | Zero `.Result` / `.Wait()` / `GetAwaiter().GetResult()` / non-handler `async void` in src. CancellationToken threaded on every public async service method (one grep hit was a line-wrapped signature — verified present, `OnboardingService.cs:238`). |
| 4 | EF Core discipline | ✅ | 170 `AsNoTracking()` uses across read paths. **Zero** raw SQL in src (`SqlQueryRaw`/`FromSqlRaw`/`ExecuteSqlRaw` — the only interpolated SQL lives in a Postgres-backed TEST probe with a documented EF1002 note). `dotnet ef migrations has-pending-model-changes`: **"No changes have been made to the model since the last migration."** No loop-issued queries observed in hot paths (in-memory view runner is a documented, ruled trade-off with a named escape seam). |
| 5 | Layering integrity | ✅ | Dependency direction exact: Api→Infrastructure→Application→Domain; Domain has **0 package references**. Controllers: zero `using eProcure.Infrastructure` except `HealthController` (DB connectivity ping — deliberate) and the demo auth handler (Api infrastructure plumbing, not a controller). Controllers are thin — logic lives in services (validated by the sweep's service-level test coverage). |
| 6 | Domain encapsulation | ✅ | **0** direct `.Status =` / `.HeaderStatus =` writes outside domain methods (was 17 pre-Slice-G). Transition timestamps private-set (e.g. `Invoice.Status { get; private set; }`), stamped in domain methods — pinned by `TransitionTimestampsTests`. |
| 7 | Clock & determinism | ✅ | **0** naked `DateTime.UtcNow`/`.Now`/`DateTimeOffset.UtcNow` in src outside the `IClock` abstraction itself. IClock injected everywhere time is read. |
| 8 | Error handling & HTTP semantics | ✅ | `ExceptionMiddleware` maps every domain exception family to its status: NotFound→404, Forbidden→403, View/Dashboard/CustomField/Form validation→400, DomainRule→409/400, DbUpdateConcurrency→409 (pinned by `ConcurrencyTests.ExceptionMiddleware_maps_a_concurrency_conflict_to_409`). **0 empty catches**; responses carry title+detail, never stack traces. |
| 9 | Authorization consistency | ✅ | 150 `[Action]` attributes; 14 deliberate `[AllowAnonymous]` — **all enumerated on both sweep exemption lists** and enforced by `AnonymousSweepTests` + `ActionAssignmentSweepTests` (exactly-one-catalog-action per authenticated endpoint, no orphan actions) + `RoleMatrixTests` (per-role 403 theory). These sweeps are green in the 478. |
| 10 | Dead code / TODO / secrets | ✅ | **0** TODO/HACK/FIXME/XXX in src (holds the pre-CF snapshot). **0** hardcoded secrets or personal emails in tracked src (git-grep for the A2F-scrubbed address and password literals: clean; conn strings come from config/env). |
| 11 | Test quality | ⚠️ note | 0 skipped tests anywhere (API/web/e2e); integration layer hits **real Postgres** (Testcontainers-style local docker + live-DB probes); assertion-free tests: none found. **Note:** e2e specs use 112 `waitForTimeout` fixed sleeps alongside condition waits — they mask no known race today (65/65 twice consecutively) but are the style the F2 intermittency investigation would tighten first. Structural → reported, not mass-rewritten unsupervised. |
| 12 | Migration safety | ⚠️ note | Model snapshot in sync (Dimension 4). CF-era migrations (`CustomListOrderMode`, `CustomFieldAuthoring`) are purely additive. **Note:** several *development-era* slice migrations carry `DropColumn`/`DropTable` in `Up()` (Slice04/11/A/C/D restructures). These were iterative dev-phase reshapes on rebuildable databases, and `StableLineKeys` documents its non-destructive approach explicitly — but if any environment must be migrated **through** the historical chain rather than from scratch, those Up() drops are data-affecting. Recommendation (operator decision): squash history to a clean baseline before first production deploy. |

## Fixed under this phase
Nothing required a standalone `BE-CLEAN-Tn` commit: the sole compiler warning was
repaired in Phase 1 (`edcbc88`); every other trivially-fixable class (naked clocks,
sync-over-async, empty catches, TODO debt, secrets, EF1002 in src) had **zero instances**.

## Reported for the operator (top items, none urgent)
1. **Migration history squash before prod** (D12) — historical dev-phase Up() drops are
   fine for rebuilt DBs, wrong for migrating a long-lived DB through the chain.
2. **e2e fixed-sleep tightening** (D11) — replace `waitForTimeout` with condition waits
   opportunistically, starting with 08-entry-forms (the F2 file).
3. **Confirm production Postgres version** — feeds the CF6 partial-index vs
   NULLS-NOT-DISTINCT decision (already built version-independent; see BLOCKERS).

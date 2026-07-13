# BACKEND CLEANLINESS AUDIT — spec & pass bars

**Purpose:** "check if the backend is clean." This is a fixed rubric the agent runs and scores.
Each dimension has a probe and a pass bar. Findings go to `docs/CF/BACKEND-CLEANLINESS.md` with a
verdict per dimension (✅ clean / ⚠️ note / ❌ fix-needed). Fixes for anything trivially fixable
(warnings, formatting) may be committed under `BE-CLEAN-Tn:`; anything structural is REPORTED,
not fixed unsupervised (structural backend changes need operator eyes, same as CF5+ architecture).

## Dimension 1 — Compiler hygiene
- Probe: `dotnet build` treating warnings as errors (or full warning capture).
- Pass bar: 0 warnings, 0 errors. Any MSBuild/analyzer warning listed with file+line.
- Fixable unsupervised: yes (version pins, unused usings). Commit if fixed.

## Dimension 2 — Nullable correctness
- Probe: `<Nullable>enable</Nullable>` on all projects; count `#nullable disable`, `!` null-forgiving
  operators, and `#pragma warning disable CS86xx`.
- Pass bar: nullable enabled everywhere; null-forgiving used only where genuinely justified
  (documented). Flag clusters of `!` as a smell.
- Fixable: only trivially; structural nullability changes → report.

## Dimension 3 — Async/await correctness
- Probe: grep for `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `async void` (non-handler),
  missing `CancellationToken` on public async service methods, `async` methods with no `await`.
- Pass bar: no sync-over-async blocking; CancellationToken threaded through service layer;
  no `async void` outside event handlers.
- Fixable: report (changing async signatures ripples).

## Dimension 4 — EF Core discipline
- Probe: N+1 risks (loops issuing queries), missing `AsNoTracking()` on read paths, `Include`
  explosions, raw SQL interpolation (EF1002), untracked `SaveChanges` batching, migration
  drift (`dotnet ef migrations has-pending-model-changes` equivalent — model vs snapshot).
- Pass bar: read paths AsNoTracking; no interpolated SqlQueryRaw; model matches last migration
  (no pending changes); no obvious N+1 in hot paths.
- Fixable: EF1002 parameterization yes; query restructuring → report.

## Dimension 5 — Layering / clean-architecture integrity
- Probe: Domain has zero EF/ASP.NET dependencies; Application depends on Domain only;
  Infrastructure implements Application interfaces; no controller reaches past Application into
  Infrastructure directly; no domain logic leaked into controllers (fat-controller smell).
- Pass bar: dependency direction intact; controllers thin; domain pure.
- Fixable: report (moving code across layers is structural).

## Dimension 6 — Domain encapsulation
- Probe: count direct `.Status =` / state-field writes outside aggregate methods (should be 0,
  was 17 pre-Slice-G); public setters on transition timestamps (should be private set);
  aggregates mutated only through methods.
- Pass bar: 0 direct status writes; transition stamps private-set; invariants enforced in domain.
- Fixable: small encapsulation tightening yes (like A2F NIT-3); report if widespread.

## Dimension 7 — Clock & determinism
- Probe: `DateTime.UtcNow` / `.Now` / `DateTimeOffset.UtcNow` outside the SystemClock (should be 0);
  IClock injected everywhere time is read.
- Pass bar: 0 naked clock reads; IClock discipline total.
- Fixable: yes (inject the clock).

## Dimension 8 — Error handling & HTTP semantics
- Probe: exception middleware maps domain exceptions to correct status codes (400 validation,
  403 forbidden, 404 not-found, 409 concurrency/conflict); no swallowed exceptions
  (`catch {}` empty); no leaking stack traces in responses.
- Pass bar: consistent status mapping; no empty catches; no info leak.
- Fixable: report (error-contract changes need review).

## Dimension 9 — Authorization consistency
- Probe: every controller endpoint carries `[Action]` or a deliberate `[AllowAnonymous]`;
  ApiActions-count == ActionCatalog-row-count; every anonymous route is on both sweep lists with
  a comment; no endpoint silently unguarded.
- Pass bar: 100% endpoints guarded-or-explicitly-anonymous; counts reconcile; sweeps green.
- Fixable: a missing guard is a SECURITY fix — apply it and flag prominently.

## Dimension 10 — Dead code / TODO debt / secrets
- Probe: TODO/HACK/FIXME/XXX count; unused public methods; commented-out code blocks;
  hardcoded secrets/connection strings/personal emails in tracked files (git grep).
- Pass bar: TODO debt inventoried (0 is ideal — pre-CF snapshot had 0); no secrets/personal
  data in tracked source (A2F scrubbed vieshall@ — confirm still 0 in live tree excl. audit docs).
- Fixable: dead-code removal yes; secrets → remove + flag.

## Dimension 11 — Test quality (not just count)
- Probe: tests actually assert (no assertion-free tests); integration tests hit real Postgres;
  no tests disabled/skipped without reason; browser tests wait on real conditions (not fixed
  sleeps that mask races).
- Pass bar: no skipped tests without a logged reason; integration layer real; assertions present.
- Fixable: report.

## Dimension 12 — Migration safety
- Probe: every migration reversible where feasible; destructive ops (DropColumn/DropTable) only
  in Down() or with justification; CF-era migrations (CustomListOrderMode, CustomFieldAuthoring,
  + any CF5/6/7) present and clean; no data-loss Up() without a note.
- Pass bar: Up() paths non-destructive or justified; model-snapshot in sync.
- Fixable: report.

## AUDIT COMPLETE
- [ ] All 12 dimensions probed and scored in `BACKEND-CLEANLINESS.md`.
- [ ] Trivially-fixable items (warnings, EF1002, unused usings, clock injection, dead code,
      secrets) fixed under `BE-CLEAN-Tn:` commits, suites green.
- [ ] Structural findings REPORTED with recommendation, NOT fixed unsupervised.
- [ ] Overall backend verdict stated: clean / clean-with-notes / needs-attention, with the
      top 3 things (if any) for the operator to decide.

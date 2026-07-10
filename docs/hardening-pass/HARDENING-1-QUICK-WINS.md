# HARDENING 1 of 2 — Quick-Wins Pass (backend, small independent fixes)

Surgical session: small, independent fixes from the 2026-07-02 audit plus
two backlog rows. No refactoring, no scope creep. Read docs/BACKLOG.md
first — T8 and T9 close two of its rows; update the rows to "done" with
this slice named.

## Ground rules
1. BEFORE any change: list every file you'll touch, grouped by task. WAIT.
2. All timestamps via IClock. Exactly ONE migration (T4+T5+T6), named
   QuickWinsHardening, with a working Down(). Verify Up→Down→Up on dev seed.
3. Baselines: API 258/258 (+K7 delta if it added an API test — use actual),
   web vitest at its current count, e2e-audit crawl green. Report exact
   numbers before/after.
4. If any task is larger than described: STOP that task, report, continue
   with the others.

## Tasks

### T1 — Demo identity: environment-gate + explicit demo mode (SEC-1/X10)
CurrentUser.cs (~:53) honours X-Demo-User with no environment check, and
AuthController's dev-users/dev-login are always on. Replace with this rule:
- Development: X-Demo-User honoured unconditionally (local demos unchanged).
- Non-Development: honoured ONLY if config Demo:Enabled == true AND the
  environment is NOT Production. In Production the header is ignored
  regardless of config — a hard IsProduction() block, not a config default.
- When Demo:Enabled is active outside Development, log a startup warning:
  "DEMO MODE ENABLED — X-Demo-User impersonation is active. Never enable
  in production."
- dev-users / dev-login endpoints follow the same rule (404 when inactive).
Bind a DemoOptions class ("Demo" section), default Enabled=false; add
Demo:Enabled=false explicitly to appsettings.json as documentation.
Tests: fallback inert in a fake Production env even with Enabled=true;
active in Development; active in Staging+Enabled; inactive Staging+disabled.

### T2 — Env-gate CORS (SEC-7)
Program.cs (~:110): UseCors("dev-web") unconditional → Development only.

### T3 — Delete orphan client functions (PRG-4, updated post-Slice K)
client.ts: delete createVendor, getDevUsers, getOnboardingInvitations and
dangling imports. Do NOT delete revokeOnboardingInvitation — Slice K wired
it (in use). Keep all server endpoints. tsc -b + oxlint clean.

### T4 — Award.RfqId unique index (DBA-4)
AwardService assumes one award per RFQ; nothing enforces it.
HasIndex(x => x.RfqId).IsUnique() on Award. → QuickWinsHardening migration.

### T5 — Stop persisting Award.TotalValue (DBA-10)
Convert to computed not-mapped property derived from allocations (follow
the PO.Total pattern; builder.Ignore). Verify the DoA gate in AwardService
reads the computed value. Column drop in the same migration. Test:
TotalValue always equals sum of allocations; existing award/DoA tests pass.

### T6 — Small schema corrections (DBA-12, partial)
Same migration: (a) VendorOnboardingInvitations.TokenHash index → unique;
(b) TechnicalScore gains CreatedUtc (IClock on creation). Nothing else —
no check constraints this session.

### T7 — API-side hardcoded date literals (K7 follow-through)
K7 fixed the frontend date literals. grep the API tree for the same class
('28/06/2026' and similar literals used as data defaults). Report hits
first; fix data-producing defaults to IClock; leave test fixtures and seed
data untouched (seeded historical dates are correct — report only).

### T8 — DashboardService "RFQs to bid" excludes Declined (BACKLOG / K6a)
DashboardService: toBid currently counts Declined invitations. Exclude
Declined (and confirm Rescinded is already excluded upstream). Test: a
declined invitation does not appear in the vendor's to-bid count.

### T9 — Expose MaxExtensions in RfqDetail (BACKLOG / Slice J carry-over)
Add MaxExtensions (from RfqGovernanceOptions) to the RfqDetail DTO;
regenerate web schema; replace the client MAX_EXTENSIONS constant in
RfqGovernance.tsx with the server value; delete the constant. Extend modal
now displays the server cap. vitest adjusted accordingly.

## Final report
Per-task files + one-line summary + test evidence; migration tables +
Down() proof; full suite results vs baselines; BACKLOG.md rows closed;
anything observed but not fixed (list only).

## Out of scope
[Authorize]/fallback auth policy, FilesController gating, bank masking,
GET /api/rfqs vendor scoping (all = HARDENING 2), FKs, concurrency tokens,
the 17 direct .Status= writes, line Guids, transition timestamps.

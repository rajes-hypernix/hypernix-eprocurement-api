# README-FIRST — Slice K: Vendor Portal Close-Out

Constraints for this slice. Non-negotiable.

1. **Frontend-first, API-frozen.** This slice wires UI to endpoints that
   ALREADY EXIST. If a gap genuinely requires an API change, STOP and
   report for an explicit go before touching the API — no matter how small
   or obvious the fix. (Standing rule from Slice J sign-off.)

2. **The prototype is the behavioural oracle.** The HTML prototype
   (prototype/eprocure-portal.html — the single clickable oracle with inline
   <script>; there is no separate Vendor Portal.html / eproc-app.js) defines expected
   vendor-portal UX. Where the .NET build's vendor screens lack behaviour
   the prototype has, that is a gap. Where the prototype lacks something,
   do NOT invent it.

3. **Two-phase execution.** Phase 0 produces a gap inventory and WAITS for
   scope confirmation. No code changes during Phase 0.

4. **Protected flows untouched:** RfqBuilder internals, Consolidate,
   Confirm Lines, envelope/evaluation/award screens, the dashboard
   analytics panel, and all Slice J governance components (extend/rescind/
   decline behaviour must not regress).

5. **Existing conventions only:** app style tokens (square corners, badge
   palette, chip/drawer patterns), TanStack Query for all server I/O,
   S.* state conventions where applicable, existing Modal/pill components.
   No new npm dependencies.

6. **Baselines:** API 258/258 (unchanged — should not move this slice),
   web vitest 103/103 + new tests, tsc -b clean, oxlint clean, and the
   e2e-audit crawl green at close.

7. **Register upkeep:** any newly-wired user action is appended to
   docs/PERMISSIONS-REGISTER.md (e.g. RevokeOnboardingInvitation,
   RaiseClarification as vendor). This slice is expected to bring the
   register close to complete enumeration — note that status in the final
   report.

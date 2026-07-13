# READ ME FIRST — Slice A2F: Audit #2 Fixes

**Scope:** close every finding from `docs/reviews/full-audit-2026-07-12.md` **except** the
deferred feature frontier. Nothing in this slice builds Payment Vouchers, Contract
Management, scenario sourcing, multi-round bidding, or NetSuite integration. If a task
seems to require touching those, STOP and report — it doesn't.

**What this slice is:** one security fix (AUTHZ-1), one staging blocker (Obs-6), one data
guard (GAP-5), the handover email scrub, three nits, CI cleanup, and a ledger/docs truth
pass. All small, all precisely located by the audit. This is a hardening slice, not a
feature slice.

## Session contract (unchanged from every prior slice)

1. **Step 0 first.** Produce the file plan (every file you will create/modify per task,
   with one-line reasons). **WAIT for confirmation** before writing any code.
2. **One task = one atomic commit**, message prefixed `A2F-Tn:`. CI-equivalent gates green
   before each commit: `dotnet test` (all green), `npx vitest run`, `npx tsc -b`,
   `npx oxlint --type-aware` (0 errors).
3. **Protect existing behaviour.** 448 API tests, 229 web tests, 48 crawl tests pass
   today. They must all pass after every task. You may ADD tests; you may only change an
   existing assertion when the task explicitly says so (T1 changes some matrix
   expectations — the task text lists exactly which).
4. **Five-lens review in the final report** (vendor / buyer / solution architect / senior
   programmer / CTO), per task.
5. **Docs are part of done.** AUTHORIZATION-MATRIX, BACKLOG, SETUP get updated in the same
   commit as the code they describe.

## Task order (risk first)

| Phase | Task | What | Why this order |
|---|---|---|---|
| 1 — Security | T1 | AUTHZ-1: org-wide aggregate metrics reachable by vendor principals | The only real defect. Server-side. Do it first. |
| 1 — Security | T2 | Obs-6: anonymous onboarding lookups (token-gated) | Staging blocker; touches the anonymous perimeter, so it rides with T1's security focus. |
| 2 — Integrity | T3 | GAP-5: custom-list value delete needs an in-use guard | Data-integrity hole in the config engine. |
| 2 — Integrity | T4 | NIT-1: extract the D5/D6 copy-paste reachability guard | Do it while both call sites are fresh from T1's neighbourhood. |
| 2 — Integrity | T5 | NIT-3: `Award.ApprovedUtc` encapsulation | Two-line domain tidy. |
| 3 — Hygiene | T6 | Personal-email scrub (13 files) | Handover blocker. Mechanical. |
| 3 — Hygiene | T7 | CI cleanup: MSB3277, `--legacy-peer-deps`, EF1002, checkout@v5, MessageInput confirm | The BACKLOG's standing CI rows. |
| 4 — Ledger | T8 | BACKLOG + matrix + PRIMITIVES truth pass, `appsettings.Development.json.example`, refresh `e2e-audit/results.json`, tag `v0.7-audit2-fixes` | Docs last, so they describe the final state. |

**Final gate:** full Playwright crawl (all specs) green, `results.json` committed from that
run, tag pushed. Report with five-lens review, per-task evidence, and the updated BACKLOG
diff.

## Hard boundaries

- Do NOT modify `AwardService.ApprovalThresholdMyr` (DoA config is an L5 concern).
- Do NOT widen the anonymous surface beyond the token-gated onboarding lookups in T2, and
  every addition must appear in BOTH sweep-test lists with a comment, same as the existing 11.
- Do NOT touch `NetSuiteClientStub`, the Payments placeholder, or anything under the
  deferred list.
- Migrations: T3 adds one (`CustomListValueIsActive`). No other schema change is expected;
  if you believe another is needed, STOP and report before writing it.

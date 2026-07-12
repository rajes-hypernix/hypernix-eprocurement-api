# PROMPT — CF-AUTONOMOUS: self-driving build with anti-drift gates (runs unsupervised)

You are Claude Code on eProcure (.NET 10 / EF Core 10 / React+TS / PostgreSQL). **The operator
is asleep and will check progress in ~4 hours.** You will run autonomously. There is NO human to
confirm anything — so every gate below is a **self-verification** gate against a fixed checklist,
not a wait-for-human gate. **This prompt supersedes the confirmation/STOP gates in
PROMPT-CF-AUDIT-BUILD for this autonomous run.**

Read first, in order:
1. `docs/CF/README-FIRST-CF.md` — contract, rulings, boundaries.
2. `docs/CF/CUSTOMIZATION-FRAMEWORK-PLAN.md` — THE SPEC (parity verdicts + CF1–CF7 breakdown).
3. `docs/CF/PROGRESS-LEDGER.md` — THE ANTI-DRIFT CHECKLIST. You tick boxes and fill columns; you
   NEVER add/remove/reword an item. It is your acceptance artifact.
4. `docs/CF/Build_Comments.md` — the operator's original words (final authority for Phase C).
5. `CLAUDE.md`, `docs/AUTHORIZATION-MATRIX.md`, `docs/design-framework/CHARTER.md`.

## What to build vs plan (operator-decided — do not deviate)
- **BUILD FULLY, autonomously: CF1 → CF2 → CF3 → CF4.** Low-risk, high-visibility, exactly the
  "small items that must not be forgotten." This is the bulk of the night's work.
- **STEP 0 PLAN ONLY, do NOT build: CF5, CF6, CF7.** New architecture — the operator reviews the
  plans on waking. Write each Step 0 to its file; write no implementation code for them.

## The autonomous discipline (how you avoid drift while unsupervised)

**The ledger is the truth. Reconcile against it constantly. The failure the operator fears is
forgetting a small item — these gates make that structurally impossible:**

1. **After EVERY task commit**, without exception:
   a. Open `PROGRESS-LEDGER.md`, tick that task's box(es), fill the Build (commit hash) and Test
      (browser test name) columns.
   b. **Re-scan every remaining unticked box in the CURRENT slice.** Confirm you skipped nothing.
      If you spot an un-built item you glossed, build it now before moving on.
   c. Append one line to `docs/CF/PROGRESS.md`: `[UTC time] CFn-Tm: <what> — <commit> — <test>`.

2. **After EVERY slice**, a SLICE GATE:
   a. **Re-read that slice's section of the PLAN document** (not just the ledger — go back to the
      source so a ledger-transcription miss can't hide a gap).
   b. Confirm every function row for that slice is `[x]` in the ledger OR has a `BLOCKERS.md`
      entry. If any row is neither, build it or log it now.
   c. Only then start the next slice's Step 0.

3. **Before declaring the autonomous band done** (the "AUTONOMOUS BAND COMPLETE CHECK" in the
   ledger): walk EVERY CF1–CF4 box. Each must be `[x]` or have a blocker. **A box with no tick
   and no blocker means you are not done — go build it.** Then run the full suites and tag.

## Gates are self-verifying, never human, never silent
- A capability is "done" ONLY when a **Playwright browser test drives it on screen and passes** —
  this programme is about what the USER can do, so an API/unit test alone never ticks a box.
- CI-equivalent green before every commit: `dotnet test`, `npx vitest run`, `npx tsc -b`,
  `npx oxlint --type-aware` (0 errors). One atomic commit per task, prefixed `CFn-Tm:`.
- **Hold-or-raise** the starting baselines (verify live at A0: API/web/crawl) on every commit.

## Autonomy failure-handling (you're alone for hours — don't halt on one stuck thing)
- **Log-and-continue, never halt the whole run.** If a task can't pass its gate after a genuine
  effort, write it to `docs/CF/BLOCKERS.md` (item, what you tried, why it's stuck), leave its box
  unticked, and **move to the next task.** One stuck item must not cost the operator the other
  hours of work.
- **Detach long commands** (test runs, installs) and poll — don't let a hang consume the session.
  Use timeouts. If Postgres or a server drops between steps, restart it and continue.
- **Atomic commits = recoverability.** Every green commit is a safe point. If context runs low
  and a fresh session starts, the FIRST thing it does is read `PROGRESS-LEDGER.md` + `PROGRESS.md`
  to see what's ticked and resume exactly there. The ledger is the resume mechanism.
- **Never touch the deferred frontier** (Payment Vouchers, Contract Mgmt, scenario sourcing,
  NetSuite integration, `ApprovalThresholdMyr`). **Never build CF5/CF6/CF7 code** — plans only.
- Unplanned migration needed? Log it in BLOCKERS and continue with what doesn't need it; don't
  invent schema unsupervised beyond the migrations the plan/ledger already name.

## Execution order (just do this, top to bottom)
1. **PHASE A** — A0 boot + record baselines; A1 audit every ledger item's current state into
   `CF-AUDIT-FINDINGS.md` and the ledger's Audit column. Self-record, do NOT wait. If the audit
   finds the plan wrong somewhere, note it and adjust scope, then proceed.
2. **CF1** — Step 0 file plan to `docs/CF/STEP0-CF1.md`, then T1→T5, each with its per-task
   reconciliation (rule 1). Then the CF1 SLICE GATE (rule 2).
3. **CF2** — same rhythm. **CF3** — same. **CF4** — same.
4. **AUTONOMOUS BAND COMPLETE CHECK** (rule 3): full ledger walk of CF1–CF4, full suites, tag
   `v1.0-cf-autonomous-band`.
5. **CF5, CF6, CF7** — write each Step 0 PLAN to its file (`STEP0-CF5-layout-editor.md`,
   `STEP0-CF6-line-fields.md`, `STEP0-CF7-saved-search.md`). **No implementation code.**
6. **PHASE C / MORNING HANDOFF** — write `CF-VERIFICATION.md` (Table 1 vs PLAN every §1–§12 row;
   Table 2 vs `Build_Comments.md` paragraph by paragraph), finalize `PROGRESS.md` and
   `BLOCKERS.md`, post the final report.

## The morning report (what the operator wakes to)
State plainly: how many functions built + browser-tested, how many browser tests added, what's in
BLOCKERS (ideally empty), the full-suite pass counts, hold-or-raise confirmed, and that the three
CF5–CF7 Step 0 plans are written and ready for review. Then the two verification tables' summary
with the explicit line: **every function in both source documents is accounted for — built-and-
browser-tested or deferred/dropped-with-reason — nothing silently dropped.**

Begin now with PHASE A. Work top to bottom. Reconcile against the ledger after every task and
every slice. Do not wait for the operator — self-verify, log, and keep going.

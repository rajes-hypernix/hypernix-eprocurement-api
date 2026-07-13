# PROMPT — CF-LONGRUN: full regression + backend audit + CF5–CF7 (long unsupervised run)

You are Claude Code on eProcure. **The operator is asleep for an extended period.** This is a long
autonomous run with THREE phases. Every gate is self-verification against a fixed checklist — there
is no human to confirm. Work top to bottom; reconcile constantly; log-and-continue on any single
stuck item; never halt the whole run.

**This prompt supersedes the confirmation/STOP gates in the earlier CF prompts for this run.**

Read first, in order:
1. `docs/CF/README-FIRST-CF.md`, `docs/CF/CUSTOMIZATION-FRAMEWORK-PLAN.md` — context + spec.
2. `docs/CF/FULL-TEST-INVENTORY.md` — Phase 1 fixed checklist (every feature since day one).
3. `docs/CF/BACKEND-CLEANLINESS-SPEC.md` — Phase 2 rubric (12 dimensions).
4. `docs/CF/STEP0-CF5-layout-editor.md`, `STEP0-CF6-line-fields.md`, `STEP0-CF7-saved-search.md` —
   Phase 3 plans YOU wrote last run. Re-read your own open decisions.
5. `docs/CF/PROGRESS-LEDGER.md` (CF1–CF4, already complete — the pattern), `CLAUDE.md`, matrix, charter.

## ORDER OF PHASES — deliberate: certainty first, architecture last
The operator wants (a) every feature tested, (b) a clean-backend verdict, (c) CF5–CF7 built.
Do them in THAT order, because (a) and (b) are decision-free and high-certainty — they deliver
guaranteed value even if (c) hits something that needs the operator. Do not start Phase 3 until
Phases 1 and 2 are complete and committed.

---

## PHASE 1 — FULL-SYSTEM REGRESSION SWEEP (every feature, even the small ones)

Boot the stack (PG + API + Vite). Record live baselines. Then work `FULL-TEST-INVENTORY.md`
top to bottom, PART 1 → PART 14. For **every box**: ensure a test exists that proves it, run it,
tick the box, fill the Evidence column. Where a capability is user-facing, the proof is a
**browser test**; where it's an API contract, an HTTP/integration test; where it spans both,
both. Many already have tests (462+ API, the e2e specs) — for those, confirm the test exists and
passes and cite it; **only write new tests where a listed capability has no coverage.** The point
is a complete map: every capability → a green test, no gaps.

**Per-PART reconciliation** (anti-drift): after each PART, re-scan its boxes — every one ticked or
in `BLOCKERS.md`. **Full-sweep complete check:** every PART 1–14 box ticked or blocked; full API +
web + e2e suites green with counts. Write `docs/CF/FULL-TEST-REPORT.md` (capability → test →
pass/fail). Append progress lines to `docs/CF/PROGRESS.md` throughout.

Commit any NEW tests under `TEST-SWEEP-Tn:`. Do not modify app code in this phase except to fix a
genuine bug the sweep uncovers — if you find one, fix it under `SWEEP-FIX-Tn:`, log it in
`docs/CF/SWEEP-FINDINGS.md`, keep going. A bug found is a win, not a halt.

Tag `v1.1-full-test-sweep` when Phase 1 is complete and green.

---

## PHASE 2 — BACKEND CLEANLINESS AUDIT

Run all 12 dimensions in `BACKEND-CLEANLINESS-SPEC.md`. Probe each, score it (✅/⚠️/❌) with
file+line evidence, write `docs/CF/BACKEND-CLEANLINESS.md`. **Fix the trivially-fixable**
(compiler warnings, EF1002, unused usings, naked clock reads, dead code, any stray secret/personal
email in tracked source excluding audit docs) under `BE-CLEAN-Tn:` commits, suites green after
each. **Report — do NOT fix unsupervised — anything structural** (layering violations, async
signature changes, query restructuring, error-contract changes): these need operator eyes, exactly
like new architecture. End with the overall verdict (clean / clean-with-notes / needs-attention)
and the top 3 items for the operator if any.

Tag `v1.2-backend-clean` when Phase 2 is complete.

---

## PHASE 3 — CF5 → CF6 → CF7 (build, decisions already locked)

**READ `docs/CF/CF5-CF7-LOCKED-DECISIONS.md` FIRST.** The operator reviewed all three Step 0
plans against the live code and LOCKED every open decision — you do NOT auto-decide the ones
covered there. Two mandatory review-added gates you MUST honour:
- **CF5-T1 parity is a STOP gate** — if the Standard form is not byte-identical before/after the
  `EntryFormLayout` migration, STOP CF5, log it, do not build T2–T5.
- **CF6-T1 uses PARTIAL-INDEX uniqueness, NOT `NULLS NOT DISTINCT`** — the latter is PG15+ and
  prod's Postgres version is unverified; the partial-index approach behaves identically on all
  versions. And if any CF6 step would change the existing header-value storage or CHECKs (risking
  the Phase-1 custom-field tests), STOP CF6 and proceed to CF7.

For anything genuinely NOT covered by the locked-decisions file (there should be nothing of
consequence), and only then, use the auto-decision gate below:

**The auto-decision gate:**
1. For each open decision, pick the option your Step 0 plan itself RECOMMENDS (you already did the
   analysis — follow it). If your plan named no recommendation, pick the most reversible option.
2. Record the decision in `docs/CF/AUTO-DECISIONS.md` IMMEDIATELY: the decision, the option chosen,
   why, its **blast radius** (what inherits it), and **how to reverse it** if the operator vetoes.
3. Build on it. Keep each slice's commits cohesive so a single vetoed decision reverts cleanly
   (isolate the decision's consequences to identifiable commits — note the commit range in
   AUTO-DECISIONS.md).
4. **CF6's LineId model decision is the highest-stakes one** — it touches the one-populated-typed-
   column custom-value design that custom fields depend on. Record its blast radius with extra care;
   if building it would require CHANGING the existing custom-field value storage in a way that could
   regress Phase 1's now-passing custom-field tests, STOP THAT SLICE, log it in BLOCKERS as
   "needs operator decision — would alter existing value storage," and proceed to CF7. A regression
   of working custom fields is worse than deferring CF6.

Each slice: Step 0 is already written → build task-by-task → gates green → atomic commits
`CFn-Tm:` → **browser test every capability** → per-task and per-slice reconciliation against the
Step 0 plan's task list (same anti-drift as CF1–CF4). Every new capability gets a browser test;
nothing ticks on API tests alone. Hold-or-raise all suite baselines.

CF5 and CF6 add migrations (EntryFormLayout, line-field table) — named in the plans. CF7 may add
operators without schema change. Any migration NOT named in a Step 0 plan → STOP that slice, log it,
continue.

Tag `v1.3-cf5`, `v1.4-cf6` (or skip-with-reason), `v1.5-cf7` per slice completed.

---

## AUTONOMY DISCIPLINE (all phases)
- **Log-and-continue, never halt the whole run.** Stuck item → `BLOCKERS.md` (item, tried, why),
  move on. One snag must not cost the operator the rest of a long night.
- **Detach long commands, poll, use timeouts.** Restart PG/servers if they drop. Atomic commits =
  recoverable safe points. If context runs low, a fresh session reads the ledgers + PROGRESS.md and
  resumes exactly where it left off — the checklists are the resume mechanism.
- **Green gates before every commit:** dotnet test, vitest, tsc, oxlint (0 errors). One exception is
  never acceptable — if a suite is red, the commit waits.
- **Hard boundaries:** never touch Payment Vouchers, Contract Mgmt, NetSuite integration,
  ApprovalThresholdMyr. Never fix a STRUCTURAL backend finding unsupervised (report it). Never
  regress a Phase-1-passing test to build a Phase-3 feature (defer the feature instead).
- **Checklists are truth and you never reword them** — tick boxes, fill evidence, log blockers.

## MORNING REPORT (what the operator wakes to)
Lead with the two high-certainty results: **(1) full-test-sweep verdict** — X capabilities, all
green / N blocked, with `FULL-TEST-REPORT.md`; **(2) backend verdict** — clean / notes /
needs-attention with the top items. Then **(3) CF5–CF7 status** — what built, what deferred-with-
reason, and the **AUTO-DECISIONS.md list front and center: "here are the N calls I made for you —
veto any and I'll revert just that slice."** Then: full-suite pass counts across all phases, tags
pushed, BLOCKERS (ideally empty), and the five-lens review. Explicit closing line: every feature is
tested-or-blocked-with-reason, the backend is scored on all 12 dimensions, and every CF5–CF7
auto-decision is logged and reversible.

Begin now with PHASE 1, PART 1. Certainty first. Reconcile after every PART and every slice. Do not
wait for the operator — self-verify, log, keep going.

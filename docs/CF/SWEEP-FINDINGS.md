# SWEEP-FINDINGS — bugs the Phase-1 regression sweep uncovered

## F1 — Entry-form copy fails forever after the first copy (FIXED: SWEEP-FIX-T1)
**Found by:** 08-entry-forms.spec.ts red on the Phase-1 baseline run (the copy click produced
no composer; the test waited on 'Form name' and timed out).
**Root cause (two layers):**
1. Server: the form Code derives from the Name ("Standard PR Form (copy)" → `ef_standard_pr_form__copy`)
   and collided with a leftover copy from an earlier run → 400 "already exists". Copying the same
   form twice is a legitimate user action; the code is an internal id, so a collision should
   de-dupe, not fail forever.
2. Web: the copy mutation had NO onError — the 400 was swallowed and the click silently did nothing.
**Fix:** server de-dupes with a numeric suffix (`ef_x`, `ef_x_2`, …); UI surfaces copy errors in a
Notice. Pinned by xUnit `Copying_a_form_twice_de_dupes_the_derived_code_instead_of_failing` and
proven idempotent by running 08-entry-forms back-to-back (both green).
**Also:** two litter forms from earlier failed runs deleted via the API (204s — the delete guard path).

## F2 — 08-entry-forms intermittent in full-suite context (MITIGATED, not fully root-caused)
**Observed:** after the F1 fix, 08 failed once in three full-suite runs (passed standalone
every time; two consecutive full greens after). The stranded run-stamped form found in the
DB proves that failure happened AFTER the copy+rename steps, so it is a different, later
step than F1 — its artifacts were overwritten by subsequent green runs before diagnosis.
**Mitigation:** 08 now self-heals at start (sweeps stray non-system Requisition forms via
the API), so a mid-test failure can never poison later runs; SWEEP-FIX-T1's code de-dupe
already made the poisoning harmless. If it recurs, the retained trace will pinpoint the step.

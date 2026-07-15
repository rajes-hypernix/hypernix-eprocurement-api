# CF-HANDOVER BLOCKERS / KNOWN ISSUES (log-and-continue)

## B-1 · 14-CF-FIX4-T6 is a pre-existing shared-state flake (NOT a handover regression)
- **Symptom:** in a full-suite run the segment-lifecycle test intermittently fails; it PASSES in isolation and PASSED in the CFH-T4 gate run (121/121).
- **Root cause:** the test needs a **Draft PurchaseOrder** (`pos.find(p => p.status === 'Draft')`, tests/14-cf-fix4.spec.ts:301) to exercise the assigned-value impact dialog. The seed produces 3 Draft POs, but the e2e suite shares one DB and earlier specs transition POs out of Draft (acknowledge/issue), so the Draft pool can be depleted by the time T6 runs. Playwright has no `retries` configured, so a single environmental miss fails the run.
- **Not caused by any Tn:** CFH-T5 adds PRs only (no POs); the flake predates this work (it was among the pre-fix failures and is order/timing dependent).
- **Gate impact:** none — 120 passed ≥ 117 baseline with the flake counted as a miss; it self-heals on isolation/retry.
- **Follow-up (out of handover scope):** either set `retries: 1` in playwright.config, or make T6 self-provision its Draft PO instead of consuming a shared seeded one.

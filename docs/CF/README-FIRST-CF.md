# READ ME FIRST — Customization Framework Programme (CF1–CF7)

**What this is:** the programme that makes eProcure's customization layer NetSuite-standard —
custom fields, lists, entry forms, segments, dashboards, saved views, global search — BEFORE
the remaining transaction features are built, so the pattern is clean and standardizable. It
implements `docs/CF/CUSTOMIZATION-FRAMEWORK-PLAN.md`, which was written from **browser-verified**
ground truth (every function tested by driving the actual screen, not reading code).

**The two source-of-truth documents this programme is tested against:**
1. `docs/CF/CUSTOMIZATION-FRAMEWORK-PLAN.md` — the parity analysis + build plan (verdicts per
   function: ✅ works / ⚠️ partial / ❌ missing).
2. `docs/CF/Build_Comments.md` — the operator's original feedback, verbatim. The FINAL step
   compares the built system line-by-line against BOTH.

**Seven slices, dependency-ordered:**
- **CF1** Quick parity wins — money format, custom-list edit+order-mode, unique icons, sidebar
  collapse, global-search routing.
- **CF2** Uniform lifecycle — every element edit/inactivate/delete with guards.
- **CF3** Dashboard flexibility — drag-drop, remove portlet, add-portlet bucket, tile
  authoring, populate the view-pickers.
- **CF4** Custom-field authoring parity — display type, insert-before, show-in-list.
- **CF5** Entry-form LAYOUT EDITOR — subtabs as objects, field groups, drag reorder, column
  break. *New architecture — own Step 0.*
- **CF6** Custom LINE fields + sublists. *New architecture — own Step 0. Coupled; do not split.*
- **CF7** Saved View → Saved Search — richer criteria, segments-as-searchable-class.

**Operator rulings already folded into the plan (do not re-litigate):**
- User/Item as saved-search entity classes are **DEFERRED** (build when those entities exist).
- "Customizations as a saved-search class" is **DROPPED** (use a normal admin list view —
  NetSuite doesn't saved-search field defs either).
- Global-search-on-custom-field and encrypted-field options are **DROPPED** (low value for
  SPSB procurement).

## Session contract (unchanged from every prior slice)
1. **Step 0 first, every slice.** File plan (each file created/modified, one-line reason), then
   **WAIT for confirmation** before code. CF5 and CF6 especially — they're new architecture.
2. **One task = one atomic commit**, prefixed `CFn-Tm:`. All gates green before each commit:
   `dotnet test`, `npx vitest run`, `npx tsc -b`, `npx oxlint --type-aware` (0 errors).
3. **Protect existing behaviour.** Current baselines (verify live at start): API 459, web 232,
   crawl 53 — hold-or-raise on all three, every commit. Add tests freely; change an existing
   assertion only when a task explicitly says so.
4. **Five-lens review** (vendor / buyer / solution architect / senior programmer / CTO) in each
   slice's final report.
5. **Every new capability gets a browser test** in the e2e suite — this programme is about what
   the USER can do on screen, so an API test alone is never sufficient. A function is "done"
   only when a Playwright test drives it in the browser and passes.

## The special final step
The programme is NOT done when the last slice commits. It is done when
**PROMPT-CF-AUDIT-BUILD**'s line-by-line comparison passes: every function in BOTH source
documents is either ✅-built-and-browser-tested or explicitly on the deferred/dropped list with
a reason. That comparison is the acceptance gate.

## Hard boundaries
- Do NOT touch the deferred transaction-feature frontier (Payment Vouchers, Contract Mgmt,
  scenario sourcing, NetSuite integration, `ApprovalThresholdMyr`).
- New anonymous routes: none expected; if one is needed, it goes on BOTH sweep lists with a
  comment.
- Migrations: several slices add them (CF1 order-mode, CF6 line-field table, etc.). Each is
  named and expected in its slice; an unplanned migration = STOP and report.

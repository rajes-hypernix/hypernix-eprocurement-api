# Claude Code — copy-paste prompts (one per slice)

Run in order. Paste one, let it finish, **confirm green**, then paste the next.
Every prompt assumes the package lives at `docs/pr-module/` and the mockup at
`prototype/eprocure-pr-module-mockup.html`.

---

## PROMPT — Slice A (backend foundation)

```
Read, in this order, before writing any code:
  1. CLAUDE.md (golden rules)
  2. docs/pr-module/README-FIRST.md  (the GOLDEN CONSTRAINTS — obey all 10)
  3. docs/pr-module/DATA-MODEL-ANALYTICS.md  (BINDING — analytics-grade schema)
  4. docs/pr-module/ENGINEERING-STANDARDS.md  (BINDING — clean-code/DDD/testing bar)
  5. docs/pr-module/PR-MODULE-SPEC.md  (sections 2 and 3)
  6. docs/pr-module/EDGE-CASES.md  (sections A, B, E)

If anything in the spec is ambiguous, OPEN prototype/eprocure-pr-module-mockup.html and match
its behaviour — read recomputePr, lineAct, and the DATA-MODEL NOTES comment block at the top.

This is Slice A: BACKEND ONLY, NO UI. Build additively. Do not touch the existing
"Confirm lines" flow or any existing service behaviour. The schema you commit here is the
foundation for a future SuiteAnalytics-style layer — get it clean now (see
DATA-MODEL-ANALYTICS.md, especially the table of current-model upgrades in section 7).

Do:
- Add Guid Id to PrLine; migrate seeded lines so each has an Id. (Grain key for facts/lineage.)
- Add PrLineStatus { Open, InDraftRfq, InRfq, Awarded, Cancelled, Closed } stored as a stable
  token; map existing values available->Open, rfq->InRfq, awarded->Awarded. "No quotes" is
  DERIVED, not stored.
- Add PrLineSourcing link entity + LinkStatus { Active, Returned, Cancelled } + EF config +
  migration. Append-only: links are NEVER deleted (Returned/Cancelled set
  LinkStatus+ClosedUtc+Reason). Declare its grain in the XML doc-comment.
- Add a stable LineCode/Id to RfqLine if missing (the lineage target).
- ANALYTICS FOUNDATION (additive, don't break existing readers/seed):
  * add real DateOnly source-of-truth fields for RaisedDate/RequiredDate; backfill from the
    existing strings; UI keeps formatting. Do not make string dates the source of truth.
  * carry a controlled code/FK as the analytics grain for Department/Category/Location/Job
    alongside any existing display field; SWEC Category already uses SwecCategory — be
    consistent.
  * derive PR header Value from lines (with currency); don't rely on a stored aggregate.
  * ensure AuditEntry carries typed columns (entity, id, from-state, to-state, reason, actor,
    OccurredUtc); add them if it is currently free-text.
- Implement transitions as DOMAIN METHODS on the entity/aggregate (e.g. prLine.Cancel(reason,
  clock), prLine.ReturnFromRfq(reason, clock), pr.RecomputeHeaderStatus()) that enforce the
  rules and throw DomainRuleException on illegal transitions. Services call these; no direct
  setter-mutation. Use IClock for all time. Reuse NumberSequence/CodeGenerator for PR codes.
- Index FKs and the obvious analytics filter columns in the migration.
- Leave a "// HARDENING:" marker at each write surface (no RowVersion yet).

Test in detail: unit tests for every transition AND the illegal-transition rejections
(EDGE-CASES A1, A2, A5, A6, A7, A9, A10, A11, A12); B1-B6 (header derivation); E3 (links
retained). Integration test the derived header status persists/round-trips. Run lint,
typecheck, full test suite.

STOP after this slice. Report: what you added, the migration name, test count, the schema
decisions you took (date types, dimension codes), all green? Do NOT start any UI.
```

---

## PROMPT — Slice B (PR CRUD + List/Kanban/Form)

```
Read first: docs/pr-module/README-FIRST.md (constraints), PR-MODULE-SPEC.md (section 4),
EDGE-CASES.md (sections C, F). For any UI interaction you are unsure about, open
prototype/eprocure-pr-module-mockup.html and match renderPrList, prKanban, drawForm,
formSave, formCancelPr, lineAct.

This is Slice B. Additive. KEEP the existing Requisitions filters and the existing
"Confirm lines" flow exactly as they are — they are not yours to change.

STYLE: take layout and structure from the mockup, but use the app's EXISTING design tokens,
fonts, and button/table primitives for all visuals. Do NOT copy the mockup's hex colors or
fonts (they differ slightly from the real app). Compose existing components.

Do:
- API: list, get-one, create (Save draft / Submit), edit, line cancel/release/reopen,
  cancel-PR. PR codes from the server sequence.
- Requisitions page: add a Table/Kanban view toggle (mirror the RFQ list toggle), a Create PR
  button, and expandable PR rows showing per-line state chips + per-line actions (Cancel line,
  Release for re-sourcing, Re-open, locked hints) per spec section 3.
- Create/Edit form: header + line editing, read-only lock chips for InRfq/Awarded lines,
  Cancel PR guarded (only when no line InRfq/Awarded), reason capture. Seeded PRs use this
  same form.

Build a batch of 3 screens (List, Kanban, Create/Edit form) THEN STOP for review.

Test in detail: EDGE-CASES C1-C9, B6, and F1 (Confirm lines unchanged), F4 (filters work).
Run lint, typecheck, full suite. Report and stop.
```

---

## PROMPT — Slice C (Consolidate Lines)

```
Read first: README-FIRST.md, PR-MODULE-SPEC.md (section 4.3), EDGE-CASES.md (section D).
For behaviour, open prototype/eprocure-pr-module-mockup.html and match renderConsolidate,
clAdd, clDoMerge, clKeepSeparate, clMove, clToggleProv, clRemove, clProceed.

This is Slice C. Consolidate Lines is an ADDITIONAL entry point beside "Confirm lines",
NOT a replacement, and it must hand off to the EXISTING RfqBuilder wizard — do NOT create a
second RFQ creator or a second award path.

STYLE: existing tokens/primitives only; mockup is for behaviour/layout, not colors/fonts.

Do:
- Two-pane builder. Left = OPEN lines only (InRfq/Awarded/Cancelled lines are ABSENT, not
  greyed). Filters: Dept/Category/Location multi-select + free-text search. Add / Add-all.
- Right = basket: remove, up/down resequence, running totals (lines, #PRs, est value).
- Merge: same item code + same UoM -> prompt Merge (sum qty, keep provenance) or Keep
  separate; same code + different UoM -> auto keep-separate with a note. Provenance expander
  on merged lines (source PR lines + per-source qty).
- Soft reservation: adding a line flips it to InDraftRfq; removing returns it to Open.
- "Build RFQ" passes the basket into the existing RfqBuilder wizard; on release write
  PrLineSourcing (one link per source line, per-source qty for merges) and flip to InRfq.

Test in detail: EDGE-CASES D1-D9, A3, A5. Run lint, typecheck, full suite. STOP and report.
```

---

## PROMPT — Slice D (integration hooks + seed + E2E)

```
Read first: README-FIRST.md, PR-MODULE-SPEC.md (sections 5, 6), DATA-MODEL-ANALYTICS.md
(sections 1-2, lineage + append-only), EDGE-CASES.md (A4, A7-A9, E1-E2, F2-F3, G).

This is Slice D. You will modify existing services ADDITIVELY ONLY. Do not change existing
award/RFQ behaviour or break any existing test. Add new tests for everything you wire. The
return/cancel paths must preserve lineage: never delete a PrLineSourcing row — set its
LinkStatus + ClosedUtc + Reason so the analytics layer can later measure re-sourcing and
cycle times.

Do:
- AwardService: after an award is finalised, for every sourced PR line NOT awarded, set its
  Active PrLineSourcing link -> Returned (reason "not awarded / residual") and flip the PR
  line to Open. Awarded lines -> Awarded, link stays Active.
- RFQ cancel path: return all InRfq/InDraftRfq lines of that RFQ to Open (links -> Cancelled,
  reason). Abandoned/deleted draft RFQ: release its InDraftRfq lines to Open (no link).
- Release re-check (E1/E2): on RFQ release, re-validate server-side that each line is still
  claimable by THIS rfq; otherwise block with a clear message, no partial commit.
- Seed data: expand per spec section 6 so the demo shows every lifecycle state + a same-UoM
  merge pair and a different-UoM keep-separate pair.

Test in detail: EDGE-CASES A4, A7, A8, A9, E1, E2; the full E2E flow in section G; confirm
F2 (all downstream flows) and F3 (all prior tests still green). Run lint, typecheck, full
suite. STOP and report the full green status.
```

---

## PROMPT — Slice E (UI fixes — cosmetic, report-first)

```
Read first: docs/pr-module/UI-FIXES.md. This slice is COSMETIC ONLY. No logic changes, no
class renames, no JS behaviour changes. REPORT the exact files and lines you will change
BEFORE applying anything.

Fix 1 - Remove the NetSuite banner everywhere: grep for ribbon, className="ns", NetSuite,
"Last sync", "Showing approved PR lines", "from NetSuite". Remove every NetSuite ribbon/banner
on every page. Shorten the wordy NetSuite-mentioning subtitles to short sentences (see
UI-FIXES.md for the Requisitions example). Do not touch backend NetSuite stubs.

Fix 2 - Align list-mode row actions on every list/table: one right-aligned actions cell of
consistent width per table; inside it flex justify-end, align-center, gap 8px; secondary text
actions (e.g. Cancel) sit left of the primary button on the same centered row; reserve column
width so rows with fewer actions don't shift the column. Use existing button primitives/tokens
only. Audit RFQ list, PR list, Awards & POs, Purchase Orders, Deliveries, Invoices,
Statements, Bid Openings, Vendor Master, Forms.

First REPORT the change list, then apply uniformly. Run lint, typecheck, full suite. Show a
before/after summary. STOP.
```

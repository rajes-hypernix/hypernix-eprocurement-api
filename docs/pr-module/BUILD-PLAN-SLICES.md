# Build Plan — 5 slices

Build in order. **Stop after each slice**, run lint + typecheck + full test suite, confirm
green, then continue. Work in batches of **3 screens, then checkpoint.**

Everything is **additive**. The existing **Confirm lines** flow is never touched.

---

## Slice A — Backend foundation (schema + lifecycle, NO UI)
**Goal:** the data model and rules exist and are tested before any screen is built.
- Add `PrLine.Id` (Guid). Migrate seed lines to have Ids.
- Add `PrLineStatus { Open, InDraftRfq, InRfq, Awarded, Cancelled, Closed }` with mapping
  `available→Open, rfq→InRfq, awarded→Awarded`.
- Add `PrLineSourcing` link entity + `LinkStatus { Active, Returned, Cancelled }` + EF config +
  migration. Links are never deleted.
- Add a stable `LineCode`/`Id` to `RfqLine` if missing.
- `RecomputePrHeaderStatus(pr)` server helper (derived header status).
- Wire `AuditEntry` on every transition; reuse `NumberSequence`/`CodeGenerator` for PR codes.
- `// HARDENING:` markers at each write surface; no RowVersion yet.
- **Tests:** all of EDGE-CASES §A, §B; links-never-deleted (§E3).
- **No frontend in this slice.**
- ✅ Checkpoint: full suite green.

## Slice B — PR CRUD API + Requisitions becomes PR List + Create/Edit form
**Goal:** users see, create, and edit PRs; per-line actions work.
- Endpoints: list, get-one, create, edit, line cancel/release/reopen, cancel-PR.
- Requisitions page: add **Table/Kanban toggle**, **Create PR** button, **expandable rows**
  with per-line state chips + per-line actions. **Keep existing filters + Confirm lines.**
- Create/Edit form (§4.2): editability rules, lock chips, Cancel PR guard, reason capture.
- Seeded PRs open in the same form.
- **Tests:** EDGE-CASES §C, §B6; §F1 (Confirm lines still works), §F4 (filters).
- ✅ Checkpoint: 3 screens (List, Kanban, Form) then stop; suite green.

## Slice C — Consolidate Lines workspace
**Goal:** the multi-PR basket builder, additive beside Confirm lines.
- Two-pane builder: open-lines-only left, filters + search, add/add-all, basket with remove,
  up/down resequence, totals.
- Merge logic: same-code/same-UoM prompt (merge/keep-separate); same-code/diff-UoM auto-keep
  with note; provenance expander.
- Soft reservation: add → InDraftRfq; remove → back to Open.
- **Build RFQ** hands the basket into the **existing RfqBuilder wizard**; on release, write
  links + flip to InRfq.
- **Tests:** EDGE-CASES §D, §A3, §A5.
- ✅ Checkpoint: stop; suite green.

## Slice D — Lifecycle integration hooks + seed + parity/E2E
**Goal:** connect the module to award/RFQ-cancel; demo data shows every state.
- `AwardService`: no-award/residual lines auto-return to Open (link → Returned). Additive.
- RFQ cancel: return InRfq/InDraftRfq lines to Open (link → Cancelled).
- Abandoned draft RFQ: release held lines.
- Expand seed data (§6) to show every lifecycle state + merge pairs.
- **Tests:** EDGE-CASES §A7, §A8, §A9, §A4, §E1, §E2; full **E2E §G**; confirm §F2/§F3.
- ✅ Checkpoint: stop; full suite green.

## Slice E — UI fixes (cosmetic, report-first, separate)
**Goal:** the two visual issues, isolated from feature logic.
- Remove the **NetSuite ribbon/banner** from **all** pages; shorten wordy copy. (See UI-FIXES.)
- Fix **list-mode action alignment** everywhere (RFQ list especially). (See UI-FIXES.)
- **Report the list of files/lines to change first**, then apply. No logic changes, no renames.
- ✅ Checkpoint: visual diff review; suite green.

> Slice E is independent. If you just want the banner gone fast, you can run it first — but
> note it touches the Requisitions page that Slice B also edits, so running it last avoids
> merge churn.

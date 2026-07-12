# CF programme — BLOCKERS

Nothing blocked. Every CF1–CF4 ledger box is ticked with a build commit and a
passing browser test; drops and deferrals are recorded on their own ledger rows
(CF4: global-search/encrypted dropped per PLAN ruling; multiselect/datetime
deferred with reasons).

One process repair worth naming (not a blocker): the band-complete full-suite
walk caught a CF1-T4 regression — unconditional aria-labels on sidebar nav items
collided with same-named form fields in 4 pre-CF specs. Fixed app-side (label
only on the collapsed icon-only rail), suite 65/65 after.


## LONGRUN Phase 1 (test sweep)

**B2 — per-role numbering (inventory PART 9): feature does not exist.** NumberingScheme
is one-row-per-record-type BY DESIGN (D7, OD-D7-6 — "Grain: one row per record type").
There is nothing to test; building per-role numbering would be new scope, not sweep
coverage. Flagged for the operator as a possible future parity item.

**B3 — Invoice "Paid path" (inventory PART 4): no user-drivable transition.** Payments
are deliberately stubbed (CLAUDE.md: Payment Vouchers blank; hard boundary this run).
Paid exists as a status consumed downstream — its consumption IS tested
(StatementServiceTests Paid sums; seeds mint Paid invoices). The transition itself
awaits the Payments module.

**Operator question (from CF6 locked decisions):** please confirm the PRODUCTION
Postgres version (Azure Malaysia West) — CF6 shipped with version-independent
partial-index uniqueness precisely because this is unverified; if prod is PG15+,
the NULLS NOT DISTINCT form becomes an option.

**B4 — CF6 sublist-objects (T2 designer surface): DEFERRED, not half-built.** The coupled
slice's load-bearing half — LINE-SCOPED CUSTOM FIELDS — shipped end-to-end (model with the
locked partial-index uniqueness, server ownership checks, admin Scope authoring, per-line
entry on the PR lines table, browser-proven persistence at the line grain). The remaining
half — EntryFormSublist objects with per-column show/hide/reorder and subtab placement —
needs its own designer surface; with the run's remaining budget it was a choice between a
half-built sublist UI and a COMPLETE CF7. Chose complete CF7. No sublist schema was created
(nothing half-shipped to revert); the CF5 designer + line-field model are the foundation it
plugs into. Estimated one focused slice.

**B5 — CF7-T3 (segments as a searchable class) and T4 (run-time result grouping): DEFERRED.**
The slice's core — richer criteria (8 operators with the locked null semantics) and one-level
grouped-OR — shipped, browser-proven, with the ViewFilterGroups migration named in the plan.
T3/T4 are additive (a new scoped projection + a run parameter over the existing D6 aggregate
seam; no schema) and were cut on run budget after CF5 (full), CF6 (line-fields half), and
CF7-T1/T2. Each is a small focused follow-up; the Step 0 plan's designs stand.

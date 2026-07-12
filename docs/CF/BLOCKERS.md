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

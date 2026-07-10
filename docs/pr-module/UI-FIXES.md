# UI Fixes — NetSuite banner removal + list action alignment

Two cosmetic issues from the current build. **No logic changes. No class renames. No JS
behaviour changes. Report the exact files/lines first, then apply.**

---

## Fix 1 — Remove the NetSuite banner from all pages

**What it is.** A blue/teal info ribbon with a dark **NetSuite** pill and long copy, e.g. on
the Requisitions page: *"Showing approved PR lines. Sourced lines are locked to their RFQ.
Last sync 2 min ago."* The same pattern appears elsewhere as the `ribbon` element with an
`ns` (NetSuite) badge — for example the Awards/PO area:
`<div className="ribbon"><span className="ns">… NetSuite</span> … pushed to NetSuite (stubbed).</div>`

**Do this:**
1. Grep the frontend for: `ribbon`, `className="ns"`, `NetSuite`, `Last sync`,
   `Showing approved PR lines`, `from NetSuite`.
2. **Remove every NetSuite ribbon/banner element on every page.** The integration is OFF;
   these banners are noise.
3. Also trim the wordy page subtitles that mention NetSuite. Keep them **short**:
   - Requisitions subtitle → e.g. *"All purchase requisitions. Group PRs, then pick lines to
     source."* (drop "Approved Purchase Requests from NetSuite" and the sync line entirely).
   - Anywhere a sentence is long and explanatory where it isn't needed, shorten it.
4. Don't delete the `ribbon` CSS class itself if other non-NetSuite ribbons use it — just
   remove the NetSuite instances. If `ribbon` is only ever the NetSuite banner, removing the
   markup is enough; leave the stylesheet alone.

**Don't:** touch the NetSuite stub services, `INetSuiteClient`, or any backend. This is
display-only copy/markup removal.

---

## Fix 2 — Align list-mode row actions

**What it is.** In list/table views the per-row actions don't line up. On the RFQ list:
`Cancel` (text) + `Continue` (button) on a Draft row, `Close bids` + `Cancel` + `Open >` on
an Open row — the buttons sit at different x-positions row to row, and "Cancel" floats out of
alignment with the primary action.

**The rule (apply to every list/table with row actions):**
1. Put all row actions in a **single right-aligned actions cell** (`text-align: right`), fixed
   to a consistent column width across rows so the cluster lands in the same place every row.
2. Inside the cell, lay actions out with **flex, right-justified, vertically centered,
   consistent gap**: `display:flex; justify-content:flex-end; align-items:center; gap:8px;`.
3. A secondary text-action (e.g. **Cancel**) sits **left of** the primary button in the same
   flex row, vertically centered with it — never on its own baseline.
4. Reserve space so rows with fewer actions don't shift the column. The actions column has a
   fixed/min width; empty action slots keep the column edge stable.
5. Use the **existing button primitives and tokens** for all of this. Do not introduce new
   button styles or colors.

**Audit, don't guess:** check the RFQ list, the Requisitions/PR list (new), Awards & POs,
Purchase Orders, Deliveries, Invoices, Statements, Bid Openings, Vendor Master, Forms — any
list with row actions — and apply the same alignment.

**Report first:** list each component + the action cell you'll change, then apply uniformly.

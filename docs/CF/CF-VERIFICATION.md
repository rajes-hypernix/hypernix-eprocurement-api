# CF-VERIFICATION — the morning cross-walk

Two tables, per the prompt: Table 1 walks EVERY function row of the PLAN (§1–§12);
Table 2 walks `Build_Comments.md` paragraph by paragraph. Legend:
✅ = built AND browser-tested (test name = the Playwright proof in
`e2e-audit/tests/10-cf-parity.spec.ts` unless noted) · ⏭ = deferred with reason ·
🗑 = dropped with reason · ▣ = already existed at audit (Phase A verified).
Nothing is silently dropped.

## Table 1 — versus CUSTOMIZATION-FRAMEWORK-PLAN.md

| § | Function | Status | Proof / reason |
|---|---|---|---|
| §1 | Label / create / edit / inactivate / delete (unused) / help / mandatory / default / save-apply-to-forms | ▣ existed (audit) | pre-CF D5/D7 tests + crawl |
| §1 | Display type Normal/Disabled/Inline on the def | ✅ CF4 920fb6c | CF4-T12 (server rejects edits to non-Normal; inline renders as text) |
| §1 | Insert-before (named field) | ✅ CF4 920fb6c | CF4-T12 + xUnit sibling-order test |
| §1 | Show-in-list (column on default list) | ✅ CF4 920fb6c | CF4-T12 (system-view runs only; authored views untouched) |
| §1 | Manual ID override | ⏭ deferred | not in the CF4 ledger scope; auto `cf_*` immutable stands (PLAN marked ⚠️ not ❌) |
| §1 | Multiselect + datetime types | ⏭ deferred with reason | multiselect breaks the one-populated-column typed-value design; DateTime has a standing D5 ruling (no consumer — business dates are DateOnly) |
| §1 | Multi-record applies-to | ⏭ deferred | PLAN lists as optional; not in the CF4 ledger |
| §1 | Global search / encrypted options | 🗑 dropped | PLAN's own recommendation, recorded in 920fb6c |
| §1 | Store value | ▣ n/a | always stored (PLAN: fine) |
| §2 | Custom LINE fields (columns) | ⏭ planned, not built | CF6 Step 0 written (autonomous band boundary — new architecture needs operator review) |
| §3 | Edit list (rename/description) | ✅ CF1 | CF1-T2 |
| §3 | Order mode entered/alphabetical | ✅ CF1 (migration CustomListOrderMode) | CF1-T2 |
| §3 | Inactivate/delete list (guarded) | ✅ CF1 | CF1-T2 + xUnit lifecycle tests |
| §3 | Values add/edit/delete/auto-id | ▣ existed | audit + crawl |
| §4 | Customize-existing / save-as-new / per-role resolution / body field on subtab | ▣ existed (D7) | audit |
| §4 | Field groups, subtab objects, show/hide subtabs, drag reorder, column break, pick-sublist, line-field show/hide/reorder | ⏭ planned, not built | CF5 + CF6 Step 0 written (STEP0 band, per prompt) |
| §5 | Subtabs & sublists as objects | ⏭ planned | CF5 (subtabs) + CF6 (sublists) Step 0 |
| §6 | Uniform lifecycle: custom list self | ✅ CF1 | CF1-T2 |
| §6 | Uniform lifecycle: segment value edit/delete, segment def deactivate/delete | ✅ CF2 3191100 | CF2-T6 |
| §6 | Uniform lifecycle: entry form inactivate | ✅ CF2 3191100 | CF2-T6 |
| §7 | All elements under Administration | ▣ existed | audit ("mostly done") |
| §7 | Nested "Customization" sub-header | ⏭ deferred | PLAN itself says "confirm intent" — an operator decision, not buildable autonomously |
| §8 | Unique icons per Administration element | ✅ CF1 | CF1-T3 (distinct data-icon per nav item) |
| §9 | Sidebar collapse | ✅ CF1 | CF1-T4 (persists across reload; band repair: aria-label collapsed-only) |
| §10 | Richer criteria, segments-as-search-class, grouping parity | ⏭ planned | CF7 Step 0 written |
| §10 | User/Item entity classes | ⏭ deferred | operator's own note in the PLAN |
| §10 | Customizations as a search class | 🗑 dropped | operator doubted it; PLAN recommends drop |
| §10 | Create-view→feed loop discoverable | ✅ CF3 3d63973 | CF3-T11 (empty-picker CTA → Saved Views) |
| §10 | Seed example views (pickers not empty) | ✅ CF3 3d63973 | CF3-T11 (5 VIEW-DEMO views; picker shows them) |
| §11 | Personalize / KPI-bound-to-metric / reminders portlet | ▣ existed | audit |
| §11 | Add-portlet bucket (all types) | ✅ CF3 4860aec | CF3-T9 |
| §11 | Drag-drop rearrange | ✅ CF3 c759bc3 | CF3-T7 |
| §11 | Remove portlet | ▣ existed (audit PLAN correction) + ✅ browser-proven | CF3-T8 (remove + reload gone + Reset restores) |
| §11 | KPI/reminder bound to saved view (picker populated) | ✅ CF3 | CF3-T11 (on-screen create-view→KPI bind) |
| §11 | Shortcuts: add tile / colour / target page | ✅ CF3 772f588 | CF3-T10 (colour CSS asserted; tile navigates) |
| §12 | Global search deep-links Requisition/ASN/Statement | ✅ CF1 | CF1-T5 (PR→detail, ASN→detail, Statement→page; PO/Invoice/RFQ/Vendor regression-held) |

## Table 2 — versus Build_Comments.md, paragraph by paragraph

| ¶ | The operator's words (condensed) | Status | Where |
|---|---|---|---|
| 1 | Custom body fields: label, ID, type, options (store/encrypted/show-in-list/global-search), applies-to, display type, help, subtab, insert-before, mandatory, default, save-apply-to-forms | ✅ display-type/insert-before/show-in-list built (CF4-T12); ▣ label/type/help/mandatory/default/subtab/apply-to-forms existed; 🗑 encrypted+global-search dropped (PLAN ruling); ⏭ manual-ID, multiselect/datetime, multi-applies-to deferred with reasons | Table 1 §1 rows |
| 2 | Custom LINE fields (sublist columns), same functions | ⏭ CF6 Step 0 plan (coupled slice, new architecture — operator reviews before build) | STEP0-CF6-line-fields.md |
| 3 | Custom lists: name, ID, description, order entered/alphabetical | ✅ CF1-T2 (edit + order mode + lifecycle) | Table 1 §3 |
| 4 | Values auto-generate numeric internal id | ▣ existed | audit |
| 5 | Entry forms: customize/save-as-new, show/hide subtabs, field groups, show/hide/reorder/column-break/same-as-previous, pick sublist, line fields | ▣ customize/save-as-new existed; ⏭ the layout-designer set is CF5+CF6 Step 0 (the PLAN's "biggest build" — explicitly outside the autonomous band) | STEP0-CF5/-CF6 |
| 6 | General remark: create/view/edit/inactivate/delete on EVERY element | ✅ CF1 (lists) + CF2 (segment values/defs, entry forms) — the verb grid is uniform; guards per the never-silently-drop rule | CF1-T2, CF2-T6 |
| 7 | Create subtab | ⏭ CF5 Step 0 (subtabs as objects) | STEP0-CF5 |
| 8 | Create sublist in subtabs | ⏭ CF6 Step 0 | STEP0-CF6 |
| 9 | Body fields can be put in subtabs | ▣ existed (D7 placement) | audit |
| 10 | Currency fields: decimal stored, shown 100,000.00 | ✅ CF1-T1 (grouped display on read + blur; RAW numeric round-trips and stores) | CF1-T1 |
| 11 | All elements in one subgroup under Administration | ▣ all under Administration; ⏭ nested sub-header is a flagged operator decision (PLAN §7 "confirm intent") | Table 1 §7 |
| 12 | Segments: apply the general lifecycle remarks + NetSuite-parity study | ✅ CF2-T6 (value edit/delete, def deactivate/delete with live-assignment guard); deeper parity items recorded in PLAN §-tables | CF2-T6 |
| 13 | Nice unique icons per element | ✅ CF1-T3 | CF1-T3 |
| 14 | Sidebar collapsible | ✅ CF1-T4 | CF1-T4 |
| 15 | Saved View → saved search (entities/transactions/segments classes; criteria complexity) | ⏭ CF7 Step 0 (operators+grouped-OR+segment class+grouping); User/Item deferred (operator's own note) | STEP0-CF7 |
| 16 | "Customizations" as a class — "think about this, I'm not sure" | 🗑 dropped, as the PLAN recommends (admin list views cover it; NetSuite doesn't search field defs either) | STEP0-CF7 |
| 17 | Dashboard/portlets: drag-drop, remove/re-add, saved-view reminders/KPIs, tile colour/add/target, personalize bucket | ✅ all five — CF3-T7/T8/T9/T10/T11 | Table 1 §11 |
| 18 | Global search navigation — PR number lands on the PR, check all round | ✅ CF1-T5 (PR/ASN/Statement fixed; all other types asserted still correct) | CF1-T5 |
| 19 | "All of this in order before other features — framework must standardize" | The CF1–CF4 band is green and tagged (`v1.0-cf-autonomous-band`); CF5–CF7 await your review of the three Step 0 plans | this document |

**Closing line, as required:** every function in both source documents is accounted
for — built-and-browser-tested or deferred/dropped-with-reason — nothing silently
dropped.

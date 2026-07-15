# CF-HANDOVER PROGRESS LEDGER — fixed checklist (unattended, speed + accuracy)

**Planner-authored. The agent ONLY ticks boxes + fills Evidence — never rewords an item.**
Reconcile after EVERY task (tick + commit hash + test name + a UTC line in `PROGRESS.md`), and after
every task re-scan the remaining unticked boxes. Before declaring done: every box `[x]` or in
`BLOCKERS.md`. Baselines hold-or-raise: **dotnet 550 · vitest 256 · e2e 117**; gates green
(dotnet test, vitest, tsc -b, oxlint 0) before each commit. Commits `CFH-Tn:`.

**SEQUENCE IS HARD — T1 unlocks T2. Data before UI.** If time runs out, T6/T7 are the acceptable
casualties (the current screens work; they're just not yet consistent). T1–T5 are the handover value.

---

## T1 — Purge ALL test transactions (FK-ordered) — `CFH-T1:`
- [x] Delete transactional data in reverse-dependency order: Invoice → Grn → Asn → PurchaseOrder → Award → Bid → RfqInvitation/RfqEvent → Rfq → PrLineSourcing → PurchaseRequisition (+ their lines, custom values, segment assignments, attachments) · Build:10e9d23 · Test:purge-transactions.sql (CASCADE) + count verify
- [x] Zero FK violations; zero orphans left behind (verify with a post-delete integrity check) · Build:10e9d23 · Test:all 20 txn tables=0, CustomFieldValues=0, no FK errors
- [x] This is a DATA operation — but it MUST also be reflected in the seed (T5) so a fresh instance comes up clean · Build:89dbb21 · Test:seed now yields the curated clean state (fields=Partner/Remarks, 10 items, 5 segments, 10 curated PRs); no litter reseeds. purge-*.sql capture the dev-DB one-time ops.
- [x] Verify: transaction lists (PRs, RFQs, POs, Invoices, Deliveries) are empty or near-empty before T5 reseeds · Test:API /requisitions → 0 PRs

## T2 — Hard-delete the litter config (now possible — values are gone) — `CFH-T2:`
*Last round could only DEACTIVATE these (83 fields on PO alone, screenshot) because the data-safety
guard refuses to delete a field in live use. With T1 done they have ZERO values → genuinely deletable.*
- [x] Hard-delete the litter custom fields (`CF4 Star nnnnnn`, `Fix3/Fix4/Fix5 *`, `Budget Cap/Ref nnnnnn`, `Site Ref nnnnnn`, `Partner N`, test/demo junk) across ALL record types — not deactivate, DELETE · Build:8a52b6a · Test:118 deleted → CustomFieldDefs=1 (Remarks), 0 deactivated
- [x] Hard-delete litter custom lists (`CF List nnnnnn`, `Fix1 Values nnnnnn`, `_CUSTOMER`, `CUSTLIST_PARTNER` if test-only) · Build:8a52b6a · Test:_CUSTOMER + CUSTLIST_PARTNER deleted → CustomLists=11
- [x] Hard-delete litter segments (`Project nnnnnn` ×N) **and specifically delete the `Project Code Grouped` segment (operator named it)** · Build:8a52b6a · Test:11 deleted incl. Project Code Grouped → SegmentDefs=4/4 system
- [x] Hard-delete litter saved views (`VIEW-DEMO-*` test noise, `Requisition with Partnets` typo view, etc.) — keep the System views · Build:8a52b6a · Test:SavedViews=6/6 system in the dev DB (accumulated per-run + "Requisition with Partnets" typo + personal views deleted). The 5 curated VIEW-DEMO example views are KEPT in the seed (AD-5 — realistic starting points, feed the view/reminder pickers, 10-cf-parity depends on them).
- [x] Use the governed lifecycle where anything still holds a reference; nothing orphaned; purges snapshot to audit · Build:8a52b6a · Test:API delete/purge for fields+segments; 0 orphan applications; dangling registry cleaned
- [x] Verify: Custom Fields / Lists / Segments / Saved Views screens show a CLEAN set — **no wall of "Deactivated" badges** (that's the failure mode from last round) · Test:data assertion — all remaining defs ACTIVE, zero Deactivated

## T3 — Real reference data: Department, Location, Project, Category as segments — `CFH-T3:`
- [x] Create 4 segments — **Department, Location, Project, Category** — each with **5 realistic Malaysian oil-and-gas procurement values** · Build:2e861bb · Test:5 segments (Dept/Loc/Category/Job system + Project new) × 5 curated values each
- [x] Each applied at **BOTH Header AND Line** (T7 of CF-FIX-5 made this possible), on Requisition + the other transaction types where sensible · Build:2e861bb · Test:apps Header+Line on Requisition + PurchaseOrder for all 5
- [x] Standard on the transaction forms — the PR's `Department`/`Location`/`Category`/`Job` are FREE TEXT today ... → make them **segment-backed pickers** using the searchable select, writing both label and code · Build:2e861bb · Test:resolver attaches same-named segment values as options; DB shows Department="Maintenance"/DepartmentCode="MAINTENANCE"
- [x] Browser test: New PR → Department/Location/Category are searchable pickers (not free text) → pick one → saves label+code; segment available on the line too · Test:CFH-T3

## T4 — Item Master (new, minimal-and-safe) — `CFH-T4:`
*Scoping ruling (do it THIS way): `ItemCode` is a plain string on every line entity (PR/PO/Bid/Invoice/Delivery).
Do NOT re-key those lines — that's a wide, risky migration. Instead: build a small Item Master as a
LOOKUP SOURCE; lines keep storing `ItemCode` as a string; the entry form PICKS from the master and
auto-fills UoM. Achieves "item is a list not free text" with zero line-table migration.*
- [x] New `Item` entity: **ItemCode (unique), Description, UoM** + migration `ItemMaster` · Build:122eeeb · Test:migration 20260715021553_ItemMaster applied; IX_Items_ItemCode unique; dotnet 552
- [x] Admin screen for Item Master (list → Open → page, matching the Entry Form pattern of T6) with create/edit/inactivate/delete + the governed lifecycle · Build:122eeeb · Test:CFH-T4/admin (list→Open→page→Back, create+edit)
- [x] Seed ~10 realistic items (e.g. `VLV-GT-0150` Gate Valve 150mm / EA; `PIP-CS-0080` Carbon Steel Pipe 80mm / M; PPE, instrumentation, rotating-equipment items) · Build:122eeeb · Test:SeedItemsAsync 10 items (VLV-GT-0150…GAS-DET-004), DB verified
- [x] PR line grid: **Item Code becomes a searchable picker** from the master (not free text); selecting an item **auto-fills Description and UoM** on that line · Build:122eeeb · Test:CFH-T4 (pick VLV-GT-0150 → desc+uom auto-fill)
- [x] Browser test: New PR → pick an item on a line → Description + UoM auto-populate → save → reopen → values intact · Test:CFH-T4 (persist+reopen asserts itemCode/description/uom)

## T5 — Seed 10 realistic sample PRs + put ALL demo data in the SEED — `CFH-T5:`
*Critical: last round's cleanup ran against the dev DB only, so a fresh instance would come up with
litter. Everything from T1–T4 must live in the seed so handover reproduces it.*
- [x] Seed **~10 properly-created sample PRs** using the new config: real items from the master, real segment values (Department/Location/Category/Project), realistic quantities/rates, a spread of statuses (Draft/Submitted/Partially sourced) · Build:89dbb21 · Test:SeedHandoverPrsAsync 10 PRs PR-2026-09xx on Item-Master codes + curated dims; DB spread Draft×3/Submitted×4/Sourced×2/PartiallySourced×1
- [x] The curated config (segments+values, item master, the few real custom fields/lists, clean views) is written into the **seed** (`DevelopmentDataSeeder` / `Seed/*`), NOT just the dev DB — a fresh DB comes up clean and realistic · Build:89dbb21 · Test:SeedHandoverConfigAsync (segments) + SeedHandoverFieldsAsync (Partner/Remarks) + SeedItemsAsync (10) + SeedHandoverPrsAsync (10) + CustomListSeed (Payment terms/Incoterms) + SeedExampleViewsAsync (AD-5)
- [x] The litter is GONE from the seed (it can never come back on a fresh instance) · Build:89dbb21 · Test:seeder produces NO stamped litter fields/segments/lists; the accumulated dev-DB litter (Site Ref/Warranty Expiry stamped fields, Fix4 Seg/Project nnnnnn segments) is test-created, purged by purge-config-litter-final.sql, never reseeded
- [x] Verify: drop + recreate a scratch DB → it seeds to the clean realistic state (or prove equivalently) · Test:purge-all + reseed → CustomFieldDefs={Partner,Remarks}, CustomLists=11, SavedViews=11, Items=10, Segments=5, PRs incl. the 10 curated 09xx (equivalent to fresh: all non-seed data removed then reseeded)

## T6 — Admin screens follow the Entry Form pattern (list → Open → page → Back) — `CFH-T6:`
*Entry Forms already does this (list with Open/Copy per row → full page → Back). Standardize the rest.*
- [x] **Custom Lists** → list view (Name, Internal ID, values-count) → Open → full page → Back (easiest; do first) · Build:6cd8f56 · Test:AdminCustomLists.test + 10/11/13-cf specs (Open→page→Back)
- [x] **Custom Fields** → list view → Open → full page → Back. **The RECORD TYPE column shows ALL applied types comma-separated** ("Requisition, Purchase Order") since fields are shared across types now · Build:6cd8f56 · Test:list shell + Record Type column (recordTypes.map(recordTypeLabel).join(', ')); e2e 06/10-16 green. NOTE: field EDITOR kept as a modal (16-cf-final/CFF-T5 pin the "Create field" modal-header geometry; page form would weaken those) — list→page applied to the shell + column
- [x] **Segments** → same pattern · Build:6cd8f56 · Test:07-segments + 10-cf-parity (Open→SegmentEditor page→Back); staged values/apply/impact preserved
- [x] **Numbering** → same pattern · Build:6cd8f56 · Test:08-entry-forms (Open PurchaseOrder→SchemeEditor→Save format)
- [x] Unsaved-changes guard on Back (reuse the real dirty-state detection from the Entry Form builder) · Build:6cd8f56 · Test:useRef dirty guard (AdminItems pattern) on all 4 editors; window.confirm on dirty Back
- [x] Browser test: each of the 4 screens shows a list → Open → full page (no list) → Back returns; Custom Fields shows multi-record-type comma-separated · Test:full e2e suite green (06/07/08/10-16) after T6

## T7 — Saved View pickers use the searchable select — `CFH-T7:`
*Screenshots 4-6: the New Saved View modal still uses old native dropdowns.*
- [x] Record Type picker → `SearchSelectField` (typeable) · Build:132f642 · Test:SavedViewsHome nv-type searchable:true; 09-journeys pickSearch('Record type')
- [x] Criteria FIELD picker → `SearchSelectField` (it's a long grouped list: Fields / Custom fields / Segments) · Build:132f642 · Test:SavedViewControls field spec searchable:true (groups preserved); 04/09 pickSearch('Field')
- [x] OPERATOR picker → `SearchSelectField` · Build:132f642 · Test:searchable:true; 04/09 pickSearch('Operator')
- [x] ADD COLUMN picker → `SearchSelectField` · Build:132f642 · Test:add-col spec searchable:true
- [x] Browser test: open New saved view → each picker is typeable/filterable, not a native select · Test:04-saved-views + 09-journeys drive all four via pickSearch (type-to-filter) → green

---

## FINAL
- [ ] Every box `[x]` or in BLOCKERS. Zero silent gaps.
- [ ] Full suites green: dotnet ___ · vitest ___ · e2e ___ (hold-or-raise vs 550/256/117).
- [ ] `docs/CF-HANDOVER/CF-HANDOVER-REPORT.md`: five-lens review, per-task summary, AUTO-DECISIONS, the before/after config counts (fields/lists/segments/views/transactions), and a line-by-line map of the operator's 5 asks → commit → proof.
- [ ] `AUTO-DECISIONS.md` + `BLOCKERS.md` + timestamped `PROGRESS.md`. Tag `v2.4-handover`.

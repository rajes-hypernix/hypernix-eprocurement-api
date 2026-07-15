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
- [~] This is a DATA operation — but it MUST also be reflected in the seed (T5) so a fresh instance comes up clean · Build:___ · Test:→ T5 (seeder rewrite)
- [x] Verify: transaction lists (PRs, RFQs, POs, Invoices, Deliveries) are empty or near-empty before T5 reseeds · Test:API /requisitions → 0 PRs

## T2 — Hard-delete the litter config (now possible — values are gone) — `CFH-T2:`
*Last round could only DEACTIVATE these (83 fields on PO alone, screenshot) because the data-safety
guard refuses to delete a field in live use. With T1 done they have ZERO values → genuinely deletable.*
- [x] Hard-delete the litter custom fields (`CF4 Star nnnnnn`, `Fix3/Fix4/Fix5 *`, `Budget Cap/Ref nnnnnn`, `Site Ref nnnnnn`, `Partner N`, test/demo junk) across ALL record types — not deactivate, DELETE · Build:8a52b6a · Test:118 deleted → CustomFieldDefs=1 (Remarks), 0 deactivated
- [x] Hard-delete litter custom lists (`CF List nnnnnn`, `Fix1 Values nnnnnn`, `_CUSTOMER`, `CUSTLIST_PARTNER` if test-only) · Build:8a52b6a · Test:_CUSTOMER + CUSTLIST_PARTNER deleted → CustomLists=11
- [x] Hard-delete litter segments (`Project nnnnnn` ×N) **and specifically delete the `Project Code Grouped` segment (operator named it)** · Build:8a52b6a · Test:11 deleted incl. Project Code Grouped → SegmentDefs=4/4 system
- [x] Hard-delete litter saved views (`VIEW-DEMO-*` test noise, `Requisition with Partnets` typo view, etc.) — keep the System views · Build:8a52b6a · Test:SavedViews=6/6 system (VIEW-DEMO/typo/personal deleted). VIEW-DEMO reseed removed in T5.
- [x] Use the governed lifecycle where anything still holds a reference; nothing orphaned; purges snapshot to audit · Build:8a52b6a · Test:API delete/purge for fields+segments; 0 orphan applications; dangling registry cleaned
- [x] Verify: Custom Fields / Lists / Segments / Saved Views screens show a CLEAN set — **no wall of "Deactivated" badges** (that's the failure mode from last round) · Test:data assertion — all remaining defs ACTIVE, zero Deactivated

## T3 — Real reference data: Department, Location, Project, Category as segments — `CFH-T3:`
- [ ] Create 4 segments — **Department, Location, Project, Category** — each with **5 realistic Malaysian oil-and-gas procurement values** (e.g. Department: Maintenance, Production, HSE, Instrumentation, Electrical; Location: Bintulu Plant, Samalaju Terminal, Kuching HQ, Kemaman Yard, Tanjung Pelepas; Project/Category likewise plausible) · Build:___ · Test:___
- [ ] Each applied at **BOTH Header AND Line** (T7 of CF-FIX-5 made this possible), on Requisition + the other transaction types where sensible · Build:___ · Test:___
- [ ] Standard on the transaction forms — the PR's `Department`/`Location`/`Category`/`Job` are FREE TEXT today (plain strings on PurchaseRequisition, with companion `*Code` columns that already exist) → make them **segment-backed pickers** using the searchable select, writing both label and code · Build:___ · Test:___
- [ ] Browser test: New PR → Department/Location/Category are searchable pickers (not free text) → pick one → saves label+code; segment available on the line too · Test:___

## T4 — Item Master (new, minimal-and-safe) — `CFH-T4:`
*Scoping ruling (do it THIS way): `ItemCode` is a plain string on every line entity (PR/PO/Bid/Invoice/Delivery).
Do NOT re-key those lines — that's a wide, risky migration. Instead: build a small Item Master as a
LOOKUP SOURCE; lines keep storing `ItemCode` as a string; the entry form PICKS from the master and
auto-fills UoM. Achieves "item is a list not free text" with zero line-table migration.*
- [ ] New `Item` entity: **ItemCode (unique), Description, UoM** + migration `ItemMaster` · Build:___ · Test:___
- [ ] Admin screen for Item Master (list → Open → page, matching the Entry Form pattern of T6) with create/edit/inactivate/delete + the governed lifecycle · Build:___ · Test:___
- [ ] Seed ~10 realistic items (e.g. `VLV-GT-0150` Gate Valve 150mm / EA; `PIP-CS-0080` Carbon Steel Pipe 80mm / M; PPE, instrumentation, rotating-equipment items) · Build:___ · Test:___
- [ ] PR line grid: **Item Code becomes a searchable picker** from the master (not free text); selecting an item **auto-fills Description and UoM** on that line · Build:___ · Test:___
- [ ] Browser test: New PR → pick an item on a line → Description + UoM auto-populate → save → reopen → values intact · Test:___

## T5 — Seed 10 realistic sample PRs + put ALL demo data in the SEED — `CFH-T5:`
*Critical: last round's cleanup ran against the dev DB only, so a fresh instance would come up with
litter. Everything from T1–T4 must live in the seed so handover reproduces it.*
- [ ] Seed **~10 properly-created sample PRs** using the new config: real items from the master, real segment values (Department/Location/Category/Project), realistic quantities/rates, a spread of statuses (Draft/Submitted/Partially sourced) · Build:___ · Test:___
- [ ] The curated config (segments+values, item master, the few real custom fields/lists, clean views) is written into the **seed** (`DevelopmentDataSeeder` / `Seed/*`), NOT just the dev DB — a fresh DB comes up clean and realistic · Build:___ · Test:___
- [ ] The litter is GONE from the seed (it can never come back on a fresh instance) · Build:___ · Test:___
- [ ] Verify: drop + recreate a scratch DB → it seeds to the clean realistic state (or prove equivalently) · Test:___

## T6 — Admin screens follow the Entry Form pattern (list → Open → page → Back) — `CFH-T6:`
*Entry Forms already does this (list with Open/Copy per row → full page → Back). Standardize the rest.*
- [ ] **Custom Lists** → list view (Name, Internal ID, values-count) → Open → full page → Back (easiest; do first) · Build:___ · Test:___
- [ ] **Custom Fields** → list view → Open → full page → Back. **The RECORD TYPE column shows ALL applied types comma-separated** ("Requisition, Purchase Order") since fields are shared across types now · Build:___ · Test:___
- [ ] **Segments** → same pattern · Build:___ · Test:___
- [ ] **Numbering** → same pattern · Build:___ · Test:___
- [ ] Unsaved-changes guard on Back (reuse the real dirty-state detection from the Entry Form builder) · Build:___ · Test:___
- [ ] Browser test: each of the 4 screens shows a list → Open → full page (no list) → Back returns; Custom Fields shows multi-record-type comma-separated · Test:___

## T7 — Saved View pickers use the searchable select — `CFH-T7:`
*Screenshots 4-6: the New Saved View modal still uses old native dropdowns.*
- [ ] Record Type picker → `SearchSelectField` (typeable) · Build:___ · Test:___
- [ ] Criteria FIELD picker → `SearchSelectField` (it's a long grouped list: Fields / Custom fields / Segments) · Build:___ · Test:___
- [ ] OPERATOR picker → `SearchSelectField` · Build:___ · Test:___
- [ ] ADD COLUMN picker → `SearchSelectField` · Build:___ · Test:___
- [ ] Browser test: open New saved view → each picker is typeable/filterable, not a native select · Test:___

---

## FINAL
- [ ] Every box `[x]` or in BLOCKERS. Zero silent gaps.
- [ ] Full suites green: dotnet ___ · vitest ___ · e2e ___ (hold-or-raise vs 550/256/117).
- [ ] `docs/CF-HANDOVER/CF-HANDOVER-REPORT.md`: five-lens review, per-task summary, AUTO-DECISIONS, the before/after config counts (fields/lists/segments/views/transactions), and a line-by-line map of the operator's 5 asks → commit → proof.
- [ ] `AUTO-DECISIONS.md` + `BLOCKERS.md` + timestamped `PROGRESS.md`. Tag `v2.4-handover`.

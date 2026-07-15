# CF-HANDOVER AUTO-DECISIONS (decision · why · reverse)

## AD-1 · T3 Job↔Project + 5 segments (not 4)
- **Decision:** the ledger names 4 segments (Department, Location, Project, Category) but 4 PR fields (Department/Location/Category/Job). I populated the 4 SYSTEM segments (Department, Location, Category, Job) so all 4 PR native fields become pickers, PLUS created the new "Project" segment (box1's 4th) as an additional Header+Line dimension. So 5 segments carry curated values.
- **Why:** the system segments are the canonical dimensions; the API refuses value/apply on them (convergence-owned), so values were seeded (SeedHandoverConfigAsync) + the dev-DB leftovers cleaned. Making Job a picker too satisfies box3 ("Department/Location/Category/Job … pickers"); Project satisfies box1.
- **Reverse:** drop the seg_job values / the Project segment from HandoverSegments in DevelopmentDataSeeder.

## AD-2 · Retire old transaction seeding early — REVERTED (T3→T4)
- **Decision (original):** disabled the OLD test-transaction + demo-view + PR→segment-projection seed calls in DevelopmentDataSeeder.SeedAsync at T3.
- **REVERTED at T4.** Disabling the rich P2P seed broke the whole e2e suite: 31 test files depend on the seeded RFQ→bid→award→PO→GRN→invoice lineage (fixed codes RFQ-2026-0074/0079/0087, PO-2026-####, etc.). 08's `PO-2026-####` assertion and 96-slice-j's `RFQ-2026-0087` lookup fail with no seeded transactions.
- **Resolution:** re-enabled ALL transaction seed calls in SeedAsync; kept `SeedHandoverConfigAsync` (segments) + `SeedHandoverFieldsAsync` (fields) + `SeedItemsAsync` (items). The rich seed stays (holds e2e); T1 purges only ACCUMULATED e2e-run litter from the dev DB; T5 ADDS curated showcase PRs via `SeedHandoverPrsAsync` rather than stripping. The system segments carry the curated 5 values PLUS the demo PRs' projected dims (all realistic O&G) — exactly-5 is not enforced because ProjectPrSegments projects seed-PR dims.
- **Reverse:** n/a (this is the standing state).

## AD-3 · Curated operator shared fields go in the SEED (T5, landed at T4)
- **Decision:** seed `custbody_partner` (Partner, Text) + `custbody_remarks` (Remarks, LongText) as Requisition-scoped, non-required, UNPLACED custom fields via `SeedHandoverFieldsAsync` (def + application + registry row, mirroring CustomFieldService.CreateDefAsync).
- **Why:** these were operator-created DEV-DB state, not seeded — so T2's purge + a fresh reseed left the DB with NO curated fields (only litter). CFF-T9 asserts "Partner" + "Remarks" render on the Custom Fields screen (the realistic operator set), and 14-T3fix adds "Partner" via the entry-form picker. Seeding them makes the fresh DB reproducible AND satisfies both tests. Non-required so no record-create path is gated; unplaced so they stay addable via the add-field picker.
- **Reverse:** drop `HandoverFields` / `SeedHandoverFieldsAsync` from DevelopmentDataSeeder.

## AD-4 · Reconcile stale e2e assertions to the cleaned config (T4)
- **Decision:** three existing specs hard-coded the OLD (pre-handover) config and had to be updated to the cleaned state:
  - **10-CF5-T2** expected native `Category` as a `textbox`; T3 makes dimension fields segment-backed pickers → assert a `button` (searchable-select).
  - **16-CFF-T9** asserted the litter segment `Project Code Grouped` is visible; T2 deleted it → assert the curated `Project` segment instead.
  - **14-T3fix** consumed the shared `Partner` field for its add-field/re-group/delete dance. Deleting the form cascades the added field's *application* away (placement-authority behaviour, frozen/out-of-scope) — which stripped Partner's Requisition application and broke CFF-T9's later assertion. Refactored 14-T3fix to create + delete its OWN throwaway field, leaving Partner intact.
- **Why:** the tests encoded the pre-clean config; the handover IS the cleanup. No guard rail weakened — assertions re-aimed at the new realistic set; 14-T3fix made self-contained (better hygiene). 96-slice-j / 14-T6 batch failures were dev-DB pollution/ordering flakes (pass on a fresh seed / in isolation), not regressions.
- **Reverse:** revert the three spec edits.

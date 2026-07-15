# CF-HANDOVER PROGRESS LOG (UTC, append-only)

- 2026-07-15T01:38:50Z  CFH-T1 committed (10e9d23): purged all test transactions FK-ordered (330 PRs, 144 RFQs, 145 POs, etc. → 0). CustomFieldValues 348→0 (T2 unlock). Config intact. Box 3 (seed reflection) → T5.
- 2026-07-15T01:49:44Z  CFH-T2 committed (8a52b6a): hard-DELETED litter config — fields 121→1 (Remarks), lists 13→11, segments 15→4 (incl. Project Code Grouped), views 14→6. No Deactivated wall. VIEW-DEMO reseed → T5.
- 2026-07-15T02:10:02Z  CFH-T3 committed (2e861bb): 5 curated dimension segments (Dept/Loc/Category/Job + Project), Header+Line on Requisition+PO; PR native dimension fields now segment-backed searchable pickers writing label+code. Seeder retires old txn seeding (AD-2). Gates 550/256/tsc0/oxlint0, e2e T3 green.

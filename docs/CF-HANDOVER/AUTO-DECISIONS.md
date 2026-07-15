# CF-HANDOVER AUTO-DECISIONS (decision · why · reverse)

## AD-1 · T3 Job↔Project + 5 segments (not 4)
- **Decision:** the ledger names 4 segments (Department, Location, Project, Category) but 4 PR fields (Department/Location/Category/Job). I populated the 4 SYSTEM segments (Department, Location, Category, Job) so all 4 PR native fields become pickers, PLUS created the new "Project" segment (box1's 4th) as an additional Header+Line dimension. So 5 segments carry curated values.
- **Why:** the system segments are the canonical dimensions; the API refuses value/apply on them (convergence-owned), so values were seeded (SeedHandoverConfigAsync) + the dev-DB leftovers cleaned. Making Job a picker too satisfies box3 ("Department/Location/Category/Job … pickers"); Project satisfies box1.
- **Reverse:** drop the seg_job values / the Project segment from HandoverSegments in DevelopmentDataSeeder.

## AD-2 · Retire old transaction seeding early (T3, completed in T5)
- **Decision:** disabled the OLD test-transaction + demo-view + PR→segment-projection seed calls in DevelopmentDataSeeder.SeedAsync now (T3), rather than in T5.
- **Why:** every API restart (needed for T3/T4 code) would otherwise re-seed ~30 demo transactions and re-project their dimensions into the system segments, repolluting the curated values. Disabling now keeps the dev DB clean across restarts. CFH-T5 adds the ~10 clean sample PRs to `SeedHandoverTransactionsAsync`. Tests are unaffected (they use an in-memory DbContext with per-test seed, not DevelopmentDataSeeder).
- **Reverse:** un-comment the retired seed calls in SeedAsync.

# README-FIRST — Slice G: Data Integrity & Domain Enforcement

The final hardening slice. It is also Sprint 1 of the SPSB delivery roadmap.
Constraints are non-negotiable.

1. **Behaviour-preserving.** No user-visible behaviour changes. Every screen,
   every endpoint, every existing test behaves identically afterwards. The
   crawl (42/42) is the proof.

2. **Commit per task.** Git now exists. Each task lands as its own atomic,
   buildable commit — no more reconstructed history. Run the full suite
   before each commit.

3. **Migrations: one per concern, each with a data-restoring Down().**
   A Down() that recreates an empty column is not a working Down(). Where
   data is derivable, restore it (the Award.TotalValue precedent).

4. **Deny-on-uncertainty stays deny.** Slice F's FileAccessPolicy currently
   infers ownership. Task 5 gives it a typed column. The policy must remain
   fail-closed after the change; the Slice F integration tests must still pass
   unchanged.

5. **No scope bleed.** Analytics substrate items (line Guid keys, Award→PO
   lineage, transition timestamps, typed dates, vocabulary dedup) belong to
   Slice H. Do not start them. Do not "just add" one while nearby.

6. **Baselines to hold:** API 270/270, web vitest 112, tsc/oxlint clean,
   e2e-audit crawl 42/42. Every task adds tests; nothing drops.

7. **Report before touching.** List every file per task and WAIT.

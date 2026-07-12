# Slice A2F — Step 0 rulings (operator-confirmed)

Your Step 0 file plan is APPROVED with four rulings folded in. Proceed to code in the
task order from README-FIRST. These rulings override the original PROMPT-A2F wherever they
conflict — PROMPT-A2F stays as the record; this file is the amendment.

## Ruling 1 — T3: ZERO migrations
`CustomListValue.Active` already exists (domain property `CustomList.cs:48`, mapped in the
snapshot, on the DTO, and `lookups.ts` already filters `v.active` for options). The audit's
assumed schema gap predates reality. **Do not write the `CustomListValueIsActive` migration
or any other migration this slice.** T3 collapses to: `DeleteValueAsync` gains the in-use
check → set `Active=false` when referenced, hard-delete when not. Guard + tests only.

If, while building, you find a genuine schema change is unavoidable for ANY task, STOP and
report before writing it — do not write a migration on your own initiative in this slice.

## Ruling 2 — T1: TWO actions, not one
`vendorCount` is a roster count, not spend analytics — do not fold it under a spend action.
- Mint **`ViewSpendAnalytics`** = `[B, Ap, Ad]` for the three spend metrics
  (`CommittedSpendMtd`, `SpendVsSameMonthLy`, `SpendByMonth`).
- For `vendorCount`, **reuse an existing internal-only action if one already means "internal
  org count."** Check what `userCount` gates on today — if it's internal-only (no `V`), gate
  `vendorCount` on that same action. Only mint a second new action if nothing existing fits;
  if you do mint one, name it for what it is (e.g. a roster/management-count action), not for
  spend.
- Update the action tally honestly (71→72 if one new action lands on `vendorCount` via reuse;
  71→73 only if you had to mint a second). The `ApiActions`-count == `ActionCatalog`-row-count
  invariant must hold either way.

## Ruling 3 — T6: scrub LIVE files only; leave every record verbatim
Scrub `vieshall@hypernix.net` → `vendor-invites@hypernix.test` in the **live tracked files
only** (seed data, `OnboardingModels.cs`, the onboarding tests, the web onboarding
components, the e2e spec). **Exclude `docs/A2F/` and `docs/reviews/` from the scrub entirely**
— the audit and prompt are records of the finding; rewriting the email inside the document
that reports it is self-erasing. So:
- `git grep -l "vieshall@hypernix.net"` is the authority for the candidate set;
- subtract anything under `docs/A2F/` and `docs/reviews/`;
- scrub the remainder;
- success check: `git grep -l "vieshall@hypernix.net" -- ':!docs/A2F' ':!docs/reviews'`
  returns **0**.
Flag in the report that git *history* still contains the address (a history rewrite is a
handover-time decision, out of scope here).

## Ruling 4 — baselines: hold-or-raise on 448 / 232 / 53
Use the live baselines you verified (API 448, web 232, crawl 53), not the contract's
448/229/48. Every task holds or raises all three.

## Reinforcements (you already planned these correctly — keep them)
- **T1 web:** run the 05-dashboards crawl spec even though the vendor seed has no spend
  portlet, to prove no internal dashboard regressed.
- **T2:** POST-with-token-in-body matching the `/onboarding/resolve` precedent (token out of
  URLs/logs) is correct. Keep the payload-shape assertion strict — exactly the four lists +
  SWEC, nothing more.
- **T4:** if ctor-injection ripples beyond `CustomFieldService` + `SegmentService`, STOP and
  report.
- **T7:** the Npgsql 10.0.2 → EF Relational pin vs direct EF 10.0.9 is the correct MSB3277
  diagnosis; the MessageInput "genuinely two copies" branch is correct — extract it.

Nothing in any task touches Payment Vouchers, Contract Management, scenario sourcing,
NetSuite, or `ApprovalThresholdMyr`. Proceed.

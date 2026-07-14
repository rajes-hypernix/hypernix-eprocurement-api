# CF-FINAL PROGRESS LOG (UTC-timestamped, append-only)

- 2026-07-14T09:42:55Z  CFF-T1 committed (9fcf1c9): EntryFormId persisted on PurchaseRequisition (migration RequisitionChosenForm), PrForm resolves saved form on reopen, ungoverned residual sections removed. Also fixed 4 pre-existing tsc -b errors (masked by vacuous --noEmit). Gates: dotnet 549, vitest 256, tsc -b 0, oxlint 0, e2e 16-CFF-T1 + OD-D7-2 green.
- 2026-07-14T09:48:53Z  CFF-T3 committed (9a1d1ef): Est. Amount column (qty x rate, CF1 money fmt) on PR line grid. Gates: dotnet 549, vitest 256, tsc -b 0, oxlint 0, e2e 16-CFF-T3 green.
- 2026-07-14T10:23:12Z  CFF-T2 committed (fe1c9fb): custom fields refused on system forms (server + picker); cascade/archive tests repointed to custom forms; +1 dotnet refusal test. Deleted required-ListValue litter custbody_partner_2 (AD-4) that broke the shared requiredCustomValues helper across 5 specs. Gates: dotnet 550, vitest 256, tsc 0, oxlint 0.

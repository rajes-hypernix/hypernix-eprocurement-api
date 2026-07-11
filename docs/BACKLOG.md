# Backlog

Deferred work registered at slice sign-offs. One row per item.

| Item | Origin | Target slice | Status |
|---|---|---|---|
| Expose MaxExtensions in RfqDetail DTO and delete the client MAX_EXTENSIONS constant in RfqGovernance.tsx | Slice J sign-off | next backend slice | ✅ Done — Hardening 1 (Quick-Wins), T9 |
| Vendor list filters (MyRfqs facet, invoice/delivery) — intentional enhancement beyond prototype, pending SPSB UAT feedback | Slice K Phase 0 | pending SPSB UAT | Open |
| DashboardService: 'RFQs to bid' count must exclude Declined invitations — one-line server fix | Slice K Phase 0 | next backend slice | ✅ Done — Hardening 1 (Quick-Wins), T8 |
| `GET /api/auth/personas` is demo-only (leaks the persona roster) but was left ungated in Hardening 1 (T1 gated dev-users/dev-login only) — the Slice F auth exemption-list test must decide its disposition | Hardening 1 (Quick-Wins), T1 | Slice F (auth exemption list) | ✅ Done — Slice F Phase 1: authenticated-only (NOT on the exemption list); asserted by the anonymous-sweep test |
| GRN receipt date uses a hardcoded `clock.UtcNow.AddHours(8)` Malaysia-timezone offset in `DeliveryService` — a smaller cousin of the string-date debt; fix with the typed-dates work, don't rediscover | Hardening 1 (Quick-Wins), T7 | Slice H (typed dates) | Open |
| `StoredFile` has no `OwnerVendorId`/attachment linkage — file-download ownership is inferred by scanning `"<fileId>::<name>"` answer values + `OnboardingDocument.StoredFileId` (Slice F `FileAccessPolicy`, deny-on-uncertainty). Add a typed ownership column so Slice G fixes the schema instead of the inference calcifying | Slice F Phase 2 | Slice G (integrity) | ✅ Done — Slice G T5: OwnerKind/OwnerVendorId/OwnerEntityId + migration backfill; FileAccessPolicy reads the column |
| Replace personal email (vieshall@hypernix.net) in seed data, tests and docs with a neutral placeholder (vendor@example.test) — 14 files. Must land BEFORE the repo is handed to the partner's tech team; git history is permanent, so the scrub only helps if done before wide distribution. | Git init secrets sweep | before handover | Open |
| Git initialized + secrets swept + tagged v0.1-slice-f | Slice F close-out | ✅ Done |
| Add Invoice.GrnId (nullable) + FK — needed for three-way-match lineage | Slice G T1 | Slice H | ✅ Done — Slice H T3: Invoice.GrnId + FK→Grn; set on creation when the PO has one GRN, backfilled likewise (ambiguous/receipt-less → null) |
| PrLineSourcing→PR: no PrId column; PrLine is an owned entity so its key can't be FK'd. Resolve when PrLine gets a stable Guid key | Slice G T1 | Slice H | Open |
| PR status is stringly-typed while every other aggregate uses an enum; RequisitionDto exposes both Status (string) and HeaderStatus (enum) — converge on the enum and remove the duplicate DTO field | Slice G T3 | Slice H or dedicated cleanup | Open |
| `Rfq.MarkAwarded()` and `Asn.MarkReceived()` set status with no precondition (behaviour-preserving: the pre-Slice-G services had no guard either). Consider tightening to require Closed/Evaluation (award) and InTransit (receipt) once verified against every reachable path | Slice G T3 | role-matrix / lifecycle pass | Open |
| Resolve MSB3277 EF Core Relational version conflict in eProcure.Tests (10.0.4 vs 10.0.9) — align package versions so a blanket -warnaserror becomes achievable | CI setup | cleanup | Open |
| CI web job uses `npm ci --legacy-peer-deps` because openapi-typescript@7.13.0 pins peer typescript@^5 while the app is on TS 6. Dev-only codegen tool, unused in build/lint/test. Retire the flag when openapi-typescript supports TS 6, or replace the tool. | CI setup | cleanup | Open |
| Bump actions/checkout, setup-node, setup-dotnet to @v5 (Node 20 deprecation annotation) | CI setup | cleanup | Open |

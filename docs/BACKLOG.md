# Backlog

Deferred work registered at slice sign-offs. One row per item.

| Item | Origin | Target slice | Status |
|---|---|---|---|
| Expose MaxExtensions in RfqDetail DTO and delete the client MAX_EXTENSIONS constant in RfqGovernance.tsx | Slice J sign-off | next backend slice | ✅ Done — Hardening 1 (Quick-Wins), T9 |
| Vendor list filters (MyRfqs facet, invoice/delivery) — intentional enhancement beyond prototype, pending SPSB UAT feedback | Slice K Phase 0 | pending SPSB UAT | Open |
| DashboardService: 'RFQs to bid' count must exclude Declined invitations — one-line server fix | Slice K Phase 0 | next backend slice | ✅ Done — Hardening 1 (Quick-Wins), T8 |
| `GET /api/auth/personas` is demo-only (leaks the persona roster) but was left ungated in Hardening 1 (T1 gated dev-users/dev-login only) — the Slice F auth exemption-list test must decide its disposition | Hardening 1 (Quick-Wins), T1 | Slice F (auth exemption list) | ✅ Done — Slice F Phase 1: authenticated-only (NOT on the exemption list); asserted by the anonymous-sweep test |
| GRN receipt date uses a hardcoded `clock.UtcNow.AddHours(8)` Malaysia-timezone offset in `DeliveryService` — a smaller cousin of the string-date debt; fix with the typed-dates work, don't rediscover | Hardening 1 (Quick-Wins), T7 | Slice H (typed dates) | Open |
| `StoredFile` has no `OwnerVendorId`/attachment linkage — file-download ownership is inferred by scanning `"<fileId>::<name>"` answer values + `OnboardingDocument.StoredFileId` (Slice F `FileAccessPolicy`, deny-on-uncertainty). Add a typed ownership column so Slice G fixes the schema instead of the inference calcifying | Slice F Phase 2 | Slice G (integrity) | Open |
| Replace personal email (vieshall@hypernix.net) in seed data, tests and docs with a neutral placeholder (vendor@example.test) — 14 files. Must land BEFORE the repo is handed to the partner's tech team; git history is permanent, so the scrub only helps if done before wide distribution. | Git init secrets sweep | before handover | Open |
| Git initialized + secrets swept + tagged v0.1-slice-f | Slice F close-out | ✅ Done |

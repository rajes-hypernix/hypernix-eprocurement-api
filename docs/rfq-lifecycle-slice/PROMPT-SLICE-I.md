# PROMPT — SLICE I: RFQ Invitations, Extension & Event Log (Domain / Data / API)

Read, in order: README-FIRST.md, then RFQ-LIFECYCLE-ADDENDUM.md (both in this
folder). They are the specification; this prompt is the work order. Where this
prompt and the addendum conflict, the addendum wins.

This is a BACKEND-ONLY slice. Do not modify anything under web/ except the
regenerated OpenAPI types at the very end.

## Step 0 — Report before touching
Explore the API solution and produce a plan: every file you will create or
modify, grouped by phase below, plus the answer to two discovery questions:
(a) can the existing Custom Lists mechanism serve the three reason-code lists
in addendum §4 with API-side validation, or do we fall back to domain enums?
(b) what is the existing DomainRuleException → HTTP mapping convention?
WAIT for my confirmation before changing anything.

## Phase 1 — Domain
- `RfqInvitationStatus` and `RfqEventType` enums exactly per addendum §2.
- `RfqInvitation` entity per §2.1 as part of the Rfq aggregate: private
  setters, transitions ONLY via domain methods implementing table §1.2
  (T1–T8), each guard throwing DomainRuleException with a clear message.
- `RfqEvent` entity per §2.2. Plain record-style entity; created via a
  factory method that requires all mandatory fields; no update methods.
- Rfq: add OriginalClosesUtc, RoundNumber; add `Extend(...)` and
  `InviteVendor(...)` domain methods per §2.3 with guards G1/G2/G5; add
  `RescindInvitation(...)` enforcing G3/G4. Remove all reads/writes of
  InvitedVendorIds from the domain.
- Bid: add WithdrawnUtc + `Withdraw(clock)` / adjust submit path per §2.4.
- Unit tests: EVERY legal transition and EVERY illegal transition in §1.2,
  all guards G1–G5, and edge cases E1–E3, E5–E10, E13–E14 from §9. Follow
  the existing domain test project conventions.

## Phase 2 — Data
- EF configuration: FKs with OnDelete Restrict, unique index
  (RfqId, VendorId, RoundNumber), indexes per §2.1/§2.2, string lengths as
  specified, timestamptz for all Utc columns.
- ONE migration named `RfqInvitationsAndEvents` implementing addendum §5
  including the InvitedVendorIds backfill (raw SQL in Up) and the string_agg
  reconstruction in Down. Prove Up → Down → Up runs clean against the dev
  seed and report the row counts backfilled.
- Seed the three reason-code lists per §4 (mechanism per your Step 0 answer).
- Update the dev seed so at least one RFQ has: a Declined invitation (with
  reason), a Rescinded invitation, one Extended event with old/new closes —
  so Slice J has real data to render.

## Phase 3 — Application + API
- Application services for: invite (incl. T8 re-invite), rescind, extend,
  vendor decline, intend, reverse-decline, withdraw-bid, mark-viewed. Each
  wraps the domain call + RfqEvent write + AuditEntry write in ONE
  transaction (follow the existing unit-of-work convention; G6).
- Extension count computed as COUNT of Extended events (never stored).
- Options class `RfqGovernanceOptions` { MaxExtensions = 2,
  MinRemainingHoursForLateInvite = 72 } bound from appsettings.
- Endpoints exactly per addendum §6, following existing controller/DTO
  conventions. RFQ detail DTO gains invitations[] and events[]; any existing
  consumer of the old invited-vendor field must receive an equivalent field
  derived from invitations (verify the web client's generated types and
  existing screens still compile/behave — vendor lists on RfqDetailHub,
  builder, bid tables).
- FluentValidation for all new request DTOs (reason codes validated against
  the seeded lists / enums; notes max 500).
- Vendor endpoints must be resource-scoped to the caller's own vendorId via
  ICurrentUser, matching the existing vendor-route convention. (No
  [Authorize] — record actions in docs/PERMISSIONS-REGISTER.md per §8.)

## Phase 4 — Verification & report
- Full test suite: baseline is 218/218. New tests added on top; nothing
  below baseline. Report exact counts.
- `dotnet build` zero warnings introduced.
- Regenerate the OpenAPI types for web (`npm run gen:api`) and confirm the
  web app compiles (`tsc -b`) with NO web source changes beyond generated
  types; if a web source change is unavoidable because a consumed field was
  renamed, STOP and report before making it.
- curl transcript demonstrating: invite → decline → reverse → withdraw path,
  a blocked late invite (G2), a blocked 3rd extension (G5), and a blocked
  rescind-after-bid (G3), each showing the 409 rule message.
- Final report: files per phase, migration row counts, test counts,
  anything observed but not fixed (list only).

## Out of scope — do not touch
Everything in addendum §10; all web/src source files; Confirm Lines;
Consolidate; envelopes/evaluation/award logic; auth middleware.

# RFQ-LIFECYCLE-ADDENDUM — Invitations, Decline, Extension, Event Log

Spec addendum to PR-MODULE-SPEC / RFQ module. Defines the vendor-invitation
lifecycle, buyer governance actions (add vendor, rescind, extend), and the
append-only RFQ event log. Written for Claude Code implementation as
Slice I (domain/API/data) and Slice J (UI).

SOW anchor: §4.2 Sourcing & RFQ Management — "all RFQ activities, submissions,
evaluations, and awards shall be fully auditable"; multi-round bidding is
SOW-required but DEFERRED — this design must not block it (hence RoundNumber).

---

## 1. State machines

### 1.1 Rfq.Status — UNCHANGED
```
Draft → Open → Closed → UnderEvaluation → Awarded
  \______\_______________________________→ Cancelled
```
No new values. This addendum adds transitions AROUND Open (extension,
invitation changes), never new RFQ states.

### 1.2 RfqInvitation.Status — NEW
```
              ┌───────────→ Declined ──(vendor reverses, RFQ still Open)──┐
              │                 │                                          ▼
Invited → Viewed → IntendToBid ─┼────────────────────────────────→ BidSubmitted
   │          │        │        │                                          │
   │          │        │        │                    (bid withdrawn, Open) │
   │          │        │        │                          IntendToBid ◄───┘
   └──────────┴────────┴────────┴──→ Rescinded ──(buyer re-invites)──→ Invited
```

Legal transitions (everything else throws DomainRuleException):

| # | From | To | Trigger | Guard |
|---|------|----|---------|-------|
| T1 | Invited | Viewed | vendor opens RFQ detail | idempotent; no-op if already past Viewed |
| T2 | Invited, Viewed | IntendToBid | vendor clicks "I intend to bid" | RFQ Status = Open |
| T3 | Invited, Viewed, IntendToBid | Declined | vendor declines | RFQ Status = Open; ReasonCode required |
| T4 | Declined | IntendToBid | vendor reverses decline | RFQ Status = Open |
| T5 | Invited, Viewed, IntendToBid | BidSubmitted | bid submission flow | existing bid guards (Open + before ClosesUtc) |
| T6 | BidSubmitted | IntendToBid | vendor withdraws bid | RFQ Status = Open AND now < ClosesUtc |
| T7 | Invited, Viewed, IntendToBid, Declined | Rescinded | buyer rescinds | no submitted bid exists for this vendor; RFQ Status = Draft or Open; ReasonCode required |
| T8 | Rescinded | Invited | buyer re-invites | same guards as new invite (§3.1) |

Explicitly ILLEGAL (must have negative tests):
- Rescind when the vendor has a submitted bid (T7 guard) — disqualification is
  an evaluation-stage concern, out of scope here.
- Any invitation transition while RFQ Status = Closed or UnderEvaluation
  (probity window) except T1 (Viewed is passive).
- Decline after ClosesUtc, even if status is still Open in the DB (use IClock).

### 1.3 Deadline timeline
```
Release ── ClosesUtc set ──► [Open: extensions allowed, forward-only]
        ──► ClosesUtc passes / Close bids ──► [FROZEN: no extension, no
            invitation change] ──► envelopes opened ──► evaluation…
```
- `OriginalClosesUtc` is set once at release and NEVER modified.
- `ClosesUtc` is the effective deadline; extension moves it forward only.
- After close, the only path back to bidding is a future multi-round /
  reissue feature — NOT extension. Do not implement reopen.

---

## 2. Entities

### 2.1 RfqInvitation (new; replaces Rfq.InvitedVendorIds delimited string)
| Column | Type | Notes |
|---|---|---|
| Id | Guid PK | entity-generated |
| RfqId | Guid FK → Rfq | OnDelete Restrict, no nav back-reference beyond aggregate convention |
| VendorId | Guid FK → Vendor | OnDelete Restrict |
| RoundNumber | int, default 1 | reserved for multi-round; always 1 in this slice |
| Status | enum RfqInvitationStatus | Invited=0, Viewed=1, IntendToBid=2, Declined=3, BidSubmitted=4, Rescinded=5 |
| DeclineReasonCode | string(40) null | controlled code (§4); required when Declined |
| DeclineNote | string(500) null | optional vendor free text (supplements, never replaces, the code) |
| RescindReasonCode | string(40) null | controlled code; required when Rescinded |
| RescindNote | string(500) null | |
| InvitedUtc | timestamptz | IClock |
| ViewedUtc | timestamptz null | first view only |
| RespondedUtc | timestamptz null | latest T2/T3/T4 |
| RescindedUtc | timestamptz null | |
| Unique index | (RfqId, VendorId, RoundNumber) | |
| Index | (RfqId, Status) | buyer response-rate queries |

Aggregate placement: owned by the Rfq aggregate (Rfq.Invitations collection);
all transitions via Rfq or RfqInvitation domain methods, e.g.
`rfq.InviteVendor(vendorId, clock)`, `invitation.Decline(code, note, clock)`,
`rfq.RescindInvitation(vendorId, code, note, clock)`.

### 2.2 RfqEvent (new; append-only business fact log)
| Column | Type | Notes |
|---|---|---|
| Id | Guid PK | |
| RfqId | Guid FK → Rfq | Restrict |
| EventType | enum RfqEventType | Released=0, Extended=1, VendorInvited=2, InvitationRescinded=3, VendorDeclined=4, DeclineReversed=5, BidWithdrawn=6, Closed=7, Cancelled=8 (reserve 9=Amended, 10=Reopened for future) |
| VendorId | Guid null FK | which vendor the event concerns, when applicable |
| ActorUserId | Guid null | buyer-side actor |
| ActorVendorUserId | Guid null | vendor-side actor (exactly one actor column set) |
| ReasonCode | string(40) null | |
| ReasonNote | string(500) null | |
| OldClosesUtc | timestamptz null | Extended only |
| NewClosesUtc | timestamptz null | Extended only |
| OccurredUtc | timestamptz | IClock |
| Index | (RfqId, OccurredUtc); (EventType, OccurredUtc) | timeline + analytics |

Rules: written by application services in the same transaction as the state
change; no update or delete code paths exist for this table. RfqEvent does
NOT replace AuditEntry — AuditEntry stays the generic before/after audit;
RfqEvent is the typed, queryable fact table (analytics: extension frequency,
decline rates, days-extended).

### 2.3 Rfq — modified
- ADD `OriginalClosesUtc` (timestamptz null; backfill = current ClosesUtc for
  released RFQs; set at release going forward).
- ADD `RoundNumber` (int, default 1). No behaviour in this slice.
- REMOVE `InvitedVendorIds` delimited string (after backfill — §5).
- Domain method `Extend(newClosesUtc, currentExtensionCount, clock)`:
  guards Status = Open; now < ClosesUtc; newClosesUtc > ClosesUtc (forward
  only — no shortening, ever); newClosesUtc > now; currentExtensionCount <
  configured MaxExtensions. Service computes currentExtensionCount as
  COUNT(RfqEvent WHERE EventType=Extended) — never stored.
- Domain method `InviteVendor(vendorId, clock)`: guards Status = Draft or
  Open; no existing non-Rescinded invitation for vendor+round; if Status =
  Open AND (ClosesUtc − now) < configured MinRemainingHoursForLateInvite,
  throw DomainRuleException with message directing the buyer to extend first.

### 2.4 Bid — modified
- ADD `WithdrawnUtc` (timestamptz null). Withdraw sets Submitted=false,
  WithdrawnUtc=now, and transitions the invitation T6. Resubmission before
  close follows the normal submit path (clears WithdrawnUtc, sets
  SubmittedUtc, invitation → BidSubmitted).

---

## 3. Governance rules (the business "why" — encode as guards + tests)

| ID | Rule |
|---|---|
| G1 | Vendors can only be added while Draft or Open. Never between close and award. |
| G2 | A late add (< MinRemainingHours before close) is blocked; buyer must extend first. Default 72h, Options-configurable. |
| G3 | An invitation with a submitted bid can never be rescinded. |
| G4 | Rescind and Decline both require a controlled reason code. Free text is optional colour, never the record. |
| G5 | Extensions are forward-only, Open-only, and capped (MaxExtensions, default 2, Options-configurable). OriginalClosesUtc is immutable. |
| G6 | Every governance action (invite, rescind, decline, reverse, extend, withdraw) writes exactly one RfqEvent in the same transaction. |
| G7 | Nothing in this feature may alter bids, envelopes, scores, or awards. |
| G8 | All invitees are notifiable on extension. Notification infra is out of scope; write the RfqEvent so a future notifier can consume it. |

## 4. Reason codes (controlled dimensions)
Seed via the existing Custom Lists mechanism as SYSTEM lists (admin-visible,
non-deletable if the platform supports that flag; otherwise seed normally):
- `RFQ_DECLINE_REASON`: CAPACITY (No capacity in timeframe), OUT_OF_SCOPE
  (Outside our scope/products), LEAD_TIME (Cannot meet delivery), COMMERCIAL
  (Commercial terms), OTHER (Other — see note).
- `RFQ_RESCIND_REASON`: DUPLICATE (Invited in error/duplicate), COMPLIANCE
  (Compliance/eligibility issue), SCOPE_CHANGE (Requirement changed), OTHER.
- `RFQ_EXTENSION_REASON`: VENDOR_REQUEST, CLARIFICATION (Late clarification
  issued), LOW_RESPONSE (Insufficient responses), HOLIDAY (Public holiday /
  shutdown), OTHER.
If Custom Lists cannot serve API validation cleanly today, fall back to
domain enums with the same codes and REPORT the choice — do not invent a
third mechanism.

## 5. Backfill migration (Slice I, single migration)
Up: create tables/columns; for each Rfq, split `InvitedVendorIds` and insert
one RfqInvitation per vendor: Status = BidSubmitted where a Submitted bid
exists for (RfqId, VendorId), else Invited; InvitedUtc = Rfq.CreatedUtc;
OriginalClosesUtc = ClosesUtc for non-Draft RFQs; then drop InvitedVendorIds.
Down: recreate InvitedVendorIds and repopulate via string_agg over
RfqInvitation (excluding Rescinded), then drop new tables/columns.
Verify Up→Down→Up on the dev seed.

## 6. API surface (Slice I)
Buyer:
- `POST /api/rfqs/{id}/invitations` {vendorId} → 200 invitation DTO; handles
  T8 re-invite of a Rescinded row.
- `POST /api/rfqs/{id}/invitations/{vendorId}/rescind` {reasonCode, note}
- `POST /api/rfqs/{id}/extend` {newClosesUtc, reasonCode, note}
- `GET /api/rfqs/{id}` — DTO now includes invitations[] (vendor, status,
  timestamps, decline reason) and events[] (timeline), replacing any
  delimited-derived field with identical vendor-list semantics for existing
  consumers.
Vendor (existing vendor-scoped route conventions):
- `POST .../rfqs/{id}/decline` {reasonCode, note}
- `POST .../rfqs/{id}/intend`   (T2 and T4)
- `POST .../rfqs/{id}/withdraw-bid`
- Vendor GET of RFQ detail triggers T1 MarkViewed (idempotent side effect).
Errors: DomainRuleException → 409 with rule message (follow existing mapping
convention); validation → 400.

## 7. UI (Slice J)
Buyer — RfqDetailHub:
- Invited-vendors table: new Status pill column (Invited grey / Viewed grey /
  Intends to bid teal / Declined amber with reason on hover-tooltip / Bid
  submitted green / Rescinded struck-grey). Rescinded rows stay visible.
- Row action "Rescind" (visible only when T7 legal): modal with reason-code
  select + optional note.
- "Add vendor" button (Draft/Open only): reuse the RFQ Builder vendor picker
  component; on G2 block, show the rule message with an "Extend deadline"
  shortcut.
- Action bar "Extend deadline" (Open only): modal — new close date/time
  picker (min = current ClosesUtc), reason-code select, note; shows
  "Extension N of MaxExtensions" and original close date.
- New "Activity" panel: RfqEvent timeline, newest first (event label, actor,
  reason, old→new close where applicable).
Vendor portal:
- Invitation list + RFQ detail: "Decline invitation" (modal: reason select +
  note) while Open and not yet bid; declined state shows a banner with
  "Reconsider — I intend to bid" (T4).
- "I intend to bid" affordance (lightweight button) pre-bid.
- After submitting: "Withdraw bid" (confirm modal, only while Open & before
  close); withdrawn state returns to the bid-editable view.
Dashboard/KPIs: UNCHANGED in this slice.

## 8. Permissions register (deferred auth — record only)
Create/append `docs/PERMISSIONS-REGISTER.md`: InviteVendorToRfq,
RescindRfqInvitation, ExtendRfq, ReInviteVendor (buyer roles TBD);
DeclineRfqInvitation, ReverseDecline, DeclareIntendToBid, WithdrawBid
(vendor principal, own invitations only — resource-scoped). Note the
extend/close race as a concurrency candidate for the deferred hardening pass.

## 9. Edge cases (must have tests)
| ID | Case | Expected |
|---|---|---|
| E1 | Vendor declines at 23:59, ClosesUtc 00:00 next day | allowed (now < ClosesUtc) |
| E2 | Vendor declines after ClosesUtc but RFQ row still Open | DomainRuleException (IClock guard) |
| E3 | Buyer rescinds vendor who submitted then withdrew | ALLOWED (no submitted bid at that moment) |
| E4 | Buyer invites vendor already Rescinded | T8: same row → Invited, new RfqEvent, timestamps updated |
| E5 | Buyer invites vendor already invited (any live status) | 409, no duplicate row |
| E6 | Extend to a datetime earlier than current ClosesUtc | DomainRuleException (forward-only) |
| E7 | Third extension when MaxExtensions=2 | DomainRuleException |
| E8 | Extend while Closed / UnderEvaluation | DomainRuleException |
| E9 | Late invite inside 72h window | blocked with extend-first message |
| E10 | Withdraw bid after ClosesUtc | DomainRuleException |
| E11 | Two buyers extend simultaneously | note as known race (deferred concurrency pass); last-write-wins acceptable for now, both events recorded |
| E12 | Backfill: RFQ with submitted bid vendor + non-bidding vendor | BidSubmitted + Invited rows respectively |
| E13 | Vendor views RFQ twice | ViewedUtc set once (first view) |
| E14 | Declined vendor tries to submit a bid via API | DomainRuleException (must reverse decline first — T4 then T5) |

## 10. Out of scope (do not build, do not break)
Multi-round/rebid (RoundNumber reserved), amendments/addenda + vendor
acknowledgment, no-bid reissue flow, evaluation-stage disqualification,
notifications, broadcast clarifications to late invitees, auth enforcement,
concurrency tokens, any dashboard/KPI change.

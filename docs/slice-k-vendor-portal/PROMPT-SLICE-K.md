# PROMPT — SLICE K: Vendor Portal Close-Out (UI)

Read README-FIRST.md in this folder first. Then execute in phases. Do not
skip Phase 0.

## Phase 0 — Gap inventory (no code changes; WAIT at the end)

Open the prototype (prototype/eprocure-portal.html — the vendor portal is a
role/mode inside this single clickable oracle; there is no separate Vendor
Portal.html / eproc-app.js) and walk
every vendor screen it defines: dashboard, RFQ invitations/bids list, bid
form, POs, deliveries/ASN, invoices, statement, clarifications/messages.
For each, compare against the corresponding built screen (VendorPortal
dispatcher → MyRfqs, BidForm, PoList/PoDetail vendor views, DeliveryList/
AsnForm/AsnDetail, InvoiceList/InvoiceForm, VendorStatement, Clarifications/
ChatDock).

Produce a table: Screen | Prototype behaviour | Built behaviour | Gap
(yes/no) | Effort (S/M/L) | Needs API? (must be NO to stay in scope).

Seed the inventory with these KNOWN items from the 2026-07-02 audit — verify
each and include them:

- K1 (audit X2 / BUY-4): Onboarding invitation REVOKE — the server endpoint
  exists but no UI reaches it. Wire a "Revoke" row action into the
  Onboarding Queue (buyer screen, included in this slice because it is
  invitation-lifecycle and security-relevant): confirm modal, revoked rows
  stay visible with a Revoked badge. NOTE: the client.ts wrapper
  revokeOnboardingInvitation may have been deleted in an earlier cleanup —
  re-add the wrapper if absent; the ENDPOINT exists either way.
- K2 (audit X3): Vendor cannot RAISE a clarification — thread reply works,
  but there is no vendor-side "New clarification" entry point even though
  the domain + endpoint shipped. Wire it: vendor picks one of their invited
  RFQs as scope, composes, thread appears in their list. Mirror the buyer's
  New-clarification modal pattern.
- K3: MyRfqs list polish per prototype: status facet filter, closes-date
  visibility with the timezone label convention, and the status-adaptive
  CTA verified against every invitation state Slice J introduced (Invited/
  Viewed/IntendToBid/Declined/BidSubmitted — Rescinded rows are filtered
  out server-side and must simply not break the list).
- K4: Vendor list-screen filters where the prototype has them and the build
  does not (candidates: vendor PO list status tabs parity, invoice list
  status/match filters, deliveries list). Inventory decides the exact set.
- K5: Post-action confirmation panels per prototype: after bid submit,
  after ASN create, after invoice submit — the prototype's confirm/summary
  behaviour vs the build's current toast-only (or missing) treatment.
- K6: Vendor dashboard tiles — verify counts/links match the prototype's
  vendor dashboard and none regressed after Slice J (declined invitations
  must not inflate "action needed").

Anything else the walk-through surfaces goes in the table too. Mark items
Needs API = YES as OUT OF SCOPE (list them for the backlog; do not build).

STOP. Present the inventory and wait for scope confirmation.

## Phase 1 — Build (after confirmation)

Implement the confirmed items. Per item: list files before changing them
(batching by item is fine), reuse existing components (Modal, pills,
ReasonModal pattern from RfqGovernance where a confirm-with-reason is
needed), and keep each item independently revertible (one commit per item
when history exists).

## Phase 2 — Verification

- vitest: baseline 103/103 plus, at minimum: revoke action renders + requires
  confirm (K1); vendor new-clarification modal round-trip renders (K2);
  MyRfqs filter narrows list (K3); one post-action panel renders (K5).
- tsc -b clean, oxlint clean.
- Run the full e2e-audit crawl; all checks green, zero console errors.
- Manual walkthrough evidence: vendor raises a clarification and buyer sees
  it; buyer revokes an onboarding invitation and the magic link stops
  resolving (verify the 404/expired behaviour — this is the security point
  of K1).

## Final report

Per-item files + summary, test counts vs baselines, crawl result,
PERMISSIONS-REGISTER.md additions and its total action count, out-of-scope
API-needing items destined for docs/BACKLOG.md, and anything observed but
not fixed (list only).

## Out of scope — do not touch
Payments module; any API/domain/migration change (stop-and-report rule);
auth enforcement; dashboards' analytics panel; RfqBuilder; Consolidate;
evaluation/award screens; NetSuite anything.

# PROMPT — SLICE J: RFQ Invitations, Extension & Event Log (UI)

Prerequisite: Slice I is merged and its API is running with the updated dev
seed. Read README-FIRST.md and RFQ-LIFECYCLE-ADDENDUM.md (§7 is your scope).

This is a FRONTEND-ONLY slice. No API, domain, or migration changes. If an
API gap blocks you, STOP and report — do not work around it client-side.

## Step 0 — Report before touching
List every file you will create or modify, grouped by the tasks below, plus:
which existing components you will reuse for (a) the vendor picker, (b) the
reason-code modal pattern, (c) status pills. WAIT for confirmation.

## Task 1 — Buyer: RfqDetailHub invitation table
- Add a Status pill column per addendum §7 styling notes, reusing the app's
  existing pill component/tokens (app style package is the authority — square
  corners, Inter, existing badge palette).
- Declined rows: reason code label visible, note on hover tooltip.
- Rescinded rows: remain in the table, visually muted with strikethrough
  vendor name and the rescind reason.
- Row action "Rescind" rendered only when legal (pre-bid states, RFQ
  Draft/Open); opens modal: reason-code select (from API-provided list) +
  optional note + confirm. On 409, surface the rule message inline.

## Task 2 — Buyer: Add vendor & Extend deadline
- "Add vendor" button on RfqDetailHub (Draft/Open only), reusing the RFQ
  Builder vendor picker (search/filters/SWEC match). Already-invited vendors
  appear disabled with their current status. On the G2 late-invite 409, show
  the rule message with an "Extend deadline" shortcut button that opens the
  extend modal.
- "Extend deadline" in the status-adaptive action bar (Open only). Modal:
  new close date+time picker (min = current ClosesUtc + 1 minute), reason
  select, optional note, and a static line: "Extension {n} of {max} ·
  Original close {OriginalClosesUtc}". Disable submission when the API
  reports the cap is reached; still handle the 409 defensively.

## Task 3 — Buyer: Activity timeline
- New "Activity" panel on RfqDetailHub rendering events[] newest-first:
  event label, actor name, reason label, and for extensions "old → new"
  close datetimes. Follow the app's existing card/panel styling. Empty
  state: "No activity yet."

## Task 4 — Vendor portal
- Invitation list + bid page: "Decline invitation" (modal: reason select +
  optional note), shown only while the RFQ is Open and no bid submitted.
- Declined state: banner with the recorded reason and a "Reconsider — I
  intend to bid" action (T4), shown only while Open.
- "I intend to bid" lightweight action pre-bid; once set, show a subtle
  confirmation state instead of the button.
- "Withdraw bid" on the submitted-bid view (confirm modal), only while Open
  and before close; withdrawn returns the vendor to the editable bid view
  with a "bid withdrawn — resubmit before {ClosesUtc}" notice.
- Viewing the RFQ detail fires the mark-viewed call exactly once per load
  (fire-and-forget; failures must not break the page).

## Task 5 — Consistency sweep
- Anywhere the UI previously derived invited-vendor counts or lists
  (RfqList bid-progress badges, RfqDetailHub stat cards, dashboards), verify
  the numbers still reconcile: invited count should EXCLUDE Rescinded;
  bid-progress denominators must state what they count. Report each place
  checked and any semantic decision made.

## Done means
- tsc -b clean, oxlint clean, vitest: baseline 90/90 plus new tests — at
  minimum: invitation table renders all six statuses from mock data; rescind
  modal requires a reason; extend modal blocks backward dates; vendor
  decline→reconsider flow renders both states.
- Manual walkthrough evidence (describe or screenshot): buyer invites a
  vendor, vendor declines with reason, buyer sees Declined pill + activity
  event, buyer extends deadline, vendor sees new deadline, vendor reverses
  decline and submits, buyer sees BidSubmitted.
- Final report: files per task, test counts, semantic decisions from Task 5,
  anything observed but not fixed (list only).

## Out of scope — do not touch
API project; dashboards' analytics panel; Confirm Lines; Consolidate;
envelope/evaluation/award screens beyond the RfqDetailHub additions above;
notifications; any styling refactor beyond these components.

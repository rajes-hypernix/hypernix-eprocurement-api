# PROMPT — D7.5: Reach & Journeys (rollout of proven machinery)

Governing constraints: docs/design-d0-d1/README-FIRST.md in full.
Prerequisite: D7 shipped. Baselines from D7 close — record, hold or raise.

D7.5 builds NO new engines. It wires proven components into the operator's
stated journeys — each gate below is one of those journeys performed by a
persona. If any task turns out to need new machinery, STOP and report;
that is a scoping finding, not a build instruction.

## Step 0 — Report and WAIT
File plan, five-lens review, and three surveys:
(a) LIST-SCREEN ROLLOUT TARGETS: which list screens can take the
    ViewPicker honestly — ListPage-composed (RfqList has it) vs
    grandfathered pages where the picker can mount without full archetype
    migration vs pages where migration is the honest prerequisite (those
    go to their gated rows, not this slice). Propose the rollout set;
    floor: PO list + Vendor master + Invoice list or argued equivalents.
(b) DETAIL-SCREEN ROLLOUT: remaining CustomFieldsSection/SegmentsSection
    surfaces (Invoice, ASN, RFQ detail, Onboarding review) — same honest
    triage.
(c) PAGINATION REALITY: does /api/views/{id}/run page today? Propose the
    paging contract (page/size, total) and where each consumer lands:
    list screens page fully; SavedViewList portlet stays top-N with
    "View all →" opening the list screen WITH that view selected (the
    NetSuite pattern, and the operator's stated expectation).

## The tasks (commit each; order by dependency)
1. SAVED VIEWS HOME: a "Saved Views" entry (nav placement proposed in
   Step 0 — user-menu or sidebar) listing mine/shared/system across
   record types with the existing rules (edit own, share per A-gate,
   delete own); "New view" for ANY record type from here — the builder
   already exists, this is its front door. GATE: the operator's journey —
   go to one place, create a view with criteria, no list screen involved.
2. RUN PAGINATION: per (c) — pinned by test (page 2 differs, total
   correct, scoping preserved across pages).
3. VIEWPICKER ROLLOUT per (a) + each screen's default = a seeded system
   view reproducing today's list (parity, crawl green per screen).
4. SAVEDVIEWLIST "VIEW ALL →": portlet footer navigates to the record
   type's list screen with the portlet's view selected. GATE: >N rows in
   a view → portlet shows N + View all → full paged list, same rows.
5. USER-ADDED REMINDERS: in Personalize, add a reminder row from ANY view
   the caller can run (the D4 Reminders portlet consumes it; counts live;
   click-through opens the list with that view). GATE, the operator's
   journey verbatim: buyer creates "PRs pending approval" as a view, adds
   it as a reminder, the count shows, clicking it lands on the filtered
   list.
6. DETAIL-SECTION ROLLOUT per (b).
7. Docs: PRIMITIVES (the journeys as patterns), BACKLOG reconciled (each
   deferred surface has a row; closed rows closed), matrix if any
   endpoint moved.

## THE CLOSING GATE — the operator's full journey, one persona, one spec
09-journeys.spec.ts: buyer opens Saved Views home → creates "PRs pending
approval" with criteria → Personalize: adds it as a Reminder AND pins a
KPI from it → dashboard shows both live → clicks the reminder → paged
list with the view selected → opens a PR → the D7 role form renders with
her custom field on its subtab. One unbroken NetSuite-grade journey,
screenshots committed. Full crawl at close.

## Out of scope
Any new engine; archetype migrations beyond what (a) sanctions as cheap;
vendor-portal saved-view UI (its own row stands); scheduled/emailed
anything; new npm dependencies.

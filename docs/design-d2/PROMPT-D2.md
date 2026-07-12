# PROMPT — D2: Archetypes & Navigation Shell

Governing constraints: docs/design-d0-d1/README-FIRST.md applies in full
(charter, five lenses, no-dummy doctrine, baselines, commit discipline).
Prerequisites: D1 pushed and CI green; the operator's gallery sign-off given;
the corner-radius ruling recorded in CHARTER.md (whichever way it went).

D2 delivers the composition layer: after this slice, every new screen is an
archetype instantiation, and the app has a NetSuite-grade shell.

## Step 0 — Report and WAIT
File plan per phase, five-lens review of the plan, and four discovery
answers with evidence:
(a) Which list endpoints support server-side text search today (query
    params), per record type? Global search must be FUNCTIONAL — if a thin
    read-only /api/search endpoint (code + title match across Vendor, PR,
    RFQ, PO, Invoice; perimeter-scoped, vendor principals see only their
    own reachable records) is the honest way to deliver it, propose it here.
    No schema changes; queries only. No client-side fake-federation over
    unfiltered lists.
(b) The exact role→action map <Gated> will consume, derived from
    docs/PERMISSIONS-REGISTER.md — and the honest statement that D2's
    gating is DISPLAY gating (server authorization is the role-matrix
    slice, scheduled before D3). Hidden ≠ forbidden; say so in code comment
    and PRIMITIVES.md.
(c) Which screens are the four retrofit targets' closest structural
    relatives (so archetype props are derived from more than one example):
    Transaction (PrForm — done in D1), Entity (VendorDetail), List
    (RfqList), Setup (AdminCustomLists).
(d) The D0 pattern census rows (Task 3) each archetype absorbs — same
    derived-not-invented rule as D1.

## Phase 1 — Archetype scaffolds (commit per archetype)
web/src/ui/archetypes/: TransactionPage, EntityPage, ListPage, SetupPage.
- TransactionPage: header (code CodeField-styled, title, status pill,
  status-adaptive action bar slot), body-field sections from FieldSpec
  arrays, sublist slots, subtabs, dirty-navigation guard built in.
- EntityPage: master header, body sections, related sublists as subtabs.
- ListPage: toolbar (search box, filter slot, view toggle slot, primary
  action), table with sortable columns, bulk row-select with indeterminate
  select-all (census F31 — this is where it lands), empty state, row
  actions; pagination if any existing list has it, else not invented.
- SetupPage: rail + detail layout generalised from AdminCustomLists.
Each scaffold's props derived from the D0 pattern census + the retrofit
target and its relatives. No speculative slots.

## Phase 2 — Navigation shell (one commit)
- Global search in the top bar: functional per Step 0(a); results grouped
  by record type, keyboard navigable, mono codes rendered as codes;
  vendor-principal scoping proven by a test.
- Quick-create "New" menu: role-aware (consumes the <Gated> map), routes to
  existing create screens only.
- Recent records: genuinely functional via localStorage (per-browser),
  capped list, record-type icons; a BACKLOG row notes server-side
  persistence rides D4's personalization tables.
- Center tabs: the existing role sidebars re-expressed as code-defined
  center-tab configs (structured so D4 can later data-drive them) —
  navigation behaviour identical today, per the crawl.
- <Gated action="..."> primitive per Step 0(b).
- QuickView hover card: fetches the existing GET detail endpoint on
  hover-intent (delay + cancel), renders a compact FieldSpec-driven summary;
  applied to at least vendor and RFQ references.

## Phase 3 — Retrofit proof gates (commit per screen)
- VendorDetail → EntityPage. ManualVendorForm's rival Field dies here.
- RfqList → ListPage (table/board toggle preserved in the view-toggle slot).
- AdminCustomLists → SetupPage.
- OnboardingForm's rival Field dies in whichever of the above touches it,
  or as its own micro-commit — D1's backlog rows close either way.
Gate per screen: behaviour parity, existing tests pass with markup-only
edits, full 42-check e2e-audit green after EACH retrofit, pixel-consistent
except sanctioned classes already ruled.

## Phase 4 — Enforcement + docs (one commit)
- Extend the adoption test: new PAGE components under components/ must
  compose an archetype (source-scan heuristic: new files matching the page
  naming conventions must import from ui/archetypes) — grandfather list for
  today's pages, shrink-only, same mechanics as D1's.
- PRIMITIVES.md gains the archetype catalogue + the display-gating honesty
  note; BACKLOG rows updated (rival Fields closed, F31 closed, recents
  persistence added).

## Verification
Baselines: API 298 (untouched), web 164 + new, full crawl 42/42 after every
retrofit, tsc/oxlint clean, CI green on push (link). Final report: per-phase
files, the Step 0 discovery answers as-built, retrofit parity evidence,
five-lens review — the CTO lens states what "all new screens, primitives
only" now means operationally.

## Out of scope
Saved Views (D3); dashboards/portlets/metrics (D4); any schema change; role
matrix server enforcement; migrating any screen beyond the three named;
BidForm and Consolidate (explicitly protected — their gates come later);
restyling beyond already-sanctioned classes.

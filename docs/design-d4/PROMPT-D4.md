# PROMPT — D4: Dashboards, Portlets & the Metric Layer

Governing constraints: docs/design-d0-d1/README-FIRST.md in full.
Prerequisites: D3 shipped (the aggregation seam is yours to build now);
RM catalog + permissions endpoint live.

D4's headline gate: web/src/mock/dashboardAnalytics.ts IS DELETED. Every
number on every dashboard is computed or honestly absent. A user creates
their own KPI end-to-end without code.

## Step 0 — Report and WAIT
File plan per phase, five-lens review, and five discovery answers:
(a) DASHBOARD CONTENT CENSUS: per role (Buyer, Vendor, Admin, Approver,
    TechEval), every element on today's dashboard classified COMPUTED
    (cite the query) vs MOCK (cite the mock field) vs STATIC. The mock
    list is D4's kill list; the computed list must survive byte-identical
    or migrate onto the metric layer with parity.
(b) SYSTEM METRIC CATALOG, derived not invented: each seeded metric names
    its data source (table/timestamp per DATA-MODEL.md), its unit, its
    null behaviour (Slice H's honest-null rule propagates — a metric whose
    inputs are backfill-null renders "not yet available", never zero).
    Expected candidates: open PRs, RFQs awaiting bids, ready-to-open,
    under-evaluation (the four stat cards — already computed, migrate),
    PR→PO cycle days (IssuedUtc-dependent → null until real flow),
    committed spend MTD + vs same month LY, vendors onboarded/month,
    top recent POs. The demo mockup's dashed FORECAST is NOT rebuilt:
    actuals only; "deterministic projection (regression) on chart
    portlets" goes to BACKLOG as a product-roadmap item.
(c) AGGREGATION SEAM DESIGN: extend the D3 run machinery with
    GET /api/views/{id}/aggregate (count | sum | avg over a Money/Number
    FieldKey), riding the SAME scoped sources and View* checks — plus
    time-bucketed series for chart portlets (group by month over a Date/
    Instant FieldKey). State where in-memory decoration strains and what
    the IQueryable escape hatch would be — document, don't build, unless
    the seed data already proves a real problem.
(d) PERSONALIZATION INTERACTION: role-default dashboards seeded per role;
    "Personalize" copies the role default to a user-owned dashboard
    (copy-on-write); arrange mechanism with NO new npm deps — propose
    (HTML5 drag or arrow-based reorder; either is acceptable, pick for
    reliability over flash). Reset-to-default action.
(e) CATALOG ROWS: propose the new actions (UseDashboards all principals;
    ManageOwnDashboard all; ManageRoleDashboards Admin — or argue
    otherwise), each carried by an endpoint per the no-orphan rule.

## Phase 1 — Schema + role defaults (one migration: Dashboards)
Dashboard (Code, Name, OwnerRole nullable, OwnerUserId nullable,
IsRoleDefault, check: exactly one owner axis), PortletInstance
(DashboardId FK Restrict, PortletType enum, Title, Col/Row/Width smallints,
SavedViewId FK nullable, ConfigJson — the framework's ONE sanctioned JSON,
schema documented per portlet type in code). Seed role-default dashboards
reproducing (a)'s computed content per role. Up→Down→Up proven.

## Phase 2 — Metric layer + aggregation API
- SystemMetricCatalog in code (id, label, unit, compute via IClock-injected
  queries — this is where the VendorPerformanceView CURRENT_DATE backlog
  row gets honoured for anything this layer touches), each metric's null
  behaviour tested.
- The aggregate + series endpoints per (c), action-annotated, sweep
  unchanged, RoleMatrix auto-cases counted.
- Tests: each system metric against seed data (including the honest-null
  cases), aggregation correctness per fn × DataType, vendor-principal
  aggregation ⊆ own scope (the D3 scoping proof extended to numbers).

## Phase 3 — Portlet components + dashboard renderer
Each portlet type LIVE, consuming Phase 2 or D3, rendered through D1/D2
primitives (the adoption tests will hold you to it):
- KpiMeter (view-aggregation OR system metric; target + thresholds from
  config; honest-null state), KpiScorecard (grid of the above),
- Reminders (rows of {savedViewId} → live counts, click-through opens the
  list screen WITH that view picked — the D3 integration),
- SavedViewList (top-N rows of a view, e.g. top recent POs),
- Shortcuts (role-aware via the permissions list, like the New menu),
- RecentRecords (the D2 localStorage recents, portlet-ized),
- Chart (metric-over-months, hand-rolled SVG per the house precedent —
  actuals only, honest empty state below a minimum-data threshold).
Dashboard renderer replaces each role's hardcoded dashboard component,
reading the role default (or the user's personalized copy). Personalize +
arrange + reset per (d). Crawl green per role.

## Phase 4 — THE GATE + docs
- DELETE web/src/mock/dashboardAnalytics.ts and every import of it. The
  buyer dashboard shows only computed numbers or honest "not yet
  available" — no fabricated forecast, no invented growth headline. If the
  editorial headline can compute truthfully from real invoice history in
  the seed (this month vs same month LY), keep it computed; if the seed
  can't support it, it renders nothing and a BACKLOG row notes it
  self-activates with real data — the Slice H posture, verbatim.
- Gate evidence, performed by personas (extend 04-saved-views.spec.ts's
  pattern into a permanent spec): buyer creates a KPI from her own saved
  view (count of "Open RFQs closing this month", target set), pins it to
  her personalized dashboard, arranges it; the number matches the view's
  row count; vendor persona's dashboard shows only own-scope numbers.
  Screenshots committed.
- Docs: DATA-MODEL (two tables + grain + the ConfigJson schema note),
  PRIMITIVES (portlet catalogue), AUTHORIZATION-MATRIX (new rows), BACKLOG
  (forecast-projection row added; CURRENT_DATE row closed for touched
  paths; anything (a) killed that a role misses goes to SPSB-OPEN-QUESTIONS
  if it's a product call).

## Verification
Baselines from D3 close (API 388, web 210, e2e 43) held or raised; full
crawl per role after Phase 3 and at close; CI link per phase; migration
proof; five-lens review — the CTO lens states the demo-dashboard loop is
closed and what a KPI now costs.

## Out of scope
Forecast/projection math; custom fields/segments (D5/D6); center-tab
top-bar shell (its BACKLOG ruling stands); vendor-portal saved-view UI
(own row); any new npm dependency without stopping to ask; scheduled or
emailed dashboards.

# eProcure — Dashboard Analytics Panel (demo, hardcoded data)

Replace the "Active RFQs" table on the DASHBOARD with an analytics panel.
Frontend-only session. The file `eprocure-dashboard-analytics-mockup.html`
(in this folder) is the VISUAL ORACLE — match its layout, typography, and
behaviour, but use the real app's existing style tokens/components where they
exist (the app is the style authority; the mockup shows structure and
behaviour).

## Ground rules
1. BEFORE any change, list every file you intend to touch and wait for my OK.
2. FRONTEND ONLY. No API changes, no new endpoints, no schema, no migrations.
3. NO new npm dependencies. Charts are hand-rolled SVG exactly as in the
   mockup (paths computed from data arrays). Do not add recharts/chart.js/d3.
4. Scope: BUYER dashboard (and Admin only if it shares the same component).
   The VENDOR, APPROVER, and TECH dashboard variants are OFF LIMITS —
   untouched.
5. The RFQ list page (RfqList) already shows the full RFQ table — do not
   modify it.
6. Keep the existing dashboard KPI stat cards exactly as they are.

## What to build
Remove the Active RFQs table + "New RFQ" button block from the buyer
Dashboard and replace it with a single bordered panel, two-column grid
(main column + 380px right rail), containing:

### A) Editorial headline + forecast area chart (left column)
- Georgia headline: "Compared with this time last year, awarded spend is
  up 12.8%" (delta in green), one-line explanatory subtitle, and a
  "View details" link that routes to the existing RFQ list page.
- SVG area chart: 3 series (Materials / Services / MRO & consumables),
  12 months actual (Jul 2025 – Jun 2026) as solid lines with low-opacity
  fills, 3 months forecast (Jul – Sep 2026) as dashed lines, dot markers on
  the current month, dashed vertical divider at the actual/forecast
  boundary, RM 'k y-axis gridlines.
- Hover: vertical cursor line + tooltip showing month, per-series values,
  and total (see mockup for exact behaviour).
- "Show forecast" toggle in the panel header hides/shows the dashed
  segments and forecast months (see mockup).

### B) New vendors onboarded (right rail, top)
- Stacked SVG bar chart, 13 months, 3 segments (Manufacturer / Distributor /
  Service provider), small legend.
- Card header "View all" link routes to the existing Vendor Master page.

### C) Top recent purchase orders (right rail, bottom)
- Compact 5-row table: PO code rendered as plain JetBrains Mono text
  (NOT a link — these are mock IDs and must not produce dead detail
  routes), vendor name, status pill reusing the app's existing status-pill
  styling, RM total right-aligned.
- Card header "View all" link routes to the existing PO list page.

All three navigation links (RFQ list, Vendor Master, PO list) must resolve
to real existing routes — verify each one navigates correctly.

## Data
Create `web/src/mock/dashboardAnalytics.ts` exporting typed constants for
all three widgets. Copy the exact numbers from the mockup's <script> data
block (series values, bar values, PO rows, headline figures). Top of file
must carry:

```ts
// DEMO DATA — hardcoded pending the analytics layer. Do not wire to API.
```

No component may inline the numbers; everything imports from this one file
so the future API swap is a single-file change.

## Component structure
Follow the app's existing component conventions. Suggested (adapt to
convention if it differs):

- `web/src/components/dashboard/SpendTrendChart.tsx`
- `web/src/components/dashboard/VendorsOnboardedChart.tsx`
- `web/src/components/dashboard/RecentPosCard.tsx`
- `web/src/components/dashboard/DashboardAnalyticsPanel.tsx` (composes the
  three)

Keep chart math inside the components; keep data in the mock file.

## Done means
- `tsc -b` clean, oxlint clean, vitest 90/90 (adjust any dashboard test
  that asserted the old Active RFQs table; do not delete unrelated tests).
- Buyer dashboard renders the panel with no console errors.
- Vendor dashboard verified UNCHANGED — state which file/branch guarantees
  this.
- All three "View all"/"View details" links navigate to real pages.
- Add one vitest render test for DashboardAnalyticsPanel (renders headline,
  both SVGs, and 5 PO rows from mock data).
- Final report: files changed per component, one-line summary each, and
  full test results vs the 90/90 baseline.

## Out of scope — do not touch even if you notice problems
API project entirely; vendor/approver/tech dashboards; RfqList; Confirm
Lines; Consolidate; any auth code; any styling refactor beyond this panel.

# Dashboard Analytics Panel — demo package

Replaces the Active RFQs table on the buyer Sourcing Dashboard with a
NetSuite Next-style analytics panel (editorial headline, forecast area
chart, vendors-onboarded bars, top recent POs). Demo build: all figures
hardcoded in one mock file pending the real analytics layer.

## Contents
- `PROMPT.md` — the Claude Code prompt. Paste its full contents into a
  Claude Code session started at the repo root (`~/dev/eprocure`).
- `eprocure-dashboard-analytics-mockup.html` — the visual/behavioural
  oracle. Open in a browser to see target layout, hover tooltip, and the
  forecast toggle. Claude Code reads this file for structure; the real app
  style package remains the authority for tokens.

## Notes
- Frontend-only. No API, no migrations, no new npm dependencies.
- Headline text and forecast are template + arithmetic (no AI): the real
  implementation later is computed deltas and a simple projection
  (linear regression / Holt-Winters) in the API.
- PO codes in the table are intentionally NOT links (mock IDs); the three
  "View all"/"View details" links route to real pages (RFQ list, Vendor
  Master, PO list).
- Future wiring: swap `web/src/mock/dashboardAnalytics.ts` for an API call —
  single-file change by design.

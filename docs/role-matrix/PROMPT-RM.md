# PROMPT — SLICE RM: Role-Matrix Authorization

Read README-FIRST.md in this folder first.

## Step 0 — The matrix. Report and WAIT.
Produce docs/AUTHORIZATION-MATRIX.md (draft, uncommitted until ruled):
1. Enumerate EVERY endpoint via ApiExplorer (the sweep test's technique).
2. Assign each an action name: the register's 10 enumerated actions where
   they exist; new action names for everything else (verb-noun, e.g.
   SubmitPr, ApproveAward, ScoreTechnical, ManageUsers, ViewDashboard).
   Group read endpoints sensibly (e.g. ViewRfqs covers RFQ list + detail)
   rather than one action per route.
3. Assign allowed roles per action from three evidence sources, cited per
   row: PERMISSIONS-REGISTER.md, D2's ui/gating.ts provenance notes, and
   the de-facto access encoded in role sidebars/screens. Where evidence
   conflicts or is absent, the row is marked OPERATOR-DECISION with the
   options stated — do not guess. Expect genuine questions at least on:
   Approver's read surface, evaluators' access outside scoring, Admin's
   reach into transactional actions, and which internal roles may use
   /api/search and the files endpoints.
4. State the [AllowAnonymous] set (unchanged from Slice F) and the
   Vendor principal's complete action list.
Include the file plan for Phases 1–3 and the five-lens review of the plan.
STOP for the operator's ruling on every OPERATOR-DECISION row.

## Phase 1 — Catalog + enforcement (after ruling)
- ActionCatalog: one declarative file (action → roles), each row commented
  with its matrix line. An authorization requirement/handler resolves
  [Authorize] policies from it; endpoints get their action via a single
  attribute or endpoint-metadata convention — pick the mechanism that keeps
  controllers readable and REPORT which.
- Wrong role → 403 problem-details ("Not permitted for your role"), never
  a leaky 404-masquerade EXCEPT where Slice F already established
  404-for-existence-hiding (dev endpoints) — preserve those exactly.
- The drift-proof test: ApiExplorer-driven assertion that every endpoint
  (outside the AllowAnonymous set) carries an action assignment. New
  unassigned endpoint = red CI.

## Phase 2 — The authorization test matrix
Integration suite in the Slice F mould, catalog-driven: for every action,
for every role — allowed roles get non-401/403 (200/404/409 all acceptable;
we assert authorization, not workflow), denied roles get exactly 403.
Generated from the catalog so the matrix and its tests cannot diverge.
Anonymous-sweep must pass unchanged; Slice F scoping tests unchanged.

## Phase 3 — Web derivation
- GET /api/auth/permissions: returns the CALLER's allowed actions (from
  the catalog + their claims). Anonymous → 401 like everything else.
- <Gated> and the New-menu consume it (fetched once post-identity, cached);
  ui/gating.ts's static map is deleted; a web test pins that gating renders
  from the server list. Display behaviour for every persona is unchanged —
  crawl proves it.
- PERMISSIONS-REGISTER.md is rewritten to point at AUTHORIZATION-MATRIX.md
  as the living document; the "one remaining authorization item" note is
  closed.

## Verification & final report
Baselines held/raised; crawl 42/42; CI links per push; the matrix document
committed as ruled; per-lens review — the CTO lens states plainly: the
platform now enforces who-may-do-what at the server, the last register
item is closed, and D3 is unblocked.

# PROMPT — D6: Custom Segments (the dimension engine)

Governing constraints: docs/design-d0-d1/README-FIRST.md in full. Standing
procedures: fresh-DB migration replay, idempotent seeds, exit-code gating
(the D5 lesson), commit-per-phase CI.

Segments are user-defined REPORTING DIMENSIONS: one definition, applied to
multiple record types (header or line level), every assignment a dimension
key by construction. This is what NetSuite custom segments are, and it is
the ruled answer to both SPSB dimension questions (spend category on P2P
lines; vendor segmentation) — those stay THEIR call; D6 builds the engine
and proves it with a demo segment plus the system migrations below.

Headline gate: an ADMIN defines a segment ("Project"), applies it to
PR + PO + Invoice, assigns values on real records; it then functions as a
real dimension — saved-view filter, KPI slice, series/chart grouping —
performed by personas as permanent spec 07-segments.spec.ts.

## Step 0 — Report and WAIT
File plan per phase, five-lens review, and five discovery answers:
(a) THE T6 MIGRATION QUESTION (expect this to produce OPERATOR-DECISIONs):
    the frozen gate says the four PR dimension fields (Department,
    Location, Category, Job) "migrate in as seeded system segments." Those
    are load-bearing typed columns today (PR form, facet filters, D0
    census). Survey every consumer and bring OPTIONS with costs: (i) seed
    the four as system segments + backfill SegmentAssignments from the
    columns + PR renders/filters via segment machinery + columns retired
    (a T4-class contract change); (ii) same but columns retained in
    lockstep (dual-write, drift risk — argue honestly); (iii) segments
    seeded + backfilled, PR UI unchanged this slice, convergence its own
    gated row. Recommend one with evidence. Behaviour-preservation doctrine
    applies whichever way I rule.
(b) LINE-LEVEL SCOPE: SegmentApplication.LineLevel rides Slice H's stable
    line Guids. Propose the line surfaces this slice: engine capability
    complete + which ONE sublist gets the assignment UI as proof (PO lines
    is the natural spend-category dress rehearsal) — versus wiring every
    sublist speculatively (rule 6 says no).
(c) HIERARCHY: SegmentValue.ParentValueId per the framework schema. Does
    any near-term consumer need hierarchy RENDERING (tree pickers,
    rollup in aggregation), or does D6 ship flat-with-parent-stored and
    hierarchy UX/rollup goes to BACKLOG gate-driven? Recommend with
    evidence; storing the column costs nothing, building rollup does.
(d) EXECUTOR + REGISTRY: seg_* keys as Kind=Segment rows; the resolver
    pattern (D5's Val() precedent) for filters/columns/aggregate-grouping.
    NEW capability to design: GROUP-BY-SEGMENT for KPI slices and series
    ("spend by Project") — propose the endpoint/parameter shape riding the
    same scoped sources; this is D6's one genuinely new engine piece,
    like D5's resolver was.
(e) CATALOG + AUTH: propose rows (prior, argue if wrong: A68
    ManageSegments = Admin for def/value CRUD; assignment read rides
    View*, assignment WRITE = Buyer via the A67 convention — or fold
    assignment into A67? argue the honest shape); vendor principals read
    segment labels on reachable records, write nothing.

## Phase 1 — Schema (one migration: Segments)
SegmentDef (Code UQ seg_*, Name, HasHierarchy, Required, Active, Utc),
SegmentValue (SegmentDefId FK, Code, Label, ParentValueId FK nullable,
Active, Sort, UQ(SegmentDefId, Code)), SegmentApplication (SegmentDefId FK,
RecordType, LineLevel bool, UQ(SegmentDefId, RecordType)),
SegmentAssignment (SegmentDefId FK, SegmentValueId FK, RecordType,
RecordId, LineId Guid nullable, UQ(SegmentDefId, RecordType, RecordId,
LineId)). Registry lockstep (Kind=Segment) per the D5 pattern; orphan
probe extended to assignments; whatever (a)'s ruling seeds/backfills lands
here with counts reported. Fresh-DB replay proven.

## Phase 2 — API + engine
Def/value/application CRUD; assignment endpoints (per (e)'s ruled shape,
decorating scoped record fetches — the established three-layer pattern);
executor: Segment-kind filters/columns via the resolver + the (d)
group-by-segment capability with honest-null (unassigned records =
excluded/unassigned bucket surfaced, never silently dropped); tests: the
scoping matrix, group-by correctness, hierarchy storage round-trip,
RoleMatrix auto-cases, sweeps unchanged.

## Phase 3 — UI
Admin Setup screen (defs, values with parent picker per (c)'s ruling,
applications matrix — which record types, header/line); SegmentsSection on
the D5 CustomFieldsSection pattern for header assignment (same three
screens minimum); the ONE line-level proof surface from (b); view builder
palette gains "Segments" grouping; KPI/series UI gains the group-by-segment
pick where (d) lands it.

## Phase 4 — THE GATE + docs
Persona spec + screenshots: admin defines Project with three values,
applies to PR+PO+Invoice; buyer assigns on one of each; a view filters by
it; a KPI slices by it; a series groups spend by it — numbers match runs.
Whatever (a) ruled lands with its own parity evidence. Docs: DATA-MODEL
(four tables, grain, the dimension-key-by-construction note), PRIMITIVES,
matrix, BACKLOG (hierarchy rollup if deferred; remaining sublists;
convergence row if (a)=iii) — and BOTH SPSB-OPEN-QUESTIONS dimension rows
updated: "engine ready — decision is now which segments and which values,
not whether."

## Verification
Baselines from D5 close (API 420, web 214, e2e 45) held or raised; full
crawl at close; CI per phase; five-lens review — the CTO lens states what
a new reporting dimension now costs, and names the SPSB questions as the
first real consumers waiting.

## Out of scope
Building SPSB's actual segments (their call — engine + demo only unless
(a) rules the four system migrations in); hierarchy rollup math unless (c)
justifies; form-engine conditional behaviour (D7); NetSuite class/dept/
location mapping (integration era); any new npm dependency.

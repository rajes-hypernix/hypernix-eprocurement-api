# STEP 0 — CF7: Saved View → Saved Search generalization (plan only — no code built)

**Closes:** PLAN §10. Scope per the operator's own rulings recorded there:
**User/Item entity classes DEFERRED** (they don't exist as first-class records
yet — revisit when they do); **"customizations as a search class" DROPPED**
(NetSuite doesn't saved-search field defs either; the admin list views cover it).
CF3 already delivered §10(c) discoverability and §10(d) seeded pickers; CF4
delivered show-in-list. What remains is (a) richer criteria and (b) segments as
a searchable class, plus grouping parity.

## Current engine (verified in code)

- `SavedViewFilter`: FieldKey + `ViewOperator { Eq, In, Contains, Between, Gte,
  Lte }` + Value/Value2, implicitly AND-joined, flat list.
- Runner: in-memory over the scoped DTO list; custom + segment keys hydrate via
  per-run maps; aggregate/series/groupBy already exist (D4/D6).
- Grouping: KPI `groupBy` one segment dimension; no result-table grouping.

## (a) Richer criteria — operators + AND/OR

Operator additions (enum extension — migration-free; the column stores the enum
name): `Neq`, `Gt`, `Lt`, `StartsWith`, `IsEmpty`, `IsNotEmpty`, `NotIn`,
`NotContains`. Each with honest-null semantics defined UP FRONT (the D4
discipline): `Neq`/`NotContains`/`NotIn` EXCLUDE null rows (null is "unknown",
not "different") — `IsEmpty` is the explicit way to ask for nulls. This rule is
written into the runner tests before the operators land.

AND/OR — **one level, NetSuite-style "grouped OR"**: `SavedViewFilter` gains
`GroupIndex (int, default 0)`. Filters in the same group OR together; groups
AND together. Full nested boolean trees REJECTED: the builder UI for arbitrary
nesting costs more than the parity it buys, and NetSuite's own criteria UI is
effectively AND-of-ORs. Migration: **`ViewFilterGroups`** (one added column,
default 0 = today's all-AND behaviour — zero backfill).

Builder UI: each criterion row gets an "or…" affordance that adds a row into the
same group; groups render as bracketed blocks. The existing typed value editors
(the `PR status` select, date @-tokens) carry over per operator arity
(IsEmpty/IsNotEmpty take no value; Between keeps Value2).

## (b) Segments as a searchable class

Today segments are dimensions ON records. NetSuite also lets you SEARCH the
segment values themselves (e.g. "all active Cost Pool values assigned to >0
records"). Delivery: `RecordType.Segment` is NOT added (segments aren't a
record type); instead a dedicated scoped source — the runner's `SourceRowsAsync`
switch gains a `SegmentValueRow` projection (DefName, Code, Label, Active,
ParentCode, AssignedCount) behind action `ViewSegments` (already exists, A68).
Registry rows seeded as a new census block (`RecordType.SegmentValue`), so the
SAME builder/runner/columns/operators work unchanged. Migration: none (registry
rows are seed data; enum extension on RecordType for `SegmentValue` only —
**`SegmentSearchClass`** migration if the enum is DB-mapped by name in
FieldRegistry rows; named now so it is planned, likely a seed-only change).

## (c) Grouping parity (result-table summary)

`RunAsync` gains optional `groupBy=<fieldKey>&summary=<fn>` (reusing the D6
aggregate seam — same honest-null Unassigned bucket): the run result returns
grouped rows (group key, count, summed field) instead of flat rows when asked.
Views store nothing new (grouping is a RUN parameter first; persisting a view's
default grouping = one nullable column `SavedView.GroupByFieldKey`, migration
**`ViewDefaultGrouping`**, only if the operator wants it persisted — decision
below). The SavedViewList portlet and Saved Views home render grouped results
as section headers with counts (the NetSuite summary look).

## Task breakdown (browser-gated)

- CF7-T1: operator additions + null semantics tests. Browser: builder uses
  Neq + IsEmpty, list matches; a null row appears under IsEmpty, not Neq.
- CF7-T2: `ViewFilterGroups` migration + grouped-OR runner + builder "or…" UI.
  Browser: (Status=Draft OR Status=Submitted) AND Dept=Ops returns the union-
  intersection correctly on screen.
- CF7-T3: segment search class (projection + registry census + ViewSegments
  gating). Browser: build "active Cost Pool values with 0 assignments" and see
  the honest list.
- CF7-T4: run-time grouping + grouped rendering in home + portlet. Browser:
  group POs by vendor with sum(Total), section headers show sums that agree
  with the aggregate endpoint.

## Deferred / dropped (recorded, per instruction)

- **User/Item entity classes — DEFERRED** (operator's note; no first-class
  User/Item records exist to search yet).
- **Customizations as a search class — DROPPED** (operator doubted it; admin
  list views already cover it; NetSuite parity does not require it).
- Line-value search — deferred to post-CF6 (values gain LineId there; the
  runner stays header-grain until that lands and proves stable).

## Risks / operator decisions wanted

1. Grouped-OR (one level) vs full nesting — proposed one level; confirm.
2. Persist a view's default grouping (`ViewDefaultGrouping` migration) or keep
   grouping a run-time toggle only? Proposed: run-time first, persist on pull.
3. In-memory runner strain: operators/OR-groups don't change the cost profile,
   but segment-search adds a new source — the documented escape hatch
   (IScopedQuerySource<TDto>) remains the seam if real data proves slow.

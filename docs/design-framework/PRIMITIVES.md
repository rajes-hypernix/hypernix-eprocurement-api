# eProcure UI Primitives — the FieldSpec pipeline (D1) & Archetypes (D2)

Every field in the product renders from a `FieldSpec` object through one
pipeline (charter rule 3). Built-in fields define their specs in code; D5
custom fields will hydrate the same shape from the server. Derivation and
census evidence: `D0-INVENTORY.md`. Live catalogue: the dev-only
**/#design gallery route** (`web/src/ui/gallery/Gallery.tsx` — tree-shaken out
of production builds).

## The contract

`web/src/ui/fieldSpec.ts` — signed off at D0, implemented verbatim.

- **FieldSpec is pure metadata.** Primitives take `(spec, value, onChange)`;
  runtime state is passed at the render site: `error?: string` and
  `chrome?: 'labelled' | 'bare'` are props, never spec members.
- `chrome='labelled'` renders the `.field` block (label, required `*`, help,
  error); `chrome='bare'` renders the control alone with
  `aria-label={spec.label}` — the table-cell mode.
- Values are STRINGS in the stored formats the app already uses (pipe-joined
  multi/attachment, JSON table/group, ISO dates, raw numeric strings — never
  floats). `CheckboxField` alone takes a boolean.
- Reserved members: `displayType` renders normal/disabled/readOnly/hidden and
  no-ops otherwise (D5); `sourcing` is typed and inert (D7);
  `validation.maxLength` reserved.

## Catalogue (16 field primitives + Button family + badge set)

| Primitive | dataType | Notes |
|---|---|---|
| TextField | text, email | readOnly renders the `.ro` treatment |
| TextAreaField | longText | `rows` prop (2–4 in use) |
| CodeField | code | monospace via `--mono` (JetBrains Mono) |
| SelectField | select | options: static flat/grouped (optgroups) or customList; stores the CODE |
| DependentSelectField | select | customList + `parentField`; render site passes `parentValue`; degrades to free text when the parent has no children |
| MultiSelectField | multiSelect | checkbox column; pipe-joined value |
| YesNoField | yesNo | — / Yes / No |
| SegmentedField | segmented | radio-semantics button group (btn-pri/btn-out idiom) |
| CheckboxField | boolean | boolean value; label-after-box `.ck` idiom |
| NumberField | number, percent | `validation.min/max` = the business caps; `unit` suffix; `commitOnBlur`/`onCommit` render-site props (EvalScreens cells) |
| MoneyField | money | raw numeric string in state (live totals work); display formats on blur via `fmt()`; `currencyCode` affordance |
| DateField | date, dateTime | ISO in (Slice H T4), emits bare `yyyy-MM-dd`; readOnly shows dd/MM/yyyy via `fmtDay`; dateTime = instant (datetime-local + timezone affordance, `validation.min` floor) |
| AttachmentField | attachment | wraps existing uploadFile/fileUrl; `<id>::<name>` pipe value; config.multiple/filetypes |
| TableField | table | rows×columns from config; cellType drives input type; JSON value |
| RepeatingGroupField | group | entries to config.max; per-field types; JSON value |
| FieldChrome | (internal) | the one shell — do not hand-roll `.field` blocks in new code |
| Button / LinkButton | — | variant primary/outline/ghost/danger, `size='sm'`, `icon`, `busy`, `red` (replaces inline red styles) |
| StatusBadge / Pill / Chip | — | the six tones; domain status→tone maps stay at the domain edge |

## Usage rules

1. **New fields go through primitives.** Enforced:
   `web/src/test/no-raw-form-elements.test.ts` fails on any raw
   `<input>/<select>/<textarea>` in `web/src/components` beyond the
   grandfather list at `web/src/test/raw-form-elements.grandfather.json`
   (25 files / 121 elements at D1 close). The list SHRINKS at migrations —
   regenerate it in the migration commit — and never grows.
2. **No style overrides.** Primitives accept no className/style props; tokens
   (`web/src/tokens.css`, mirror `web/src/ui/tokens.ts`) are the only styling
   channel. Layout margins belong on wrapper elements at the render site.
3. **Migrate when touched.** Old markup coexists (charter: additive
   coexistence); a screen you're materially editing should adopt primitives
   for the parts you touch, then shrink the grandfather list.
4. **Sanctioned composition — checkbox-gated cell** (award grid, bid-line
   gate): `CheckboxField chrome='bare'` + conditional `NumberField
   chrome='bare'` in one `<td>`. A composition, not a primitive.
5. **Misfits stay bespoke, by written disposition** (D0 §Task 2): chat
   MessageInput (share one comm component when touched), SwecPicker tree
   (single-sourced already), bulk row-select (D2 List archetype).

## Archetypes (D2) — every new screen is an instantiation

`web/src/ui/archetypes/` — the composition layer. Derived from the D0 pattern
census + the retrofit targets and their relatives (D2 Step 0c/0d); no
speculative slots.

| Archetype | Retrofit proof | What it owns |
|---|---|---|
| **TransactionPage** | PrForm | crumb, pagehead + status-pill slot, error Notice, top+bottom action bar, sections from FieldSpec arrays via `renderField`, optional subtabs, sublist children, **dirty-navigation guard** (`useDirtyNavigationGuard` — call `markClean()` before intentional navigation) |
| **EntityPage** | VendorDetail | crumb, master header (inline badge nodes), header actions, controlled/uncontrolled subtabs, per-tab content |
| **ListPage** | RfqList | pagehead + primary-action + view-toggle slots, search box, faceted multi-select filterbar (values derived from data), columns with OPT-IN sorting (default off), row actions, bulk row-select with indeterminate select-all (census F31), EmptyState (none vs filtered), `alternateBody(filteredRows)` for board views |
| **SetupPage** | AdminCustomLists | pagehead + primary action, error Notice, rail (label + hint) + detail pane, modal children |

Supporting: `renderField` (the one dataType→primitive dispatch — D5 rides it),
`QuickView` (hover-intent card over existing GET detail endpoints),
`ui/display.tsx` (Stat, Kv), `centerTabs.ts` (nav data in center-tab shape;
the sidebar renders it via `tabsToNavGroups` — a top-tab shell is a deferred
operator design decision, see BACKLOG).

**Enforcement**: `src/test/archetype-adoption.test.ts` — a NEW page component
(name matching `*Page/*Screens/*List/*Detail/*Master/*Queue/*Hub/*Builder`)
must import from `ui/archetypes`; today's 12 non-composing pages are
grandfathered in `src/test/page-archetypes.grandfather.json`, shrink-only.

## Display gating — the honesty note (D2 Step 0b; updated at Slice RM)

`<Gated action="…">` (`ui/gating.tsx`) hides affordances. Since Slice RM the
list it consults is DERIVED FROM THE SERVER (`GET /api/auth/permissions`, the
ActionCatalog ruled in docs/AUTHORIZATION-MATRIX.md) — there is no client-side
role map to drift. Hidden is still not the security control: the SERVER
enforces every action with a 403 regardless of what renders, and the display
gate is a courtesy view of that same catalog. Never cite a hidden button as a
security control — cite the matrix row.

## Proof gates

PrForm is the Transaction reference (D1 Phase 3 fields; D2 Phase 1a
archetype): header from a FieldSpec array, bare-chrome line cells, zero
behaviour-test edits. VendorDetail/RfqList/AdminCustomLists are the D2
Phase 3 retrofits (full 42-check e2e after each). **All three rival Field
components are dead** (PrForm at D1; ManualVendorForm at D2 3a; OnboardingForm
at D2 3d).

## Saved views — the list-screen adoption note (D3)

List screens consume SAVED VIEWS for their row sets: `ViewPicker` +
`ViewBuilder` (`components/views/SavedViewControls.tsx`) ride the registry
(`GET /api/views/fields`) and the run endpoint; criterion value inputs render
by registry DataType through the D1 primitives. **New list screens must not
hand-roll filter persistence** — a screen's default is a seeded SYSTEM view,
user filters that deserve saving become view criteria. RfqList is the proof
screen (system view "All RFQs"; its quick facets stay layered per the D3
ruling — the Requisitions migration designs facets AS criteria from the
start, see BACKLOG). Sharing is publication: the Shared toggle appears only
for holders of ManageSharedViews (A61), derived from /api/auth/permissions
like every other gate.

## Dashboard archetype + portlet catalogue (D4)

`ui/archetypes/DashboardPage` is the FIFTH archetype: a 2-column grid of
portlet instances read from `GET /api/dashboards/mine`, with arrow-based
arrange chrome (ruled: testable beats flashy). `components/Dashboard.tsx` is
its instantiation; the eight portlet types (`components/portlets/`) are ALL
live-data — KpiMeter (metric- or view-aggregate-backed; honest "not yet
available"), KpiScorecard, Reminders (live counts; click-through opens the
list WITH the view picked), SavedViewList, Shortcuts (server-permission
gated), RecentRecords, Chart (hand-rolled SVG, actuals only, minimum-data
honest empty state), MyInvitations (vendor work surface, OD-D4-2).
**web/src/mock/ is DELETED — nothing on any dashboard is illustrative.** New
portlet types add a config record in PortletConfigs.cs, a renderer here, and
seed rows; no schema change.

## Custom fields — the identical pipeline (D5)

`components/customfields/CustomFieldsSection.tsx` renders a record's custom
fields: defs+values arrive in one call, become a FieldSpec array and go
through `renderField` — **the same pipeline as every built-in field; there is
no second path** (the charter-rule-3 ruling, cashed in). Wired on PO detail,
VendorDetail (tab) and PrForm; other record surfaces adopt the section at
their own gates. Defs are Admin Setup (`AdminCustomFields`, SetupPage
archetype, A65); values save via A67 (Buyer). Custom fields appear in the
view-builder palette grouped "Custom fields", filter/aggregate/KPI through
the untouched D3/D4 chain, and date criteria may use the ruled `@today±Nd`
token form. **Required is enforced at value-save ONLY — it does not gate
record lifecycle transitions.** A PO with an empty required custom field
still issues; transition gating is D7 form-engine territory, by design.

## Segments — the dimension engine (D6)

`components/segments/SegmentsSection.tsx` is the D5 section pattern applied to
dimensions: assignments arrive WITH def metadata and options in one call,
render as selects through `renderField` — the same pipeline, no second path.
Wired on PrForm, PO detail and VendorDetail; PO Lines carry the ONE line-level
proof (per-line expander → the same section with `lineId`). Defs/values/
applications are Admin Setup (`AdminSegments`, SetupPage archetype, A68);
assignments ride the FOLDED A66/A67 (ruled: same species as custom values —
the rows split only if the role sets ever diverge). Value codes derive from
labels via the same `DimCode` as the PR's dimension columns; the four system
segments mirror those columns one-way and are read-only on every surface.
Applied segments join the view-builder palette grouped "Segments" and slice
any aggregate/series via `groupBy` — every slice carries the NAMED
`Unassigned` bucket (honest-null applied to dimensions). The sliced KPI
(AddKpi "Slice by segment") is the UI proof; the stacked view-backed chart
portlet is BACKLOG, gate-driven.

## Entry forms — behaviour per role (D7)

PrForm renders from the caller's RESOLVED entry form (`/api/entry-forms/
resolve`, A71 + the record type's dynamic View*): FieldGroup sections,
3-across rows, FullWidth memo-style, definition-driven SUBTABS through the
TransactionPage tab slot — all through `renderField`, no second path. The
seeded Standard form is byte-identical to the markup it replaced (parity-
pinned both sides). Placed cf_/seg_ fields render inline on edit and save
via their own A66/A67 endpoints; UNPLACED keys keep their auto-sections —
D5's zero-deploy promise is preserved by design. Defaults (static values;
the shared @-token grammar for dates) apply to NEW records only.

**The enforcement boundary (ruled, verbatim).** Write contracts carry no
form id, and none is wanted: **a client-sent formCode would let a client
SEND THE STANDARD FORM'S CODE to dodge its role form's required fields —
server re-resolution isn't just cleaner, it's the only shape that isn't a
bypass** (OD-D7-2). The server re-resolves the CALLER's form at submit and
enforces `requiredOnForm` there — at SUBMIT only, never draft save (OD-D7-3:
drafts are incomplete by nature; D5 def-level required at value-save is
unchanged). Everything else — displayType (normal/disabled/readOnly/hidden)
and defaults — is UI-layer: **hidden ≠ forbidden**; a hidden field's value
still travels in the DTO and the role matrix is unchanged by form layout.
Per-form write-masking would be a new authorization semantic nobody ruled —
it is NOT claimed (BACKLOG).

**The L4 sublist boundary (restated).** Admin-defined subtabs are pure
layout containers holding FIELDS only. Sublists (line grids, activity,
documents) keep their built-in homes; CUSTOM sublists — admin-defined child
tables — are the recorded L4 boundary and deliberately out of D7's scope.

## Numbering — Setup configuration (D7)

`AdminNumbering` (SetupPage, A70) edits prefix/year-segment/digits per
record type with a non-consuming next-code preview. The scheme shapes codes
at MINT time only; history is never rewritten and counters never reset, so
no format change can re-issue an existing code (all four cases test-pinned;
PRG-2 concurrency untouched).

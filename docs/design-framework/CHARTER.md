# eProcure Design Framework — CHARTER

Created at D0 from `docs/design-d0-d1/README-FIRST.md`, expanded where the D0
inventory sharpened a principle. Applies to every design slice D0–D7. The
inventory itself is `D0-INVENTORY.md` (same directory); census row numbers
(F1–F37) below refer to it.

## 1. Typed core, configurable edges
No JSON/KV on business entities, ever. The ONE sanctioned JSON edge is the
questionnaire `FormItemConfig` (question templates), which predates this
framework and stays: it configures QUESTIONS, not business entities. FieldSpec
imports that shape verbatim rather than inventing a second config dialect.
Anything that would put a JSON bag on a Vendor, PR, RFQ, PO, Invoice, or their
lines is rejected at review.

## 2. Nothing ships as a dummy
Every component built must be demonstrably functional with real data by its
slice's gate. Placeholder-driven UI is a defect, not a phase. (The Payment
Voucher screen is the one standing exception, by explicit operator instruction
in CLAUDE.md — it is a labelled placeholder, not a dummy pretending to work.)

## 3. Metadata-consuming primitives from day one (RULED)
Every field renders from a FieldSpec object — built-in fields simply have their
specs defined in code. One rendering pipeline, forever. D5 custom fields and L4
custom records flow through this exact path.

**Sharpened by D0:** this is not aspiration — `AnswerInput` already renders all
12 question types from `(type, config, value, onChange)` and serves both RFQ
bids and onboarding packs. FieldSpec is the generalisation of that proven
contract, and the D0 catalogue shows the 12 QTYPES map onto the derived
primitives with zero remainder. Two rules the census forced:
- **FieldSpec is pure metadata.** Values and change-handlers stay props on the
  primitive (`spec, value, onChange`); specs are reusable data, storable by D5.
- **Chrome mode is the render site's choice, not the spec's.** The same logical
  field appears as a labelled block and as a bare table cell (qty guards,
  financial grid, matrix answers). Primitives take `chrome: 'labelled'|'bare'`;
  bare mode derives `aria-label` from `label` — formalising today's ad-hoc
  practice instead of forking the pipeline.

## 4. Five archetypes, no bespoke screens
Dashboard, Transaction, Entity, List, Setup. After D2, every new screen is an
archetype instantiation.

**Sharpened by D0:** the pattern census found the raw material is already
consistent — one `pagehead`, one `crumb`, one Notice, one Spinner, one
EmptyState — but tables come in 8 hand-rolled archetypes and sections/stat
cards/KV rows are re-implemented locally. D2's job is consolidation of
census-listed variants, not invention. The rival list (Section/Stat/KV
re-implementations, `filterbar` vs `filterbar2`, hand-rolled modals and empty
states, two danger-button idioms) is recorded in D0-INVENTORY §Task 3 and dies
at D2 gates or when touched.

## 5. Brand tokens are locked
Teal `#336374`, active-nav `#25586B`, links `#2C5C6E`, cream `#F3EFE9`, greige
`#F6F4EF`, border `#E4DED2`; Georgia headings and stat numerals, Inter UI,
JetBrains Mono codes; square corners. D1 systematises them; it does not restyle
the product.

**Sharpened by D0 (token findings, inventory §Token findings):**
- Three referenced-but-undefined variables (`--amber-bg`, `--blue-bg`,
  `--red-bg`) get defined in D1 Phase 1 at their current fallback values.
- The pill/badge palette's hardcoded hexes are deliberate contrast choices:
  Phase 1 NAMES them as tokens without changing a rendered pixel.
- **Open operator decision (blocks nothing in D0, must be settled in D1
  Phase 1):** neither Inter nor JetBrains Mono is actually loaded by the app —
  `--sans` names Inter with no `@font-face`, and `.mono` uses `ui-monospace`.
  Either ship the two font files (self-hosted; no CDN) or amend this charter
  line to the de-facto system stacks. Until decided, no component may hardcode
  a font-family; everything goes through the token.
- **Square corners** apply to structural chrome (cards, inputs, buttons,
  modals; `--radius: 0`). Pills, chips, dot-badges, ribbons, and the chat/
  health badges are the codified exception family (rounded by design for
  affordance). D1 must not "fix" them to square.

## 6. Derived from reality
No primitive exists that D0's inventory cannot trace to a real usage in the
codebase. No speculative components.

**Sharpened by D0:** the discipline runs both directions —
- every FieldSpec member carries a census-row citation (inventory §Task 4);
  members with no census evidence are either RESERVED-and-inert (`displayType`,
  `sourcing`, `maxLength`) or absent (`prefix`, input masks, style overrides);
- census rows that fit no clean primitive are MISFITS with a written
  disposition (bespoke-with-justification or deferred-to-slice), never silently
  absorbed into a bloated base component. Current misfits: chat MessageInput,
  SwecPicker tree, bulk row-select (D2), checkbox-gated cell composition.

## Coding principles (unchanged from README-FIRST)
- Additive coexistence: primitives live alongside old markup; screens migrate
  at their gates or when touched, never big-bang.
- One component, one file, typed props, no `any`. FieldSpec is the single
  contract; no primitive accepts styling overrides that bypass tokens.
- Tests per primitive: render, all states, a11y basics (label association,
  keyboard focus). Behaviour parity proven for retrofits.
- Atomic commits per phase, CI green on every push, crawl green at gates.
- Baselines: record the exact API/web/crawl counts at slice start; nothing
  drops below the recorded baseline. (D0 record: web vitest 31 files / 112
  tests green; D1 re-records at its start per its prompt.)

## The five lenses — applied at every report
Every Step-0 plan and every final report includes a five-lens review:
vendor/end-user; buyer/procurement user; solution architect (typed-core line,
L3/L4 seam); senior programmer (clean, tested, single-responsibility, no
duplication); CTO (marginal cost of the next module, handover risk). Reports
that skip a lens are incomplete.

## Out of scope for D0–D1
Any backend/DB change; dashboards/portlets (D4); saved views (D3); navigation
shell (D2); archetype scaffolds beyond what PrForm's rebuild needs (D2); custom
anything (D5+); restyling beyond token systematisation. The D0 gap list
(inventory §Task 5) names the tempting absences — toasts, tooltips, date-range
filters, sortable tables, comboboxes, form-level validation — and where each
belongs instead of D1.

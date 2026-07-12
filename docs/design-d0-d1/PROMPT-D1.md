# PROMPT — D1: Tokens & Field Primitives (the rendering pipeline)

Prerequisites, verify before anything: (1) D0-INVENTORY.md is committed and
the FieldSpec contract carries the operator's sign-off; (2) Slice H's
TypedDates commit is on main (`git log` — STOP if absent, per the
coordination guard). Record the exact test/crawl baselines at start.

## Step 0 — Report and WAIT
File plan per phase below, plus: (a) the final FieldSpec as you will
implement it (any drift from the signed-off D0 draft highlighted);
(b) which census misfits you are deferring and where they're logged;
(c) the five-lens review of the plan itself.

## Phase 1 — Token system
Extract the locked brand tokens from index.css into a single source of
truth (CSS custom properties in a tokens.css, plus a typed TS mirror for
components that need values). Categories: color (brand, status palette —
enumerate every pill tone the D0 census found), typography (Georgia/Inter/
JetBrains Mono scales actually in use), spacing, border, focus ring.
Do NOT restyle anything: after this phase the app renders pixel-identical;
the tokens simply name what already exists. Commit alone.

## Phase 2 — The primitive library
web/src/ui/ (new): implement the D0-derived primitive catalogue, every
primitive consuming FieldSpec (charter rule 3 — metadata-consuming from day
one). Requirements:
- One field chrome component (label, required marker, help, error, layout)
  that all field primitives share — the three rival Field components'
  feature union, built once.
- DateField consumes ISO-8601 (Slice H's DTOs) and renders dd/MM/yyyy via
  the existing lib/format.ts helpers; instants carry the timezone label
  convention.
- MoneyField: numeric input, currency affordance, right-aligned, formats on
  blur; never a float in state.
- DependentSelectField: options from a custom list with ParentValueCode
  filtering — powered by the existing getCustomLists client, not a new
  endpoint.
- CodeField renders JetBrains Mono; AttachmentField wraps the existing
  upload/download client paths (perimeter-scoped — do not touch the API).
- Reserved FieldSpec members (displayType, sourcing) are accepted but inert:
  displayType currently supports normal/disabled/readOnly/hidden rendering
  only where the census shows it used; the rest no-op with a code comment
  pointing at D7.
- Storybook is NOT in the stack — instead build one dev-only gallery route
  (/design/gallery, Development-gated the same way demo identity is) that
  renders every primitive in every state from FieldSpec fixtures. This is
  the living catalogue and the visual review surface.
- Tests per primitive: renders from spec, each state, label-input
  association, keyboard focus, error display. No `any`, no style-override
  props.
Commit per logical group (chrome + text-likes; selects; date/money;
attachment/table; gallery).

## Phase 3 — The proof gate: PrForm rebuilt
Rebuild PrForm.tsx entirely on the primitive library — the whole form
renders from an array of FieldSpecs plus the existing line-row logic.
- Behaviour parity is the gate: every existing PrForm test passes (adjusted
  only for markup, not behaviour), dirty-navigation guard intact, draft/
  submit/cancel flows identical, the crawl's PR checks green.
- Kill the local Field component in PrForm; the other two rivals
  (ManualVendorForm, OnboardingForm) are logged for D2-window migration,
  NOT touched now.
- Visual: pixel-consistent with today per Phase 1's no-restyle rule. Where
  the old form was internally inconsistent (the census will have shown it),
  the primitive's single rendering wins — list every such normalisation in
  the report with before/after description.
Commit alone.

## Phase 4 — Adoption rule + docs
- Add an architecture-style test (source-scan, mirroring Slice G's T4
  pattern): no NEW raw <input>/<select>/<textarea> in web/src/components
  outside a grandfather list generated from today's tree. The list shrinks
  as screens migrate; it never grows. This is the enforcement that makes
  the design system real.
- docs/design-framework/PRIMITIVES.md: the catalogue, FieldSpec reference,
  usage rules, the grandfather list location.
- BACKLOG rows: the two remaining rival Field components; census misfits
  deferred; any normalisations needing a product decision.

## Verification & report
Recorded baselines held or raised; new primitive tests counted; crawl green;
CI green (link the run); the gallery route demonstrated (screenshot list of
states); the PrForm parity evidence; the five-lens review — with the senior
programmer lens explicitly addressing duplication (three Fields → one
chrome) and the CTO lens explicitly addressing marginal-cost-of-next-module.

## Out of scope
Everything in README-FIRST's list. Additionally: do not migrate any screen
beyond PrForm; do not build archetype scaffolds (D2); do not add npm
dependencies without stopping to ask.

# README-FIRST — Design Slices D0 & D1 (Framework: Inventory, Tokens, Field Primitives)

The first two slices of the eProcure Platform Framework. Constraints are
non-negotiable and apply to every design slice (D0–D7).

## Coordination guard — read before anything
Slice H may still be in flight in another session. The rule:
- D0 (inventory, ZERO code changes) may run in parallel with Slice H safely.
- D1 MUST NOT begin until Slice H's T4 (typed dates) is merged to main.
  Reason: T4 changes date DTOs to ISO-8601 and rewrites PrForm's date
  handling; D1 builds the DateField primitive and rebuilds PrForm. Building
  date primitives against the old dd/MM/yyyy strings, or editing PrForm in
  two branches at once, guarantees rework or conflict.
  At D1 start: `git log --oneline -5` must show the TypedDates commit. If it
  doesn't, STOP and report.

## Design principles (the framework charter, short form)
1. **Typed core, configurable edges.** No JSON/KV on business entities, ever.
2. **Nothing ships as a dummy.** Every component built must be demonstrably
   functional with real data by its slice's gate. Placeholder-driven UI is a
   defect, not a phase.
3. **Metadata-consuming primitives from day one (RULED).** Every field
   renders from a FieldSpec object — built-in fields simply have their specs
   defined in code. One rendering pipeline, forever. D5 custom fields and L4
   custom records will flow through this exact path.
4. **Five archetypes, no bespoke screens.** Dashboard, Transaction, Entity,
   List, Setup. After D2, every new screen is an archetype instantiation.
5. **Brand tokens are locked** (teal #336374, active-nav #25586B, links
   #2C5C6E, cream #F3EFE9, greige #F6F4EF, border #E4DED2; Georgia headings
   and stat numerals, Inter UI, JetBrains Mono codes; square corners).
   D1 systematises them; it does not restyle the product.
6. **Derived from reality.** No primitive exists that D0's inventory cannot
   trace to a real usage in the codebase. No speculative components.

## Coding principles
- Additive coexistence: primitives live alongside old markup; screens
  migrate at their gates or when touched, never big-bang.
- One component, one file, typed props, no `any`. FieldSpec is the single
  contract; no primitive accepts styling overrides that bypass tokens.
- Tests per primitive: render, all states, a11y basics (label association,
  keyboard focus). Behaviour parity proven for retrofits.
- Atomic commits per phase, CI green on every push, crawl green at gates.
- Baselines: record the exact API/web/crawl counts at slice start (Slice H
  will have moved them); nothing drops below the recorded baseline.

## The five lenses — applied at every report
Every Step-0 plan and every final report includes a five-lens review:
- **Vendor/end-user:** does this make the vendor's daily task clearer?
- **Buyer/procurement user:** does Faridah's workflow get faster or noisier?
- **Solution architect:** does this hold the typed-core line and keep the
  L3/L4 seam open?
- **Senior programmer:** is the code clean, tested, single-responsibility,
  free of duplication (the three rival Field components die in D1)?
- **CTO:** does this reduce the marginal cost of the next module, and does
  anything here create handover risk?
Reports that skip a lens are incomplete.

## Out of scope for D0–D1
Any backend/DB change; dashboards/portlets (D4); saved views (D3);
navigation shell (D2); archetype scaffolds beyond what PrForm's rebuild
needs (D2); custom anything (D5+); restyling beyond token systematisation.

# PR Module + Consolidate Lines — Build Package

This package adds a **PR database + portal PR creation/editing**, a **Consolidate Lines**
workspace, and a **PR line lifecycle** (cancel / release-for-re-sourcing / re-open, with
provenance) to the existing eProcure app. It is **additive**. Nothing existing is removed.

## What is in here

| File | Purpose |
|---|---|
| `README-FIRST.md` | This file. Read first. The non-negotiable constraints. |
| `docs/pr-module/PR-MODULE-SPEC.md` | The full spec — data model, behaviour, screens, rules. |
| `docs/pr-module/DATA-MODEL-ANALYTICS.md` | **Analytics-readiness standard for the schema — binding.** |
| `docs/pr-module/ENGINEERING-STANDARDS.md` | **Clean-code / DDD / testing bar — binding.** |
| `docs/pr-module/EDGE-CASES.md` | Every edge case + expected behaviour (test these). |
| `docs/pr-module/BUILD-PLAN-SLICES.md` | The 5 slices and what each delivers. |
| `docs/pr-module/CLAUDE-CODE-PROMPTS.md` | One copy-paste prompt per slice. |
| `docs/pr-module/UI-FIXES.md` | NetSuite-banner removal + list-action alignment (cosmetic, separate). |
| `prototype/eprocure-pr-module-mockup.html` | **Behavioural oracle for this module.** |

## How to use this with Claude Code

Work **one slice at a time**, in order (A → B → C → D → E). For each slice, paste the
matching prompt from `CLAUDE-CODE-PROMPTS.md`. After each slice, **stop, run the full test
suite, and confirm green before the next one.**

## The behavioural oracle

`prototype/eprocure-pr-module-mockup.html` is a standalone clickable mockup of this module.
**Use it the same way you use `prototype/eprocure-portal.html`:** when the written spec is
ambiguous or you are unsure how an interaction should behave, *open the mockup and match its
behaviour.* Read its function names, its state shape (`S.*`), and its lifecycle logic
(`lineAct`, `recomputePr`, `clAdd`, `clDoMerge`, `clMove`, `prKanban`, `drawForm`).

## GOLDEN CONSTRAINTS — read every time, do not violate

1. **Additive, not a rewrite.** The existing **"Confirm lines"** flow on the Requisitions
   page **stays exactly as it is.** Consolidate Lines is an *additional* entry point beside
   it, not a replacement. Do not touch, refactor, or rename the existing confirm-lines path.

2. **One RFQ wizard, one award path.** Consolidate Lines replaces line *selection* only. Its
   "Build RFQ" hands the chosen lines into the **existing `RfqBuilder` wizard**
   (settings → questionnaire → invite → release). Do **not** create a parallel RFQ creator.

3. **Style: use the EXISTING style package, not the mockup's colors/fonts.** The mockup's
   `:root` tokens (e.g. teal `#336374`, Georgia/Inter) are *slightly different* from the real
   app's committed design tokens. **Take layout, structure, and behaviour from the mockup;
   take all colors, fonts, spacing, and component styles from the app's existing style
   package / primitives.** Compose existing components. Do not hardcode mockup hex values.

4. **Don't break existing tests.** All current tests must stay green. The only existing
   services you may modify are `AwardService` (no-award residual return) and the RFQ
   cancel/abandon path (line return) — and only **additively**, with new tests, never
   altering current behaviour or breaking current tests.

5. **Reuse infrastructure.** Portal PR codes come from the existing `NumberSequence` /
   `CodeGenerator`. Every state transition writes an existing `AuditEntry` via the existing
   audit log. Do not invent new code-gen or audit mechanisms.

6. **Test in detail.** For every transition, write a unit test. Add E2E for the headline
   flow. Run lint + typecheck + full test suite after each slice. See `EDGE-CASES.md` for the
   exact behaviours to assert.

7. **Report before large or risky edits.** If a change would touch existing migrations,
   existing service behaviour, or more than ~3 screens, stop and summarise the plan first.
   Work in batches of **3 screens, then stop** for a checkpoint.

8. **When in doubt, the mockup wins on behaviour; the existing app wins on style.**

9. **Analytics-grade schema is non-negotiable.** The long-term plan is a built-in analytics
   layer like NetSuite SuiteAnalytics. Every table you add or touch follows
   `DATA-MODEL-ANALYTICS.md`: append-only facts (no hard deletes), durable lineage links,
   GUID surrogate + immutable Code business keys, **conformed dimensions not free-text**,
   **typed UTC timestamps and real `DateOnly` business dates (not strings)**, money as
   `decimal` with currency, derived rollups (not stored aggregates), and a declared grain per
   fact table. Build the OLTP so a star-schema projection is later mechanical with zero
   refactor of business tables.

10. **Hold the CTO bar.** Follow `ENGINEERING-STANDARDS.md`: clean-architecture boundaries,
    rich domain entities with transition methods enforcing the state machine (no anemic
    setter-mutation from services), `DomainRuleException` on illegal transitions, `IClock` for
    all time, one reversible migration per slice, and detailed tests at unit + integration +
    E2E. Write it as if you are the most detail-oriented engineer on the planet handing this
    over for a 10-year maintenance life.

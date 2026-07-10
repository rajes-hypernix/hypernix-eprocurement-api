# Engineering Standards — the bar for this module

Build this as the most detail-oriented engineer/architect/CTO would: correct, clean,
maintainable, handed-over cleanly with no surprises. These are binding.

## Architecture & DDD
- **Respect clean-architecture boundaries.** Domain has no EF/infra/web dependencies.
  Application orchestrates; Infrastructure implements; Api stays thin.
- **Aggregate roots & consistency boundaries.** `PurchaseRequisition` is the aggregate root and
  owns its `PrLine`s. `PrLineSourcing` crosses aggregates (PR ↔ RFQ), so it is its **own
  entity referenced by ID**, not nested inside either aggregate.
- **Rich domain, not anemic.** State transitions are **methods on the entity/aggregate** that
  enforce the rules and are the single chokepoint for recording the transition — e.g.
  `prLine.Cancel(reason, clock)`, `prLine.ReturnFromRfq(reason, clock)`,
  `pr.RecomputeHeaderStatus()`. Services call these; controllers never mutate state directly.
- **Illegal transitions throw `DomainRuleException`** (the existing type) with a clear message.
  The state machine is the law — no setter lets you skip it.
- **Value objects** for concepts with rules: Money (amount + currency), Code IDs. No primitive
  obsession where an invariant exists.

## Determinism & side effects
- **All time via the injected `IClock`** (UTC). No `DateTime.Now`/`DateTime.UtcNow` in domain
  or services.
- **All codes via the existing `NumberSequence`/`CodeGenerator`.** No ad-hoc ID strings.
- **All transitions audited** through the existing audit log, with structured fields.
- Operations that can be retried are **idempotent** where it matters (e.g. release re-check).

## Code quality
- No magic strings/numbers — enums and named constants. Stable enum values (see analytics §9).
- Names match existing codebase conventions; new code reads like it was always there.
- Small, focused units; orchestrators stay thin; no god-services.
- Nullability honoured (NRT on); no `!` to silence the compiler without justification.
- Public surface documented with concise XML doc-comments; **each fact/link entity states its
  grain** (analytics §8).
- Oxlint (type-aware) + `tsc --noEmit` clean on the frontend; backend builds warning-clean.

## Migrations
- **One focused, reversible migration per slice**, clearly named, 14-digit timestamp (existing
  convention). Never edit a shipped migration — add a new one.
- Additive first: add new columns/tables and backfill; only then retire old shapes, and only if
  nothing reads them. Preserve existing seed/readers.

## Testing (test in detail — this is non-negotiable)
- **Unit** tests for every domain transition and guard (the state machine), including the
  illegal-transition rejections.
- **Integration** tests against EF (the InMemory provider, per existing tests) for endpoints,
  persistence, and the derived header status.
- **Parity** against the mockup behaviour; **E2E** for the headline flow (EDGE-CASES §G).
- Deterministic seed; tests independent and order-free.
- **The full existing suite must stay green.** New behaviour ships with new tests.

## Hand-over
- Anything deferred is marked with a consistent, greppable tag (`// HARDENING:`,
  `// DEFERRED:`) and listed in the slice report.
- The slice report states: what changed, the migration name, new test count, full-suite status,
  and any decision taken that the spec left open.

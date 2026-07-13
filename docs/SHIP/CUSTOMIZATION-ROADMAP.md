# Customization Layer — Roadmap & Remaining Work

**Purpose:** the deliberate build order for eProcure's customization layer (custom fields, lists,
segments, entry forms, sublists), with dependencies made explicit. This is a ROADMAP, not a build
plan — each numbered item becomes its own slice with its own Step 0 when its turn comes. It exists
so (a) the sequence isn't reconstructed from memory each time, and (b) whoever inherits this knows
the later items were **deliberately deferred with a plan**, not forgotten.

## Sequence (dependency-ordered)

### 1. Custom Fields — ✅ SHIPPED (v1.6, v1.7)
Types, validation, searchable select, manual internal id, contextual prefixes
(custbody_/custcol_/custlist_), shared-across-transactions with PR→PO carry-forward, save
discipline. Done and tested.

### 2. Custom Lists — ✅ SHIPPED (v1.6, v1.7)
One internal-id concept, auto-numeric value ids, order-mode, depends-on hardening, value-level
parent-child, staged-save. Done and tested.

### 3. Fields + Lists LIFECYCLE — 🔨 IN PROGRESS (CF-FIX-3, v1.8)
The reference registry, impact report, three-tier lifecycle (inactivate / delete / governed
historical-purge), type immutability. **Foundational** — builds the `IReferenceProvider` machinery
that items 4–6 reuse. Built form-extensible (see note below) so item 5 slots in without retrofit.

### 4. Custom Segments cleanup — ⏭ NEXT AFTER 3
Apply the same lifecycle discipline to segments: impact report, three-tier delete, no auto-save.
**Depends on:** item 3's registry pattern (a segment reference-provider is the same shape as a
field one). **Cheapest of the remaining** — it's reuse, not new architecture.

### 5. Entry Forms — ⏭ AFTER 4
Two missing capabilities the operator identified:
- **(a) Form selection on the transaction:** on a Requisition (etc.), the user cannot currently
  choose which entry form to use. The engine resolves a per-role form (D7), but there's no
  user-facing picker. Add it.
- **(b) Field-applies-to-specific-forms:** today a custom field applies to transaction *types*
  (CF-FIX-2). The parallel — a field applies to specific *forms within* a type — is missing. This
  is the natural completion of the shared-field work.
**Depends on:** item 3's reference registry being **form-extensible** (a field's "which forms am I
on" must be a config-reference provider — see the note in CF-FIX-3). If item 3 bakes in
form-static assumptions, this retrofits; if built extensible, it slots in.

### 6. Sublists (item / expense) — ⏭ AFTER 5 (biggest; real architecture)
- Create sublists as objects (currently one hardcoded item sublist) — add at minimum **item** and
  **expense** sublists.
- Choose which sublist a **custcol (line) field** goes on.
- Choose which **form** a sublist appears on.
**Depends on:** item 5 (a sublist lives on a form) and CF6 (line fields, already shipped).
**Note:** this is the same thread as the **B4 sublist-objects deferral** still open in the
CF5–CF7 longrun BLOCKERS — that design is not cold; pick it up from there.

### 7. Consolidation pass — ⏭ LAST (the seam-catcher)
One deliberate end-to-end walk of the WHOLE customization chain: a field → applied to a form →
on a transaction → with a sublist → holding a custom column → referenced by a saved view → with
lifecycle/impact-report → sliced by a segment. No single slice above tests this full chain; the
interactions between individually-correct slices are where bugs hide. This pass exists to find them
before handover.

## Standing housekeeping (not slices, but don't lose them)
- **Prod Postgres version** — still unlogged. Look it up in the Azure portal, write it into
  `ARCHITECTURE.md`. Gates whether CF6's partial-index uniqueness can later simplify to
  NULLS NOT DISTINCT. Asked for repeatedly; capture it before any prod deploy.
- **Migration squash** — dev-era migrations contain `Up()` drops; squash before first prod deploy
  (flagged in the CF backend-cleanliness audit).
- **F2 intermittent** — the unreproduced 08-entry-forms flake; root-cause pending, keep the trace
  instrumentation, don't let a green suite bury it.
- **CF5–CF7 B4/B5 deferrals** — B4 (sublist objects → item 6 above) and B5 (segment-search +
  result-grouping) still open with designs intact.

## Why this order
Foundation before features: item 3 builds the registry that 4–6 reuse. Cheap before dear: segments
(reuse) before forms (new) before sublists (biggest). Dependencies respected: forms need the
form-extensible registry; sublists need forms + line fields. Consolidation last, because it can only
test seams once the pieces exist.

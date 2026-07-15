# eProcure — Handover & Local Setup

**For:** the incoming lead .NET developer and team
**State:** tag `v2.4-handover` · all suites green · independently source-audited 2026-07-14

> **Read §6 and §7 first if you read nothing else.** They contain the single idea that determines
> whether the remaining transactions take weeks or months.

---

## 1. What this is

A full-stack Source-to-Pay procurement portal built for **Sarawak Petchem (SPSB)**, an oil & gas
company running NetSuite as their ERP. It covers the sourcing lifecycle end-to-end: purchase
requisitions → RFQ → sealed bidding & evaluation → award → purchase order → delivery (ASN/GRN) →
invoice → statements, plus vendor onboarding with financial pre-qualification.

**Stack:** ASP.NET Core Web API (.NET 10) · EF Core 10 · PostgreSQL 16 · React + Vite + TypeScript
(SPA, auth-gated, no SSR) · xUnit + Vitest + Playwright.

**Design intent:** the portal is the system of engagement; NetSuite remains the system of record.
NetSuite integration is deliberately deferred ("portal first, integration last").

---

## 2. Run it locally

**Prerequisites:** .NET 10 SDK · Node 20+ · Docker Desktop

```bash
# 1. From the repo root — start Postgres
docker compose up -d
# postgres:16 on :5432 — user eprocure / password localdev / db eprocure

# 2. API (migrations apply + dev data seeds on startup in Development)
cd api && dotnet run --project src/eProcure.Api
# http://localhost:5260 — check /api/health

# 3. Web (new terminal)
cd web && npm install && npm run dev
# http://localhost:5173
```

**Logging in.** Development uses a persona switcher, not real auth — use the "Act as" dropdown or
append `?as=<user>`. Personas: `u_admin`, `u_faridah` (Buyer), plus approver / evaluator / vendor.
**These dev routes return 404 in Production** — enforced and tested.

### What comes seeded (you don't need to create anything)

Migrations apply **and the demo data seeds automatically on startup** (`Program.cs` — `MigrateAsync()`
then `SeedAsync()`). The seeder is **idempotent**, so restarting never duplicates. Out of the box you
get a working system, not an empty one:

- **Internal users with roles** — Buyer, Approver, Technical Evaluator, Commercial Evaluator, Admin.
  These power the "Act as" switcher; no signup needed.
- **Vendors** — a set of realistic Malaysian suppliers, with vendor-side logins so you can see both
  sides of the portal.
- **Sample requisitions** in a spread of statuses, plus RFQs and purchase orders — enough to walk the
  PR → RFQ → Award → PO path immediately.
- **Segments** — Department, Location, Project, Category with realistic values, applied at header and
  line.
- **Item master** — a set of realistic items (valves, piping, PPE, instrumentation) that the PR line
  picker sources from, auto-filling description and UoM.
- **Custom lists** (Bank, Currency, Incoterms, Payment Terms, State/Country…), **entry forms**
  (Standard PR/PO/GRN/Invoice), **saved views**, and **numbering schemes**.

If you want a truly clean slate: `docker compose down -v && docker compose up -d`, then restart the
API — it re-migrates and re-seeds from scratch. **The demo data lives in the seed, not in a database
someone hand-loaded**, so every environment (yours, a teammate's, a fresh cloud deploy) comes up
identical. If you add demo data, add it to the seeder — not just your local DB — or the next person
starts empty.

**Verify your install** — a fresh seed was measured on 2026-07-15 and should reproduce exactly:

```bash
docker exec -i eprocure-db psql -U eprocure -d eprocure -c \
"select 'PRs' t, count(*) from \"PurchaseRequisitions\" union all \
 select 'CustomFieldDefs', count(*) from \"CustomFieldDefs\" union all \
 select 'Segments', count(*) from \"SegmentDefs\" union all \
 select 'Items', count(*) from \"Items\";"
```
Expected: **22 PRs · 2 custom fields · 5 segments · 10 items.** If you get those, your environment
matches everyone else's. (The seeder carries a base set of demo transactions — several e2e specs
depend on them — plus ten curated PRs layered on top; see `AUTO-DECISIONS.md`, AD-2.)

```bash
cd api && dotnet test                 # 552 tests
cd web && npx vitest run              # 256 tests
cd web && npx tsc -b                  # real typecheck — see warning
cd e2e-audit && npx playwright test   # 121 browser tests, ~5 min (boots the full stack)
```

> **Use `tsc -b`, never `tsc --noEmit`.** The root `web/tsconfig.json` is solution-style
> (`"files": []` + project references) — `--noEmit` checks **nothing** and reports success while real
> errors exist. This masked four genuine type errors historically. Only `-b` follows the references.

---

## 3. How this was built (so the history makes sense)

The git history is part of the handover — `git log` and the tags (`v0.1-slice-f` → `v2.4-handover`)
are a decision record, not just commits. The method was deliberate:

- **Slice-based.** Each unit of work got a written spec, a file plan confirmed *before* any code, then
  atomic commits with all gates green (`dotnet test`, `vitest`, `tsc -b`, `oxlint`) before each commit.
- **Proof over assertion.** A capability was never "done" because the code looked right — it was done
  when a **Playwright browser test drove it on screen** and passed. That's why the e2e suite is large:
  it's the acceptance layer, not an afterthought.
- **Rulings are recorded, not remembered.** Contentious design decisions were written down with their
  reasoning (`docs/CF-*/`, `CLAUDE.md`). Several are load-bearing — see §5.
- **Honest gates.** Where a gate turned out to be vacuous (the `tsc --noEmit` case above), it was
  found, disclosed and fixed rather than left green-and-meaningless.

Keep this discipline. It's why the audit found the codebase in the state it did.

---

## 4. The database — records and relationships (**please review this**)

This is what we most want your eyes on. The transactional spine:

```
PurchaseRequisition ──< PrLine
                            │
                            └──< PrLineSourcing   (append-only: which PR line went into which RFQ)
                                      │   PrLineId → PrLine.Id
                                      │   RfqId    → Rfq.Id
                                      ▼
                             Rfq ──< RfqLine
                              ├──< RfqInvitation        (invited vendors)
                              ├──< RfqEvent             (governance: extensions, early close)
                              └──< Bid ──< BidLine      (Bid.RfqId, Bid.VendorId)
                                       └──< BidAnswer
                              │
                            Award ──< AwardAllocation   (Award.RfqId — which bid line won)
                              │
                              ▼
                   PurchaseOrder ──< PoLine
                     PO.AwardId              → Award.Id             (lineage, "AN-2")
                     PoLine.AwardAllocationId → AwardAllocation.Id  (line-grain lineage, "AN-2")
                              │
                   ┌──────────┴──────────┐
                   ▼                     ▼
             Asn ──< AsnLine        Grn ──< GrnLine
             (Asn.PoId)             (Grn.AsnId, Grn.PoId)
                                         │
                                         ▼
                             Invoice ──< InvoiceLine
                               Invoice.PoId
                               Invoice.GrnId   (3-way-match lineage)
```

**The provenance chain is real and five hops deep.** A PO line traces back to the requisition that
started it:

```
PoLine → AwardAllocation → RFQ line → PrLineSourcing → PrLine → PurchaseRequisition
```

This matters more than it looks. It's what makes "carry a custom field's value from the PR forward to
the PO at award approval" possible — and it's why that carry-forward **only copies when the trace is
unambiguous**. A PO line consolidated from two PR lines is **skipped, with a visible note in the audit
trail**, rather than silently guessing. **Never guess through the chain.** Consolidation is a real,
supported flow; ambiguity must be surfaced, not resolved by assumption.

**Other structural facts:**
- **`PrLineSourcing` is append-only** — the audit trail of how a PR line was sourced. A PR line has a
  six-state lifecycle; this table is why you can always answer "where did this go?"
- **Vendor scoping is server-side**, never UI-side. Vendors see only their own records via the
  authorization perimeter — never trust the frontend for this.
- **~63 foreign keys**, no orphan-able lifecycle references; `xmin` optimistic concurrency on 9
  aggregates; typed dates (`DateOnly` → `date`); money is `numeric(18,2)`.
- **Statements and payments are thin by design** — payment vouchers are a NetSuite-side
  responsibility per the SOW.

**Master data — `Item` (`Procurement/Item.cs`).** A deliberately small master: `ItemCode` (unique
business key, e.g. `VLV-GT-0150`), `Description`, `Uom`, `Active`. It is a **lookup source, not a
foreign key** — transaction lines still store `ItemCode` as a **string** (they do on PR, PO, Bid,
Invoice and Delivery). The PR line *picks* from the master and auto-fills Description and UoM. This
was a conscious ruling: re-keying five line entities onto an `ItemId` FK is a wide, risky migration,
and the lookup-source design delivers "item is a list, not free text" with zero line-table change.
**If you later want the full FK, that's a deliberate migration — not something to do incidentally.**

**Dimensions are stored as label + code on the record, not as FKs.** `PurchaseRequisition` carries
`Department`/`DepartmentCode`, `Location`/`LocationCode`, `Category`/`CategoryCode`, `Job`/`JobCode`
as **plain string pairs**. The UI presents them as segment-backed searchable pickers that write both
halves. So the *dimension* is governed (segment values are managed config) while the *record* keeps a
durable, denormalised copy — a record's historical dimension text can't be mutated by someone later
renaming a segment value. Know this before you "normalise" it.

**Review request:** walk this model against your own instincts before building on it. We'd most value
a second opinion on: the append-only sourcing table, the award→PO line-grain lineage, and the
3-way-match linkage (PO + GRN → Invoice).

---

## 5. The configuration platform — how it's structured

The largest and most valuable part of the system. It lets a client **configure** the portal without a
code change — and it's what every remaining transaction inherits.

### Backend layers

| Layer | Entities | What it does |
|---|---|---|
| **Vocabulary** | `FieldRegistryEntry` | The single dictionary of every addressable field, per record type. `Kind` = `Native` \| `Custom` \| `Segment`. Native rows are seeded **mechanically from the write contract** (`EntryFormVocabulary`) — never hand-listed. |
| **Custom fields** | `CustomFieldDef`, `CustomFieldDefApplication`, `CustomFieldValue` | A field is defined **once** and applies to **many** record types (the Application join). Values use **sparse typed columns** (`ValueText/Number/Money/Date/Bool/ListCode/DateTime`) with two CHECK constraints enforcing exactly-one-populated and type-matches-column. `LineId` (nullable) gives line scope; uniqueness is **two partial indexes** (header: `WHERE LineId IS NULL`) — deliberately version-independent rather than `NULLS NOT DISTINCT` (PG15+). |
| **Lists** | `CustomList`, `CustomListValue` | Reusable value sets. Supports **dependent lists** (State depends on Country) *and* **value-level parent/child trees** — both ride the same `ParentValueCode` column with mutually-exclusive semantics (documented in code; don't "helpfully" add an FK). |
| **Segments** | `SegmentDef`, `SegmentValue`, `SegmentApplication`, `SegmentAssignment` | Reporting dimensions. `SegmentApplication` is keyed `(SegmentDefId, RecordType, LineLevel)` so one segment applies at **header and line** of the same record — one dimension, one `custseg_` id, two levels. |
| **Forms** | `EntryFormDef`, `EntryFormSubtab`, `EntryFormGroup`, `EntryFormField`, `EntryFormRoleMap` | Per-record-type layouts. **`EntryFormField` is the single placement authority** — one row per (form, field) carrying `GroupId`, `Sort`, `DisplayType`, `RequiredOnForm`. Both the field-creation cascade *and* the form designer write these same rows. |
| **Views** | `SavedView` + filters + columns | Criteria, columns, sharing; feeds dashboards/KPIs. Works over Native, Custom **and** Segment keys via the registry. |
| **Numbering** | `NumberingScheme` | Per-record-type document numbering, gap-free under concurrency (atomic `INSERT..ON CONFLICT..RETURNING` with a row lock). |

### Frontend contract

- **One `FieldSpec` type, one `renderField` dispatch.** Every field — native, custom, segment —
  renders through the same path. There are **zero** rival Field components, and it must stay that way:
  the entire config layer depends on this being the single road.
- **One `SearchSelectField`** for every list-style pick (typeable, keyboard-navigable).
- **Every admin screen follows the same shape:** a list → **Open** → full page → **Back** (with a real
  dirty-state guard that warns only when something actually changed). Entry Forms, Custom Lists,
  Segments, Numbering and Item Master all use it. **Build new admin surfaces this way** — the
  consistency is deliberate, not incidental. (Custom Fields keeps a modal editor behind the same list
  shell; see `AUTO-DECISIONS.md`.)
- Design language: square corners, Georgia headings, Inter UI, teal/cream. Motion/banner/skeleton
  conventions in `docs/CF-FINAL/UI-POLISH-REFERENCE.html`. Every save fires a confirmation banner or
  toast — copy is past tense, no "successfully", no exclamation marks.

### Two rules that are load-bearing — **do not break these**

1. **The reference registry (anti-rot).** Before a custom field or list value can be deleted, the guard
   asks *every registered provider* "do you reference this?" — **the guard never names a consumer.**
   Add a feature that references field values, implement one interface, DI-register it, and it appears
   in the impact report **automatically**. `ExtensibilityProofTests` proves this with fake providers and
   carries a do-not-weaken banner. If you ever write `if (usedInAnalytics)` inside the guard, the design
   is broken — fix the design, not the test.
2. **Layout ≠ data.** Removing a field from a form deletes only the placement row, **never** stored
   values. Retiring data is a separate governed lifecycle: **Inactivate** (hide from entry) →
   **Archive** (hide values everywhere, reversible, audited) → **Delete** (only when config-clean and no
   live values) → **Purge** (admin-only, historical records only, snapshots to audit first).
   `RemoveFromFormDataSafetyTests` pins it.

---

## 6. ★ The framework thesis — read this before you build anything

**PR is not "the first transaction." It is the reference implementation of a framework that every
other transaction reuses.**

This is structural, not aspirational. `RecordType` is
`{ Requisition, Rfq, PurchaseOrder, Invoice, Asn, Vendor, Onboarding, Grn }` — and **every config table
is keyed by it**: `FieldRegistryEntry.RecordType`, `CustomFieldDefApplication.RecordType`,
`CustomFieldValue.RecordType`, `SegmentApplication.RecordType`, `EntryFormDef.RecordType`,
`SavedView.RecordType`. The domain says so in a comment: *"Native = seeded from the list DTOs; Custom
and Segment are RESERVED — later slices add rows, never reshape."*

**Consequence: PO, GRN, Invoice and ASN are already in the enum. The configuration layer already works
for them.** Custom fields, segments, entry forms, saved views, the lifecycle, the impact report — none
of it needs rebuilding per transaction. It is record-type-generic **by design**.

### The recipe for adding a transaction to the framework

1. **Ensure the `RecordType` member exists** (all P2P ones already do).
2. **Seed its native `FieldRegistry` rows** — derived **mechanically from that record's write
   contract** (`EntryFormVocabulary`), never hand-listed. If a field isn't in the write contract it
   isn't form-controllable — that rule keeps the vocabulary honest.
3. **Seed a Standard entry form**, with the always-present `Header` field group (the Header invariant
   guarantees the field-placement cascade can never dead-end).
4. **Custom fields, segments, lists, saved views, numbering, the lifecycle and the impact report all
   work automatically.** No per-transaction plumbing.
5. **The only genuinely new work is:**
   - the **write contract** and business rules for that record,
   - the **relationship** to the record upstream of it (the FK + the provenance link),
   - the state machine / transitions.

**So: harden the framework once, and PO → GRN → ASN → Invoice → CN → Statement each become "apply the
recipe + wire the relationship."** That is the whole strategy. The alternative — building each
transaction bespoke — throws away the entire investment and produces six divergent implementations.

**Where hardening pays off most:**
- **The placement/cascade path** (`EntryFormField` as sole authority) — the seam every transaction
  touches.
- **The reference registry** — every new consumer must be discoverable, or the anti-rot guarantee
  decays the first time someone forgets.
- **The lifecycle tiers** — the more record types hold values, the more Archive/Purge governance matters.
- **Form resolution + `OD-D7-2`** — a chosen form changes *layout only*; the server always re-resolves
  the role form's required fields at submit. A chosen form must never dodge a requirement.

---

## 7. Suggested mission order

1. **Push to a repo (org account, not personal) and watch CI run for the first time.** It has never
   run — §8. Job zero.
2. **Walk and review the data model** (§4), especially the provenance chain.
3. **Prove the PR → RFQ → Award → PO path end-to-end in your own hands.** It's the spine; everything
   downstream is a variation on it.
4. **Harden the framework** (§6) *before* building PO's surfaces — every later transaction compounds
   whatever state you leave it in.
5. **Build PO using the recipe**, not bespoke: same entry-form framework, same custom-field/segment
   placement, same lifecycle. If you find yourself writing PO-specific config plumbing, stop — the
   framework should already cover it, and if it doesn't, that's a framework gap worth fixing once.
6. **Then GRN → ASN → Invoice → CN → Statement**, each as "recipe + relationship."
7. **Decide the config-promotion approach** before client #2 (§8).

---

## 8. Known open items (honest list — nothing hidden)

- **CI has never actually run.** The workflow is correct (`dotnet test` against a real Postgres service
  container, `tsc -b`, `-warnaserror`) but every commit to date was made **locally and never pushed**.
  Push, and make green CI the merge gate.
- **Config promotion is unsolved.** A rich config layer with **no way to move configuration between
  environments** — a client configures in Sandbox, signs off, and someone re-keys it by hand in
  Production. NetSuite solves this with SDF/bundles. Recommended: build a config export/import (the
  config *is* just rows; the domain boundaries make it tractable). **This will bite at the first client
  go-live.**
- **Production Postgres version (Azure Malaysia West) undocumented.** Dev is PG16. Custom line-field
  uniqueness deliberately uses **version-independent partial indexes** rather than `NULLS NOT DISTINCT`
  (PG15+) *because* prod's version was unverified — a documentation gap, not a correctness risk.
  Capture it before any prod deploy.
- **Migration squash pending** — 105 migrations including dev-era iterations.
- **F2 intermittent** — an unreproduced `08-entry-forms` flake under full-suite runs; mitigated with
  self-healing setup, root cause pending. Keep the trace instrumentation; don't dismiss it as flaky.
- **Docs folder needs a scrub** — historical audit reports contain a personal email. **Zero occurrences
  in `api/src` or `web/src`.**
- **Deferred by design:** NetSuite integration; Contract Management (SOW §4.7); payment vouchers;
  sublist objects (B4) and segment-search/result-grouping (B5) — designs intact in the CF blockers.

---

## 9. Multi-client topology (context for later)

There is **no tenant concept** (no `TenantId`; one connection string per deployment) — so the model is
**silo per client**, which is correct for enterprise procurement.

- **Dev:** ONE shared environment, not per client — clients *configure*, they don't fork code.
- **Sandbox + Production:** one each per client.
- **The "vendor portal" is not a separate system.** Vendors and buyers share one API and DB, separated
  by the authorization perimeter. A separate vendor *frontend* buys network zoning, blast radius and
  release cadence — **not** data security (a vendor with devtools calls the same API regardless).
  Recommended: same API/DB, two hostnames.
- **If vendors need one login across clients:** use a **shared identity provider** (centralised auth)
  and keep **data siloed**. Do **not** retrofit pooled multi-tenancy — that's a redesign of the data
  model and the authorization perimeter.

---

## 10. Read the docs in this order

| Doc | Why |
|---|---|
| `CLAUDE.md` | Build conventions and rulings — read first |
| `docs/design-framework/CHARTER.md` | UI/field-contract rules |
| `docs/SOURCE-AUDIT-*.md` / `AUDIT.md` | Independent five-lens audit — the honest state |
| `docs/CF/CUSTOMIZATION-ROADMAP.md` | What's built, what's deferred, in dependency order |
| `docs/AUTHORIZATION-MATRIX.md` | The role/action model |
| `docs/CF-*/` | Per-slice build prompts + ledgers — how each round was specified and proven |

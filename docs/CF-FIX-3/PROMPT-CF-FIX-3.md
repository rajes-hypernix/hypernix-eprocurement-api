# BUILD PROMPT — CF-FIX-3: reference registry, impact report, three-tier lifecycle

You are Claude Code on eProcure. This slice fixes the deepest gap the operator found in testing:
custom fields and list values can be deleted/changed without knowing what depends on them, and the
dependency-check today is neither complete nor extensible. The operator's explicit requirements:

1. **Delete must be possible** (they're cleaning up dummy transactions before handover) — but safely.
2. **You can't touch closed/historical transactions** — a value on a 5-year-old closed PR must not be
   silently edited.
3. **Destructive actions need explicit confirm + an impact report** ("where is this used: which forms,
   is it mandatory, which saved views, what else") — never auto-save.
4. **THE ARCHITECTURE REQUIREMENT (most important):** when a NEW feature is added later (a new
   analytics function, a new record type, a new place values are stored), the usage/validation check
   must pick it up **automatically**. The operator must NEVER have to come back to the custom-field
   validation code and manually register a new consumer. "I don't think that's good architecture" —
   correct. Build the extensible version.

Read this whole prompt, then **START WITH STEP 0** (full file plan per task), and **WAIT for my
confirmation** before writing code. Read first: `CLAUDE.md`, `docs/design-framework/CHARTER.md`,
`CustomFieldService.cs`, `CustomListService.cs`, the `IAuditLog` abstraction, `EntryForms.cs`,
the Views + Segments domain, and the record status enums (PoStatus, InvoiceStatus, PrLineStatus, etc).

**Baselines (hold-or-raise):** dotnet 506 · vitest 237 · e2e 86. Atomic commits `CF-FIX3-Tn:`, all
gates green before each. Every user-facing change gets a browser test in a new
`e2e-audit/tests/13-cf-fix3.spec.ts`. Hard boundaries unchanged.

---

## THE CORE IDEA — two provider registries (this is the anti-rot foundation; build it first)

The failure mode to avoid: a `CanDelete()` method that hard-codes `if (usedInForms || usedInViews ||
usedInAnalytics)`. That rots the instant someone adds a feature and forgets to update it. **Invert the
dependency:** every consumer registers itself; the guard loops over registered providers and never
names a consumer.

Two interfaces (config-references and data-references are DIFFERENT and treated OPPOSITELY — see the
three tiers):

```
// A structural/config reference: a form placement, a view column/filter, a segment, (later) analytics.
public interface ICustomFieldReferenceProvider {
    // For a given field code (and record type), return any config references held by THIS consumer.
    Task<IReadOnlyList<FieldReference>> FindReferencesAsync(string fieldCode, RecordType type, CancellationToken ct);
    string ConsumerName { get; }  // "Entry Forms", "Saved Views", "Segments", (later) "Analytics"
}

// A data reference: a stored VALUE on an actual record, WITH that record's status (live vs closed).
public interface ICustomFieldDataProvider {
    // Return how many values exist for this field, split by record lifecycle (Live vs Historical).
    Task<DataReferenceSummary> CountValuesAsync(Guid fieldDefId, CancellationToken ct);
    // Purge historical-only values for this field (used by Tier-3 purge); returns what it removed for the snapshot.
    Task<PurgeSnapshot> PurgeHistoricalAsync(Guid fieldDefId, CancellationToken ct);
    string StoreName { get; }
}
```
Same pattern for **list values** (`ICustomListValueReferenceProvider` / data provider) — a list value can
be a saved-view filter *value*, and can be *held by records*.

**Registration:** providers are DI-registered as a collection; the guard/report inject
`IEnumerable<ICustomFieldReferenceProvider>` and `IEnumerable<ICustomFieldDataProvider>` and loop. **No
consumer is ever named inside the guard.** The proof this is right is an explicit test (see T-LAST).

**`FieldReference`** carries: ConsumerName, a human label ("Buyer PR Form"), the reference kind
(FormPlacement / ViewColumn / ViewFilter / SegmentDim / …), and **flags like `IsMandatory`** (for a
form) so the report can red-flag the dangerous ones.

**FORM-EXTENSIBILITY (required — a later slice depends on this):** the `EntryFormReferenceProvider`
must report placement at the **per-form** grain, not just "used in the form engine." A field's
reference must identify *which specific form* it's placed on (by form id/name), because a future
slice adds "a field applies to specific forms within a transaction type" (today it applies to
types only). Build the provider so that when per-form applicability arrives, it is already a
config-reference the registry surfaces — do NOT bake in an assumption that form placement is a
single static fact per field. If this costs more than a small amount now, still do the per-form
grain in the reference shape (the report can show "on 3 forms" today); the point is the DATA SHAPE
is form-aware so the later work slots in without retrofitting the registry.

**`DataReferenceSummary`** carries: `LiveCount`, `HistoricalCount`, and a breakdown by record type. **Live
vs Historical is determined by each record type's terminal status** (PO: Closed/Matched/Discrepancy;
Invoice: Paid; PR line: Closed/Cancelled; RFQ: closed states — enumerate per type). **Fail closed:** if a
provider cannot determine a record's status, it counts as **Live** (never Historical), so unknown status
can never be purged.

---

## T1 — the two registries + first providers — `CF-FIX3-T1:`
- Define the interfaces + `FieldReference` / `DataReferenceSummary` / `PurgeSnapshot` in Application.
- Implement the first CONFIG providers: **EntryFormReferenceProvider** (returns placements + `IsMandatory`),
  **SavedViewReferenceProvider** (columns + filters; for list values, filter-values too),
  **SegmentReferenceProvider**.
- Implement the first DATA provider: **CustomValueDataProvider** — counts `CustomFieldValue` rows for a
  def, joining each value's `(RecordType, RecordId)` to that record's status to split Live vs Historical.
  This is the join that makes the strict purge possible.
- DI-register all as collections.
- xUnit: each provider returns correct references for a seeded fixture; the data provider correctly splits
  a value on a Draft PO (Live) vs a Closed PO (Historical).

## T2 — the impact report endpoint — `CF-FIX3-T2:`
- `GET /custom-fields/{id}/references` and `GET /custom-lists/values/{valueId}/references` → aggregate ALL
  registered providers (config + data) into one report DTO: config references grouped by consumer (with
  mandatory-flags), plus the data summary (Live count, Historical count, by record type).
- **Admin-scoped** (same action as the config screens — the report reveals system structure; do not expose
  it to lower roles).
- xUnit + it must reflect a NEW provider automatically (tested in T-LAST).

## T3 — the three-tier destructive flow (fields) — `CF-FIX3-T3:`
The lifecycle, enforced server-side and surfaced in the UI with **explicit confirm, no auto-save**:

- **Tier 1 — Inactivate:** always available. Hides from entry + config surfaces; nothing removed.
- **Tier 2 — Delete:** allowed ONLY when **(a) zero config references** (all forms/views/segments cleared —
  the impact report is the admin's to-do list) AND **(b) zero LIVE data values.** If config refs exist →
  refused, report shows exactly what to clear. If live values exist → refused, report shows the open records.
- **Tier 3 — Purge (historical-only, governed):** when config is clean and the ONLY remaining data is on
  **historical/closed** records, an admin may run **Purge**: it (1) writes a **snapshot** of every value it
  will remove to the `IAuditLog` (what record, what value — so history is auditable), (2) clears ONLY those
  historical values via the data providers' `PurgeHistoricalAsync`, (3) deletes the field. **Core transaction
  data is never touched — only the retired field's values.** Purge NEVER touches live records (strict rule).
- **Permission:** Purge is gated on a NEW higher-tier action (e.g. `PurgeCustomFieldHistory`), NOT the normal
  manage-custom-fields action — it's more consequential. Snapshot is mandatory. The whole check + purge runs
  in ONE transaction (re-verify references inside it — no check-then-act race).
- **UI:** Deactivate / Delete / Purge each open a confirm dialog that shows the impact report FIRST. Delete is
  disabled with a reason when refs/live-values exist. Purge appears only when eligible and warns clearly
  ("this permanently removes the field's values from N closed records; a snapshot is kept").

Browser tests: a field on a form → Delete disabled, report lists the form (mandatory in red); clear the form →
Delete still disabled because a Closed-PO value remains → Purge offered → purge → field gone, snapshot in audit.

## T4 — the same three tiers for LIST VALUES — `CF-FIX3-T4:`
The `X` on a list value currently auto-deletes with no confirm — **the exact auto-save sin, in a dangerous
place.** Replace with the three-tier flow:
- `X` → confirm dialog showing the value's impact report (used as a view filter-value? held by N records,
  split live/historical?).
- Inactivate (always) / Delete (config-clean + no live record usage) / Purge historical (governed, snapshotted).
- **A value held by live records blocks delete**; a value that's a live saved-view filter-criterion blocks
  delete (report says "remove it from view X first"). No auto-save — nothing happens until confirm.

Browser tests: value used as a view filter → X shows the dependency, delete blocked; unused value → delete
after confirm; value on a closed record only → purge offered.

## T5 — server-enforced type immutability + recreate guidance — `CF-FIX3-T5:`
- The field **type stays immutable** (the operator agreed: change-type = inactivate & recreate, cleaner than
  NetSuite's in-place-with-warning, because a type change silently poisons saved-view filters). **Enforce
  server-side** — a type change past the UI returns 400 (a UI-only block is not enough; the API is the guarantee).
- UI: the edit modal shows type read-only with helper text "To change the type, inactivate this field and
  create a new one," and a **"Create replacement"** action that pre-fills a new-field modal from this field's
  settings (label, scope, applies-to) so recreating isn't painful.

Browser test: type is read-only in edit; API type-change → 400; "Create replacement" pre-fills.

## T-LAST — the extensibility proof (the operator's core requirement) — `CF-FIX3-T6:`
This test is the whole point. Add a **throwaway fake provider** in the test project — a
`FakeAnalyticsReferenceProvider` that claims field X is "used in Dashboard Z" — register it, and assert:
- the impact report for field X **now shows "Analytics: Dashboard Z" WITHOUT any change to
  CustomFieldService, the guard, or the report code** — purely because the provider was registered;
- Delete of field X is now refused because of that reference.
Then a `FakeHistoricalValueProvider` (a fake data store holding a value on a "closed" record) — assert the
data summary counts it as Historical and Purge includes it — again with NO edit to the guard.
**If this test requires touching the guard/report to pass, the architecture is wrong — fix the architecture,
not the test.** Document in the report: "to add a new consumer, implement one interface and DI-register it;
never edit the custom-field lifecycle code." This is the anti-rot guarantee, proven.

---

## SEQUENCING
T1 (registries) → T2 (report) → T3 (field lifecycle) → T4 (list-value lifecycle) → T5 (type immutability) →
T6 (extensibility proof). T6 can be written alongside T1 (define the fake providers early) but asserted last.

## GAPS / SECURITY the plan must close (operator asked explicitly)
- **The live bug:** today a zero-VALUE field hard-deletes even if it's a saved-view column (guard checks
  values only, not config refs). T3 closes it — verify with a test that reproduces the old bug then passes.
- **Fail-closed status:** unknown record status = Live = never purgeable.
- **Purge is transactional + snapshotted + higher-permission.** No check-then-act race.
- **The impact report is admin-scoped** (it maps system structure).
- **List-value data references count RECORD usage**, not just config — a value held by records isn't "unused."

## PER-TASK & FINAL
Each task: Step-0 confirmed → build → gates green → atomic commit → browser test → note. Final report:
five-lens review; the three-tier model explained; the extensibility proof result (T6) called out prominently;
the full gap/security closure list; and a line-by-line map of the operator's requirements → commits → proofs.
Tag `v1.8-cf-fix-3`.

Do not begin coding until I confirm your Step 0 file plan.

---

## APPENDIX — operator's requirements verbatim
- Delete dependencies/references first, THEN delete; otherwise inactivate only.
- A value on a closed 5-year-old PR: user can't touch a closed transaction — an admin (with permission) can
  clear the field value / remove the criteria. Must have delete (cleaning dummy transactions before handover).
- On inactivate/delete: show what forms it's on, is it mandatory there, is it in a saved view or any other
  element. Any inactive/delete needs Save-to-confirm — no auto-save.
- FUTURE-PROOF: if I add a new analytics function / record / transaction and field values are used there, it
  must auto-show in this message. I can't have to come back to this code and add it to the validation. That's
  not good architecture — build it so new consumers are picked up automatically.
- Think about validation gaps and security overall for custom lists and fields.

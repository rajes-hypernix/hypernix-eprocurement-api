# CF5–CF7 — LOCKED DECISIONS (operator-reviewed) — read before Phase 3

The three Step 0 plans were reviewed against the live code. Verdict: **CF5 approved as
written, CF6 approved with ONE mandatory safety gate added, CF7 approved as written.** These
rulings supersede the "auto-decision gate" in PROMPT-CF-LONGRUN for the CF5–CF7 open decisions
below — they are DECIDED here, not to be auto-chosen. Where a decision is locked, follow it;
do not re-litigate. Log to AUTO-DECISIONS.md only the decisions NOT covered here (there should
be none of consequence).

---

## CF5 — Entry-form layout editor — APPROVED, one hardening

**Locked decisions:**
- D1 (FK replaces Subtab/FieldGroup strings): **APPROVED.** Proceed.
- D2 (hidden subtab + required field): **warn-but-allow — LOCKED.** Roles differ; a field
  required for Buyer may live on a subtab hidden for Approver. Blocking would break legitimate
  per-role layouts. The editor warns on hide; the server keeps enforcing required at submit
  (hidden ≠ not required — the D7 rule extends).
- D3 (two-column canvas only): **APPROVED — LOCKED.** Matches the existing g2 grid.

**MANDATORY HARDENING (added by review):** CF5-T1's parity snapshot gate is a **STOP gate**, not
a soft check. The `EntryFormLayout` migration rewrites how every form resolves (string subtab/
group → FK objects with a backfill). If the Standard form does NOT render **byte-identical**
before vs after the migration (existing parity test + snapshot diff), **STOP CF5, log it in
BLOCKERS as "form-resolution parity broken — needs operator review," and do not build T2–T5 on
a broken foundation.** "Close enough" is unacceptable for a migration that touches every form.

---

## CF6 — Custom line fields + sublists — APPROVED MODEL, ONE SAFETY GATE

**Locked decisions:**
- The LineId model: **option (b) — nullable `LineId` discriminator on the existing
  `CustomFieldValue` table — LOCKED.** Verified against live schema: existing unique index is
  `(FieldDefId, RecordId)`, CHECK is ExactlyOne-populated-column; adding nullable LineId with
  null=header means today's rows are unchanged and no backfill is needed. Option (a) (separate
  table) is rejected (permanent double-maintenance of the six-column machinery); option (c)
  (JSON) violates the typed-core charter rule. Correct call.
- `CustomFieldDef.Scope` (Header|Line, immutable after create): **APPROVED — LOCKED.**
- Closed SublistKey set per record type (no user-defined sublists this slice): **APPROVED —
  LOCKED.** User-defined sublists are CF7+ territory.
- Line-value view/aggregate deferred to post-CF6 (runner stays header-grain): **APPROVED —
  LOCKED.** Honest deferral; record it in the ledger.
- First delivery = PR/PO/RFQ lines; ASN/GRN lines follow the same pattern later: **APPROVED.**

**MANDATORY SAFETY GATE (added by review — this is the one landmine):**
The plan's uniqueness guarantee relies on `NULLS NOT DISTINCT` on the replaced unique index.
**`NULLS NOT DISTINCT` is PostgreSQL 15+.** Dev/CI is PG16 so it will pass every test tonight —
but PRODUCTION is Azure Malaysia West and its Postgres version is NOT verified here. If prod is
PG14 or earlier, this index behaves DIFFERENTLY in prod than in test: two header rows with
`LineId = NULL` would be treated as distinct and the uniqueness guarantee silently evaporates in
production only. That is a green-all-night / corrupt-in-prod bug — the exact class we must not
ship unsupervised.

**Therefore, CF6-T1 does NOT use `NULLS NOT DISTINCT`.** Instead, implement the header-row
uniqueness in a **version-independent** way that behaves identically on PG13–PG16:
- Preferred: **two partial unique indexes** — one `WHERE "LineId" IS NULL` on
  `(FieldDefId, RecordId)` (guarantees one header value per field per record), and one
  `WHERE "LineId" IS NOT NULL` on `(FieldDefId, RecordId, LineId)` (one value per field per
  line). Partial indexes with `IS NULL`/`IS NOT NULL` predicates work on all supported PG
  versions and give the exact guarantee without depending on NULL-distinctness semantics.
- Record this in AUTO-DECISIONS.md as "chose partial-index uniqueness over NULLS NOT DISTINCT
  for prod-version safety" with the reverse (swap to NULLS NOT DISTINCT) noted, IF the operator
  later confirms prod is PG15+ and prefers it.
- Also: add a one-line note to BLOCKERS/report asking the operator to confirm the prod Postgres
  version, so the choice can be revisited with facts.

**Second CF6 guard (the operator's own load-bearing concern):** if any part of CF6-T1 would
require CHANGING the existing header-value storage or the ExactlyOne/KindMatch CHECKs in a way
that could regress the now-passing custom-field tests from the Phase-1 sweep, **STOP CF6, log
it, proceed to CF7.** A regression of working custom fields is worse than deferring CF6. Adding
a nullable orthogonal `LineId` column should NOT touch those CHECKs (LineId is not a value
column) — verify that holds; if it doesn't, stop.

---

## CF7 — Saved View → Saved Search — APPROVED AS WRITTEN

**Locked decisions:**
- D1 (grouped-OR one level vs full nesting): **one-level grouped-OR — LOCKED.** Matches
  NetSuite's actual AND-of-ORs criteria UI; full nesting costs more than the parity buys.
- D2 (persist default grouping vs run-time only): **run-time first, persist-on-pull — LOCKED.**
  Do NOT build the `ViewDefaultGrouping` migration in this slice; grouping is a run parameter.
  If the operator later wants it persisted, that one nullable column is a follow-up.
- D3 (in-memory runner strain): **APPROVED** — operators/OR-groups don't change the cost
  profile; the `IScopedQuerySource<TDto>` seam remains the documented escape hatch.
- Null semantics (Neq/NotContains/NotIn EXCLUDE nulls; IsEmpty is the explicit null ask):
  **APPROVED — LOCKED,** and write these into the runner tests BEFORE the operators land (the
  plan already says this — good).
- Segment-search via a `SegmentValueRow` projection rather than a fake RecordType: **APPROVED.**
  Verified: RecordType is stored `HasConversion<string>`, so adding a `SegmentValue` member is a
  seed-only change, not a destructive migration. The `SegmentSearchClass` migration is only
  needed if the enum is DB-mapped in a way that requires it — if it's purely seed data, no
  migration; confirm and note which.
- User/Item entity classes DEFERRED, customizations-as-a-class DROPPED: **CONFIRMED** (operator's
  standing rulings).

---

## Summary for the run
- CF5: build as planned; T1 parity is a STOP gate.
- CF6: build as planned EXCEPT use partial-index uniqueness (not NULLS NOT DISTINCT); stop if
  header-value storage/CHECKs would change; flag prod PG version for the operator.
- CF7: build as planned; no `ViewDefaultGrouping` migration; segment-search as a projection.
- Everything else in the three Step 0 plans stands. Proceed through Phase 3 after Phases 1–2.

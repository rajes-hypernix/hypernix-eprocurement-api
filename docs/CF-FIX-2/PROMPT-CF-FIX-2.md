# BUILD PROMPT — CF-FIX-2: shared fields, NetSuite prefixes, design unification, save-button discipline

You are Claude Code on eProcure. This is round 2 of the operator's hands-on testing of Custom
Fields / Custom Lists (screenshots in `docs/CF-FIX-2/reference-screenshots/` — VIEW THEM). Five
findings, one of which (T3, shared fields across transactions) is a genuine re-architecture that
needs its own care. Read this whole prompt, then **START WITH STEP 0** — full file plan per task,
then **WAIT for my confirmation** before writing code.

Read first: `CLAUDE.md`, `docs/design-framework/CHARTER.md`, and current
`CustomFieldService.cs`, `CustomFieldsSection.tsx`, `AdminCustomFields.tsx`, `AdminCustomLists.tsx`,
`renderField.tsx`, `SearchSelectField.tsx` (the CF-FIX-1 searchable select), `index.css`.

**Baselines (hold-or-raise):** dotnet 504 · vitest 237 · e2e 81. Atomic commits `CF-FIX2-Tn:`,
all gates green (dotnet test, vitest, tsc, oxlint 0) before each. Every user-facing change gets a
browser test in a NEW `e2e-audit/tests/12-cf-fix2.spec.ts`. Hard boundaries unchanged (no Payment
Vouchers / Contract Mgmt / NetSuite integration / ApprovalThresholdMyr). Only the migrations named
here are expected; an unplanned one → STOP and report.

---

## T1 — NetSuite-style contextual internal-id prefix + fix the modal alignment — `CF-FIX2-T1:`

**The finding (screenshot 1):** the "INTERNAL ID" field shows a `cf_` prefix floating *outside and
below-left* of the input, misaligned — reads as rubbish. And the prefix is a flat `cf_`. The
operator wants NetSuite's **contextual** prefix convention, applied everywhere:

- **Body (Header-scope) custom field** → **`custbody_`** + the user's id part.
- **Line (Line-scope) custom column field** → **`custcol_`** + the user's id part.
- **Custom list** → **`custlist_`** + the user's id part.
- (If segments follow the same convention in NetSuite it's `custrecord_`/`custsegment_` — do NOT
  extend to segments in this task unless trivial; scope this to fields + lists. Note it if you see
  a clean extension.)

**Implementation:**
- The prefix is **derivable, no new data**: `CustomFieldDef.Scope` (Header|Line — already exists)
  determines `custbody_` vs `custcol_`; custom lists use `custlist_`. Derive the prefix at save
  from Scope (fields) / entity (lists). When the user types `customer`, the stored code becomes
  `custbody_customer` (header field), `custcol_customer` (line field), or `custlist_customer`
  (list). The user types only the meaningful part; the system guarantees the namespace — exactly
  NetSuite.
- **Migration `CustomFieldPrefixConvention`:** the existing `cf_*` codes need handling. **Do NOT
  rename existing stored codes** (that would orphan every stored value keyed by code). Instead:
  new fields/lists get the contextual prefix; existing `cf_`/list codes stay as-is (they still
  resolve). Document this in the migration and the report — it's the non-breaking choice, same
  discipline as the CF-FIX-1 value-id decision. If the operator later wants a one-time backfill
  rename with a value-migration, that's a separate deliberate task.
  - Actually: since prefix derives at CREATE and existing codes are immutable, this may need **NO
    migration at all** (the change is in the save path + display, not the schema). Confirm in Step
    0 — if no column changes, no migration; just the derive-at-save logic + UI.
- **UI fix (the alignment):** the prefix must render **inside the field as a fixed, non-editable
  leading affix** (like `custbody_[   customer   ]`), not floating below. The user types into the
  editable part after the affix. Remove the "auto-suggested from the label" text from *inside* the
  input (it's a placeholder leaking as a value in the screenshot) — auto-suggest by actually
  populating the editable part from the label as they type, with a real placeholder only when empty.
- Apply the same affixed-prefix pattern to the custom-LIST internal-id field.

Browser test (12-cf-fix2): create a header field with id `customer` → stored `custbody_customer`,
shown with the affix inside the field; create a line field → `custcol_`; create a list → `custlist_`.

---

## T2 — Apply the searchable-select style to ALL list-type fields portal-wide — `CF-FIX2-T2:`

**The finding:** the CF-FIX-1 searchable select (`SearchSelectField`) was wired to the field-type
picker and a few admin spots, but **the operator sees plain/unstyled selects still in use for
other list-type fields across the portal.** Make the searchable select the standard for EVERY
place a user picks from a list.

- Audit every `<select>` and every list/dropdown field in the app (custom-field List/Record
  values on records, custom-list value pickers, dependent lists, segment pickers, any native
  list-backed field, filter-bar dropdowns where a list is the source, entry-form list fields).
- Route all of them through `SearchSelectField` (via `renderField` where it's a FieldSpec field;
  directly where it's a standalone control). Type-to-filter, full names, keyboard nav, Ledger
  tokens — consistently.
- Do NOT change native browser `<select>`s that are genuinely non-list (e.g. a fixed 2-option
  toggle) unless it improves consistency cheaply — use judgement; the target is "every LIST pick
  is the searchable component," not "zero native selects exist."
- This consumes T1's work and must stay one shared contract (no rival component — charter rule).

Browser test: pick a list-type custom field value on a PO via the searchable select; confirm a
previously-plain list dropdown elsewhere (name the screen) is now the searchable component.

---

## T3 — Custom fields become SHARED and applied to multiple transactions (the re-architecture) — `CF-FIX2-T3:`

**The finding (the big one):** today a custom field is **one def bound to one `RecordType`**
(verified: `CustomFieldDef.RecordType` is singular). So to have "Warranty Expiry" on both PR and
PO you create it **twice** — two defs, two columns — and when a **PR transforms to a PO the value
does not carry**, because they're literally different fields. The operator wants NetSuite's model:
**create a custom body/line field ONCE, then choose which record types it applies to**; and when a
PR→PO transform happens, a shared field's value **carries forward**.

**This is real re-architecture — scope it carefully in Step 0 before building.** Proposed shape
(confirm/adjust with me before writing):

- **Model:** replace `CustomFieldDef.RecordType` (single) with a **many-to-many**: a def has a set
  of applicable record types. New join entity `CustomFieldDefRecordType (FieldDefId, RecordType)`,
  OR a `CustomFieldDefScope` set — pick the cleaner one in Step 0. The def is authored ONCE (label,
  id, type, scope, validation); then the admin picks **which record types it applies to** (a
  multi-select — use T2's searchable multi-select, which the CF-FIX-1 report says is built but had
  no consumer — THIS is its consumer).
- **Value storage:** `CustomFieldValue` already carries `(FieldDefId, RecordType, RecordId, LineId)`.
  A shared def now legitimately has values under multiple RecordTypes — the storage already
  supports it; the change is that ONE def is the source, not N duplicate defs.
- **Migration `SharedCustomFields`:** convert existing per-type defs. **The hard question is
  existing duplicates:** if today there are two defs "Warranty Expiry" (one PR, one PO), do they
  merge into one shared def or stay separate? **Safest: do NOT auto-merge** (can't prove two
  same-named defs are semantically identical); migrate each existing def to a shared-capable def
  that applies to its current single record type (behaviour identical to today), and let the admin
  going forward create shared ones and, if they want, consolidate manually. Document this. New
  fields get the multi-apply picker; existing ones are grandfathered to their one type. Confirm
  this migration strategy with me — it's the load-bearing decision.
- **The PR→PO value carry-forward:** when a PR transforms to a PO (find the transform path — it may
  not copy custom values today), for every custom field that **applies to both** PR and PO, copy
  the value from the PR record to the new PO record. This is the payoff the operator wants. If no
  PR→PO transform path exists yet (custom values were never copied because fields were per-type),
  building the copy is part of this task — but if the transform itself is elsewhere/complex, STOP
  and report scope rather than build a transform.
- **UI:** the custom-field create/edit modal gains an **"Applies to" multi-select of record types**
  (the searchable multi-select). The Custom Fields screen's per-record-type rail still works (shows
  fields that apply to that type), but a field can now appear under multiple tabs because it's
  shared. Make that legible (e.g. a "shared" badge, or it simply lists under each type it applies to).

Browser tests: create ONE field, apply it to both Requisition and Purchase Order; enter a value on
a PR; transform/create the PO from that PR; confirm the value carried. Confirm a field applied to
3 types appears under all 3 rails and is edited in one place.

**Because T3 is the largest and riskiest, present its Step 0 sub-plan separately and in most
detail — model choice, migration strategy, transform-copy path — and WAIT for my explicit
confirmation on those three before writing T3 code.**

---

## T4 — Square-corner design unification (portal-wide) — `CF-FIX2-T4:`

**The finding:** the design mixes rounded and square corners; the operator wants **square corners
everywhere** to unify (matching the Ledger brand's square-corner rule). Verified: ~82 border-radius
declarations, ~35 non-square.

- Audit all corner-radius: `border-radius` in CSS, `rounded-*` Tailwind classes, inline
  `borderRadius`. Set them to square (0) **except** where a radius is semantically required
  (avatars/pills that are meant to be circular — use judgement; the brand rule is "square corners"
  for panels, inputs, buttons, cards, modals, selects).
- Centralize: if there's a design-token / CSS-var for radius, set it once to 0 and let components
  inherit, rather than 35 scattered edits — but catch the ones that hardcode a radius outside the
  token.
- This must not regress the archetype/design tests. Run the full e2e visual-adjacent specs after.

Browser test / assertion: a representative sample (a modal, an input, a button, a card, the
searchable select) all render square; add a lint-ish test or a DOM assertion that the key
components have 0 radius.

---

## T5 — Save-button discipline: stop auto-save, batch then Save (portal-wide principle) — `CF-FIX2-T5:`

**The finding (screenshot 2):** on custom lists, adding a value **auto-saves each one immediately**
— the operator wants to **add all values, then click Save once.** And wants this principle applied
everywhere: **avoid auto-save unless genuinely necessary; prefer explicit Save.**

- **Custom-list values:** change from add-auto-saves to a **staged** model: added values collect in
  the UI (unsaved), the user adds several, then **one Save** commits them all. Show unsaved state
  clearly (e.g. a dot/asterisk, a "3 unsaved values" hint, disabled-until-dirty Save). Cancel
  discards staged changes. Parent-child within the staged set must still work (a staged value can
  parent another staged value — resolve on save).
- **Audit for other auto-save spots** and convert the ones that should be explicit-save. Name each
  you find in Step 0; convert the clear ones. Do NOT convert places where auto-save is genuinely
  correct (e.g. dashboard drag-arrange persist-on-drop is arguably fine; toggles that are meant to
  be instant). Use judgement and list your calls for my confirmation.
- The Save-button pattern should feel consistent with the rest of the app's modals (which already
  use explicit Create/Save).

Browser test: add three list values without them persisting; the list shows them as unsaved; click
Save once; reload; all three present. Cancel before save discards.

---

## SEQUENCING
T4 (square corners) and T1 (prefix + alignment) are quick and independent — do first. T2
(searchable select everywhere) next. T5 (save discipline) next. T3 (shared fields) LAST and with
its own confirmed sub-Step-0, since it's the re-architecture. Order: **T4, T1, T2, T5, T3.**

## PER-TASK & FINAL
- Each task: Step-0 confirmed → build → gates green → atomic commit → browser test → completion note.
- **T3 additionally:** present its model/migration/transform decisions and WAIT for confirmation
  before writing its code, even after the overall Step 0 is approved.
- Final report: five-lens review, the T3 migration strategy explained, the full list of auto-save
  spots found and how each was handled (T5), the corner-radius audit summary (T4), and a
  line-by-line map of the operator's 5 findings → commits → browser proofs.
- Tag `v1.7-cf-fix-2`.

Do not begin coding until I confirm your Step 0 file plan — and hold T3's code until I separately
confirm its three decisions.

---

## APPENDIX — the operator's verbatim findings
1. Custom field Internal ID is misaligned with rubbish words floating; make the prefix contextual
   like NetSuite — user keys `customer`, system stores `custbody_customer` (body field),
   `custcol_customer` (line), `custlist_customer` (list). Apply everywhere.
2. The list-field style (searchable select) is done for the field-type picker but NOT applied to
   all other list-type fields in the portal — apply it everywhere.
3. Square-corner fields: some are rounded, some square — make ALL square to unify the design.
4. Custom fields should be their own thing, shared, applicable to multiple transactions — like
   NetSuite, create a custom body/line field then choose which records it applies to; currently we
   duplicate per transaction. When PR transforms to PO, a shared field's value should carry to the
   next transaction. (Currently must set per transaction and duplicate.)
5. Custom list: after creating the list details, adding values auto-saves each one — instead let me
   add all first then click Save. Apply this save-button principle elsewhere too; avoid auto-save
   unless really needed.

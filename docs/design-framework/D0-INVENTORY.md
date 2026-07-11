# D0 — Field & Pattern Inventory

**Slice:** D0 (Design Framework, slice 0 of D0–D7) · **Zero code changes** — this
document and CHARTER.md are the only artifacts.
**Method:** full sweep of `web/src` (96 TS/TSX files; 33 files containing form
elements; ~330 raw `<input>/<select>/<textarea>/<button>` instances), the 12
answer types in `lib/formTypes.ts` + the `AnswerInput` renderer in
`components/vendor/BidForm.tsx`, `lib/lookups.ts`, `lib/format.ts`,
`lib/prStatus.tsx`, `lib/rfqStatus.tsx`, `components/vendors/badges.tsx`, all of
`index.css`, and the three rival local `Field` components.
**Baseline at D0 (for the record; D1 re-records at its own start):** web vitest
**31 files / 112 tests, all green**. No API/crawl runs — D0 touched nothing.

**The three numbers:** **37** distinct field configurations found ·
**16** field primitives derived (plus the Button family and the badge/pill set) ·
**4** census rows unabsorbed (misfits, each with a recommendation).

---

## Task 1 — Field census

Rows are DISTINCT CONFIGURATIONS, collapsed across files. States key:
req = required marker, dis = disabled, ro = readOnly, err = error styling,
help = help/hint text, ph = placeholder, def = non-empty default, aria =
aria-label, pre/suf = prefix/suffix affordance.

### 1a. Text-like

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F1 | Labelled text input (via local `Field` helpers) | free text | ph; req (2 of 3 rivals); aria (2 of 3) | none — the plain case | PrForm, ManualVendorForm, OnboardingForm | ~25 |
| F2 | Labelled email input | email | req, ph, aria | `type=email` | OnboardingInvite | 1 |
| F3 | Bare table-cell / inline text input (no label chrome) | free text | ro + `.ro` class (locked PR lines), ph, aria | renders inside `<td>` or editor rows | PrForm lines (itemCode/description/uom), DeliveryScreens (lot), BidForm (alt item), QuestionEditor (options/columns/rows/field labels), OnboardingReview (clarify topic/request), OnboardingResubmit (responses) | ~14 |
| F4 | Numeric-masked text input | qty / money | ro, ph, aria | `value.replace(/[^0-9.]/g,'')` sanitisation; `.amt` right-align | PrForm lines (qty, est rate) | 2 |
| F5 | Search/filter input | free text | ph | client-side list filtering (name/code/category) | RfqBuilder, RfqList, Requisitions add-vendor, RfqGovernance AddVendorModal, VendorMaster, SwecPicker | 6 |
| F6 | Message input, Enter-to-send | message | ph | `onKeyDown` Enter submits when non-empty; paired send IconButton | ChatDock, Clarifications | 2 |
| F7 | ReadOnly link input, select-on-focus | url/text | ro | copy-affordance for onboarding magic links | OnboardingInvite, OnboardingQueue | 2 |
| F8 | Code input | code | ph, aria; ro when editing existing | value CODE stored; read side renders `.mono` | AdminCustomLists (value code, list code) | 2 |
| F9 | Chrome-less inline name input (`.secname`) | free text | ph | edits section header in place | QuestionEditor | 1/section |
| F10 | Editor config text inputs (question label, unit, filetypes, form name) | free text | ph (varies by type) | live-edits form template config | QuestionEditor, Forms | ~5 |

### 1b. Number

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F11 | Labelled plain number input | number | dis (bid closed); min=0 some | lead weeks, warranty months, WHT %, max entries, sort order | BidForm, InvoiceScreens, QuestionEditor, AdminCustomLists | 5 |
| F12 | Guarded quantity input (min/max clamp) | qty | min=0, max=business cap, dis, aria, def=cap | caps: remaining (ASN), shipped (GRN), billable (invoice), min(offered, required) (award), required (bid); preset to the cap | DeliveryScreens ×2, InvoiceScreens, AwardScreens, BidForm | 5 patterns, 1/line-cell |
| F13 | Money number input | money | min=0, dis, aria | `.amt` right-aligned; NO currency affordance, NO formatting; >2 % price variance flags exception (invoice) | BidForm (unit price), InvoiceScreens (unit price) | 2 |
| F14 | Score input 0–100, commit on blur | score | min=0, max=100, def, aria | `onBlur` mutation (not onChange); empty ⇒ no post; feeds weighted calc | EvalScreens | 1/criterion-vendor cell |
| F15 | Financial grid input | money (RM’000) | aria | rejects ≥1e12; drives live Altman Z; bare cell in 11×3 grid | OnboardingForm | 33 cells |
| F16 | AnswerInput number/money/percent | number/money/% | ph = unit from config | one renderer for three QTYPES; unit as placeholder, not suffix | BidForm AnswerInput (used by RFQ bids + onboarding packs) | 3 QTYPES |

### 1c. Date & time

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F17 | Labelled date input | date | err (amber border, RfqBuilder); req | native picker; wire format ISO `yyyy-MM-dd` (Slice H T4); display elsewhere via `fmtDay` → dd/MM/yyyy | PrForm (required by), RfqBuilder (open/close ×2), AnswerInput date | 4 |
| F18 | Datetime-local input with floor | instant | min = current close, dis at cap | extension flow; converts local↔ISO via helpers; deadline is an instant (UTC stored, +8 display) | RfqGovernance ExtendModal | 1 |

### 1d. Selects

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F19 | Labelled select, static options | enum | ph as empty option | inline literal options: registration type, currency (RfqBuilder), purpose, cellType, group, answer type, show/type/region filters | ManualVendorForm, RfqBuilder, QuestionEditor, Forms, VendorMaster, Requisitions, modals | ~14 |
| F20 | Labelled select from Custom List | code | ph | `useLookups().of(listCode)`; stores CODE, shows label | ManualVendorForm, OnboardingForm (currency, payment terms, bank) | 5 |
| F21 | Dependent select (ParentValueCode) | code | ph | `of(listCode, parentValueCode)`; cascade resets children; swaps to free-text input when parent has no children (`hasCities`) | ManualVendorForm, OnboardingForm (country→state→city), AdminCustomLists (parent value) | 8 |
| F22 | Reason-code select | code | req (submit gated on it) | active values of RFQ_DECLINE/RESCIND/EXTENSION_REASON lists | RfqGovernance ReasonModal + ExtendModal (used from RfqDetailHub, BidForm) | 2 |
| F23 | Yes/No select | boolean-ish | — | `—`/Yes/No options; stored as string | AnswerInput yesno | 1 QTYPE |
| F24 | Config-options select | enum | — | options from question `config.options` | AnswerInput list | 1 QTYPE |
| F25 | Optgroup select | enum | — | grouped options (General / Specific RFQ; internal/vendor personas) | ChatDock, Clarifications (topic, recipient), TopBar (persona, dev-only) | 5 |
| F26 | Select with computed option labels | enum | — | evaluator picker; “ ✓” suffix on completed evaluators | EvalScreens | 1 |

### 1e. Textarea

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F27 | Textarea rows 2–4 | long text | ph; some labelled, some bare | reason note, cancel reason, extension note, rejection reason, message compose, instruction text, long_text answer | PrForm, Requisitions, RfqGovernance ×2, OnboardingReview, ChatDock, QuestionEditor, AnswerInput | 8 |

### 1f. Boolean & choice

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F28 | Single checkbox toggle | boolean | — | active flag, allow-multiple-files, broadcast-to-bidders | AdminCustomLists, QuestionEditor, Clarifications | 3 |
| F29 | Checkbox group (multi-pick) | enum[] | aria (some) | roles, evaluators (tech/comm), question packs, `multi` answers (pipe-joined) | AdminUsers, RfqBuilder, OnboardingInvite, AnswerInput multi | 4 groups |
| F30 | Checkbox tree with search | taxonomy codes | — | recursive SWEC tree, depth indent, leaf-only codes, live filter, chip echo | SwecPicker (used by ManualVendorForm, OnboardingForm, VendorDetail, RfqBuilder) | 1 |
| F31 | Row-select checkbox + select-all (indeterminate) | selection | dis (non-groupable), indeterminate | bulk PR grouping; line-selection drawer | Requisitions | 2 tables |
| F32 | Checkbox-gated sub-input | boolean → qty | dis (pending approval), aria | checking reveals a guarded NumberField in the same cell (award grid); bid-line `bidding` toggle gates price/qty/alt-item | AwardScreens, BidForm | 2 |
| F33 | Radio pair | enum(2) | checked | envelope Single/Dual with card-styled labels | RfqBuilder | 1 |
| F34 | Segmented button toggle | enum(2–4) | on-class (btn-pri vs btn-out) | SWEC/Non-SWEC invite type; forms purpose filter | OnboardingInvite, Forms | 2 |

### 1g. Files & composites

| # | Element | Data type | States used | Special behaviour | Files using it | Count |
|---|---------|-----------|-------------|-------------------|----------------|-------|
| F35 | Attachment picker | file(s) | aria | hidden `input[type=file]` inside label-as-button; single/multi from config; `<id>::<name>` value pairs; chip list with ext badge + remove; upload via existing client | AnswerInput attachment, OnboardingForm document checklist | 2 |
| F36 | Table/matrix answer grid | grid of number/money/text | aria per cell | rows×columns from config, cellType drives input type, JSON-serialised value | AnswerInput TableAnswer | 1 QTYPE |
| F37 | Repeatable group | entry[] of typed fields | aria | up to `max` entries, per-field types (text/number/money/date), add/remove entry, JSON-serialised | AnswerInput GroupAnswer | 1 QTYPE |

### 1h. The three rival Field components — feature union

All three define a private `Field` (and two a private `Select`) doing the same
job with different feature sets. The two `Select`s are line-for-line twins.

| Feature | PrForm `Field` | ManualVendorForm `Field` | OnboardingForm `Field` |
|---|---|---|---|
| label | ✓ | ✓ | ✓ |
| placeholder | ✓ | ✓ | ✓ |
| `type` prop (date etc.) | ✓ | ✗ | ✗ |
| required marker (`.req` \*) | ✗ | ✓ | ✓ |
| aria-label | ✗ | ✓ | ✓ |
| help text | ✗ | ✗ | ✗ |
| error display | ✗ | ✗ | ✗ |
| disabled / readOnly | ✗ | ✗ | ✗ |
| prefix / suffix | ✗ | ✗ | ✗ |

**Union = label + placeholder + type + required + aria.** No rival supports
help, error, disabled, or readOnly — those states exist in the app only on RAW
inputs (disabled on BidForm when the RFQ closes; readOnly `.ro` on locked PR
lines and magic links; error styling as an amber `borderColor` in RfqBuilder and
a red left-border + “required” tag in BidForm). The D1 field chrome must carry
the union **plus** the four states the rivals never absorbed — every one is
traced to a census row above, so nothing speculative enters.

### 1i. The 12 answer types (`QTYPES`) — already one pipeline

`AnswerInput` renders all 12 from `(type, config, value, onChange)` and is
consumed by BOTH the RFQ bid form and the onboarding question packs. It is the
existence proof for charter rule 3: metadata-consuming rendering already works
in this codebase. FieldSpec (Task 4) is the generalisation of exactly this
contract — `EditItem`/`FormItemConfig` are its questionnaire-shaped ancestors.

| QTYPE | Renders as | Census row |
|---|---|---|
| short_text | text input | F1/F3 |
| long_text | textarea rows=3 | F27 |
| number / money / percent | number input, unit as placeholder | F16 |
| list | select from config options | F24 |
| multi | checkbox column, pipe-joined | F29 |
| yesno | —/Yes/No select | F23 |
| date | date input | F17 |
| attachment | upload button + chips | F35 |
| table | matrix grid | F36 |
| group | repeatable entries | F37 |

### 1j. Buttons

| Variant | Markup | Where | Count (approx) |
|---|---|---|---|
| Primary | `.btn.btn-pri` (+`.btn-sm`) | every screen’s main action | ~60 |
| Outline | `.btn.btn-out` (+`.btn-sm`) | cancel/secondary, row actions | ~45 |
| Ghost | `.btn.btn-ghost` (+`.btn-sm`) | tertiary/row actions; red via inline `color: var(--red)` | ~30 |
| Danger | `.btn-danger` (modifier on pri) | destructive confirms | ~5 |
| Link | `.lnk`, `.freset` | breadcrumb backs, inline actions, filter resets | ~15 |
| Icon-only | `.ic` (mv/del/exch), `.cibtn`, `.cd-ic` | editor row tools, chat send, dock chrome | ~15 |
| Segmented / toggle | conditional `btn-pri`/`btn-out`, `.viewtoggle`, `.da-toggle` (aria-pressed) | type toggles, table/board views, forecast | ~10 |
| One-off shapes | `.choice` (card buttons), `.qaddbtn` (+menu), `.step`/`.obstep`/`.vtab` (nav) | chooser, editor, steppers/tabs | — |

**Danger is expressed two rival ways:** the `.btn-danger` class AND inline
`style={{color:'var(--red)'}}` on ghost/outline buttons (~10 sites). D1’s
Button primitive must make `danger` a real variant and retire the inline form.

---

## Task 3 — Pattern census (captured for D2)

| Pattern | Distinct variants | Where | Count |
|---|---|---|---|
| Form section | `card > chead(h3) > cbody`; PrForm uses bare `card + h3`; `Section` re-implemented locally in 2 files | 25+ files | ~80 |
| Field row layout | `grid g2/g3/g4`, PrForm’s `frow`; `.field` wrapper div | ~12 files | ~50 |
| Modal | shared `Modal` + `ConfirmModal` (ui.tsx); bespoke `ReasonModal`/`ExtendModal`/`AddVendorModal` (RfqGovernance); hand-rolled markup in AdminUsers (`UserModal`) | 20+ files | ~35 |
| Drawer / dock | ChatDock (floating, draggable, persisted position); hand-rolled `rqscrim/rqdrawer` line-selection drawer | 2 files | 2 |
| Notice / toast | `Notice` (ribbon, 4 tones) — the ONLY banner mechanism; no toast/snackbar exists | 21+ files | ~80 |
| Empty state | shared `EmptyState`; ~8 hand-rolled `hint`/`td colSpan` bypasses; full-page `Placeholder` (App.tsx) | 13+ / 4+ files | ~35 |
| Loading | `Spinner` only; no skeletons | 21+ files | ~40 |
| Tables | 8 archetypes: simple read-only; row-actions column (`.rowactions`); status-badge columns; expandable row (nested table in `td colSpan`); bulk-select w/ indeterminate header; inline-edit cells (PR lines, ASN/GRN/invoice qty, award grid, fin grid, matrix answers); client-sorted (AdminCustomLists); totals-outside-table (statements). No sticky headers, no `tfoot`, no sortable-header component | 20+ files | ~43 tables |
| Tabs / steppers | `.vtab` vertical tabs (VendorDetail, 8 tabs); `.obstep` sidebar stepper with done/warn states (OnboardingForm); obstep-style list picker (AdminCustomLists); no horizontal tabs | 3 files | 3 |
| Status pills | dot `.badge b-{tone}` (6 tones) via 11 status→tone maps (PR, RFQ, invitation, PO, ASN, invoice, onboarding, vendor status/type, form purpose, bid progress, dashboard outcome); filled `.pill`; `.swchip`(+`.hit`), `.lockchip`, `.filepill`, `.health`, `.lst` line chips (6 modifiers), `.chat-badge` | 35+ files | ~29 direct + mapped |
| Tooltips | native `title=` only; no component; aria-label + title redundancy common | 8+ files | ~15 |
| Breadcrumb | `.crumb` link + chev + current — fully uniform, always 2 levels | 16 files | 16 |
| Page header | `.pagehead` h1+p+spacer+actions — fully uniform | 29+ files | ~29 |
| Key-value | `.kv`(k/v) and `.vkv`(hint/vv) rivals; `.mono` value variant | 4+ files | ~50 |
| Filter bar | `.filterbar2` faceted multi-select (`msel/mpop/mscrim`, facets derived from data, `.freset`); legacy `.filterbar` (VendorMaster); no date-range filtering | 4 files | 4–5 |
| Stat cards | `.card.stat` (lbl/num/sub) + `tone-*` variants; `Stat` re-implemented locally in 3 files | 6+ files | ~50 |
| Kanban boards | column-per-status card views | Requisitions, RfqList | 2 |
| Wizard rail | `.step` horizontal-ish rail in RfqBuilder (distinct from obstep) | 1 file | 1 |

**Rival-implementation notes for D2:** `Section`/`Stat`/`KV` are re-implemented
locally in multiple files; `filterbar` vs `filterbar2`; hand-rolled modals
bypassing `Modal`; hand-rolled empty states bypassing `EmptyState`; two danger-
button idioms. These are the D2 consolidation targets — recorded here, not fixed
in D1.

---

## Task 2 — Primitive catalogue (derived, not invented)

Every census row maps to exactly one primitive; every primitive absorbs at
least one row. Misfits follow.

| Primitive | FieldSpec surface it needs | Absorbs |
|---|---|---|
| **TextField** | key, label, dataType `text`/`email`, required, disabled, readOnly, help, placeholder, defaultValue, maxLength | F1, F2, F3 (bare chrome), F5 (search variant: leading icon), F7 (readOnly + copy-on-focus variant), F9 (bare), F10 |
| **TextAreaField** | as TextField + `rows` | F27 |
| **NumberField** | + validation.min/max, `unit` (suffix), `commitOnBlur` behaviour flag, integer/decimal | F4 (qty half), F11, F12, F14, F15 (bare chrome), F16 (number/percent) |
| **MoneyField** | + `currencyCode`, right-aligned, format-on-blur, never float in state | F4 (rate half), F13, F16 (money) |
| **DateField** | dataType `date` \| `dateTime`; ISO-8601 in (Slice H T4), dd/MM/yyyy display via `fmtDay`; `withTime` renders datetime-local + timezone label (instants); validation.min/max as ISO | F17, F18 |
| **SelectField** | `options` source: static (flat or grouped) \| customList code; placeholder as empty option | F19, F20, F22, F24, F25 (grouped options), F26 (labels computed by caller — plain data) |
| **DependentSelectField** | options source: customList + `parentField` key; cascade-reset; degrade-to-TextField when parent has no children | F21 |
| **MultiSelectField** | options source (static \| customList); pipe-joined string value (matches stored answer format) | F29 |
| **YesNoField** | none beyond base (fixed —/Yes/No) | F23 |
| **CodeField** | as TextField, mono rendering (JetBrains Mono per charter — see token findings), uppercase affordance | F8 (+ read-side `.mono` code display) |
| **CheckboxField** | boolean value, label-after-box layout | F28 |
| **SegmentedField** | static options (2–4), renders button group; radio semantics | F33, F34 |
| **AttachmentField** | config: multiple, filetypes; `<id>::<name>` pipe value; wraps existing upload/download client | F35 |
| **TableField** | config: columns, rows, cellType; JSON value | F36 |
| **RepeatingGroupField** | config: fields[{label,type}], max; JSON value | F37 |
| **FieldChrome** (shared shell, not user-facing) | label + required marker + help + error + two modes: `labelled` block and `bare` (cell/inline; aria-label replaces the visual label) | the union of the three rivals + the raw-input states (§1h); the bare mode is what lets F3/F12/F15 cells use the same pipeline |
| **Button** (family) | variant primary/outline/ghost/danger, size, icon, busy, as-link | §1j (retires inline red styling) |
| **Badge/Pill set** | `StatusBadge` (dot, 6 tones — generalises the 11 existing tone maps), `Pill` (filled), `Chip` (removable/selectable — swchip) | §Task 3 status pills row |

**The 12 QTYPES map onto these with zero remainder** (see §1i), which is the
strongest evidence the catalogue is right-sized: the questionnaire engine — the
most metadata-driven surface in the product — needs nothing the census didn’t
already demand. D5 custom fields and L4 custom records ride this same list.

### Misfits (fit no clean primitive)

| Row | What | Recommendation |
|---|---|---|
| F6 | Chat message input (Enter-to-send + send button) | Leave bespoke as a shared `MessageInput` in the comm surface. It is a composition (TextField-bare + IconButton + key handling), not a field; 2 identical usages should share one local component, but it does not enter the primitive catalogue. |
| F30 | SWEC checkbox tree picker | Leave bespoke, justified: a recursive taxonomy picker with search used through one modal (`SwecPicker`) everywhere it appears. Already single-sourced. Its inner search box becomes TextField at D2-window migration. |
| F31 | Bulk row-select + indeterminate select-all | Not a field — a TABLE archetype concern. Defer to D2 (List archetype). Recorded, not built in D1. |
| F32 | Checkbox-gated sub-input (award cell, bid-line gate) | A composition pattern: CheckboxField + NumberField-bare in one cell. Document as a sanctioned composition; no new primitive. |

(The TopBar persona switcher inside F25 is a dev-harness control; it may adopt
SelectField opportunistically but is not a migration target.)

---

## Task 4 — FieldSpec contract (PROPOSED — for operator sign-off)

Design stance: **FieldSpec is pure metadata.** Values stay out of the spec —
primitives take `(spec, value, onChange)` exactly as `AnswerInput` does today,
so controlled-input wiring, react-query state, and the string-serialised answer
pipeline all keep working. Built-in fields define their specs in code; D5 will
hydrate the same shape from the server.

```ts
// web/src/ui/fieldSpec.ts (D1) — the single field contract.

export type FieldDataType =
  | 'text' | 'longText' | 'email' | 'code'          // TextField / TextAreaField / CodeField
  | 'number' | 'money' | 'percent'                  // NumberField / MoneyField
  | 'date' | 'dateTime'                             // DateField (ISO in, dd/MM/yyyy out)
  | 'boolean' | 'yesNo'                             // CheckboxField / YesNoField
  | 'select' | 'multiSelect' | 'segmented'          // SelectField / MultiSelectField / SegmentedField
  | 'attachment' | 'table' | 'group'                // AttachmentField / TableField / RepeatingGroupField

export interface FieldOption { code: string; label: string }
export interface FieldOptionGroup { label: string; options: FieldOption[] }

export type FieldOptionsSource =
  | { kind: 'static'; options: FieldOption[] | FieldOptionGroup[] }
  | { kind: 'customList'; listCode: string; parentField?: string }
  //           ^ getCustomLists + ParentValueCode filtering (lib/lookups.ts) —
  //             parentField names the sibling FieldSpec key whose VALUE is the
  //             parentValueCode (country → state → city; parent value pickers)

export interface FieldValidation {
  min?: number | string       // number/money floor; ISO date floor (F12, F14, F18)
  max?: number | string       // business caps: remaining/shipped/billable/offered (F12), 100 (F14)
  maxLength?: number          // reserved-but-unused: no census row enforces one today
}

export interface FieldConfig {                // the questionnaire edge — EXISTING
  options?: string[]                          // FormItemConfig, imported as-is so
  unit?: string                               // stored form templates keep parsing
  columns?: string[]; rows?: string[]; cellType?: string
  fields?: { label: string; type: string }[]; max?: number
  filetypes?: string; multiple?: boolean
}

export interface FieldSpec {
  key: string                  // state/DTO key; stable identity (answerKey pattern)
  label: string                // every rival Field has one (F1)
  dataType: FieldDataType     // discriminates the primitive (12 QTYPES + app types)
  required?: boolean           // req markers (F1/F2), submit gates (F22, BidForm)
  disabled?: boolean           // RFQ-closed bid inputs, at-cap extension (F11–F13, F18)
  readOnly?: boolean           // locked PR lines `.ro`, magic links, existing codes (F3, F7, F8)
  help?: string                // hints exist app-wide as `.hint`; no rival Field carries
                               // one yet — chrome absorbs it so screens stop hand-placing
  placeholder?: string         // pervasive (F1, F3, F5, F16 unit-as-placeholder…)
  defaultValue?: string | number | boolean | null   // preset-to-cap qty (F12), today-ISO dates
  options?: FieldOptionsSource // selects/multi/segmented (F19–F26, F29, F33, F34)
  validation?: FieldValidation // guards above
  unit?: string                // suffix affordance: %, weeks, months, RM’000 (F11, F16)
  currencyCode?: string        // MoneyField; RFQ carries currency today (BidForm header)
  config?: FieldConfig         // table/group/attachment shape (F35–F37) — unchanged
  // ---- reserved for D5 (custom fields) / D7 (sourcing) — accepted, inert in D1 ----
  displayType?: 'normal' | 'disabled' | 'readOnly' | 'hidden' | (string & {})
  sourcing?: unknown           // D7: where a custom field’s value comes from
}
```

Member-by-member justification (each traced to census rows):

| Member | Census evidence |
|---|---|
| `key` | every form keys state by field name; `answerKey(packId, order)` proves stable keys matter |
| `label` | all three rivals; bare cells replace it with aria-label — the chrome’s `bare` mode derives `aria-label` from `label`, formalising today’s ad-hoc `aria-label={label}` |
| `dataType` | the 12 QTYPES discriminate on exactly this; app fields add email/code/dateTime/segmented — all censused (F2, F8, F18, F33/F34) |
| `required` | rival req markers; BidForm/OnboardingForm submit-gating on required answers |
| `disabled` | F11–F13 (`!isOpen`), F18 (atCap), F32 (pending) — raw inputs only today, rivals lack it |
| `readOnly` | F3 `.ro`, F7, F8 — again raw-input-only today |
| `help` | `.hint` spans hand-placed beside fields across the app; centralising is the union step |
| `placeholder` | ~20 rows use it |
| `defaultValue` | qty presets to cap (F12), score default (F14), `todayIso()` dates |
| `options` | static (F19), customList (F20, F22), dependent (F21), grouped (F25), config-derived (F24 — D1 adapts config.options into a static source) |
| `validation.min/max` | F12’s five business caps, F14’s 0–100, F18’s time floor |
| `unit` | F11 (weeks/months/%), F16 (unit-as-placeholder today — becomes a real suffix) |
| `currencyCode` | RFQ header currency displayed beside line pricing (BidForm) |
| `config` | F35–F37 parse this exact shape today; kept verbatim so stored templates keep working |
| `displayType` | RESERVED (D5/D7). Only normal/disabled/readOnly/hidden render in D1; census shows no other display variant in use |
| `sourcing` | RESERVED (D7). No census usage; typed as `unknown`, no-op with a code comment |

Explicitly **absent** (census found no need — charter rule 6):
`prefix` (no field renders a prefix today; currency shows as table-header/hint),
`pattern`/input-mask (PrForm’s regex strip becomes NumberField behaviour, not
spec), style/className overrides (forbidden by charter), per-field event hooks
beyond onChange (blur-commit is a NumberField behaviour flag, censused once).

**Chrome modes, not spec members:** whether a field renders labelled-block or
bare-in-cell is decided by the render site (a `chrome='labelled'|'bare'` prop on
the primitive), because the SAME logical field appears both ways (qty in a
table cell vs lead-time in a labelled block). Keeping it out of FieldSpec keeps
the spec reusable across contexts — D5 stores specs, screens choose chrome.

---

## Task 5 — Gap list (things the product LACKS — D1 must NOT build them)

Census-confirmed absences; each is a later-slice candidate, not a D1 item:

| Gap | Evidence | Where it belongs |
|---|---|---|
| Toast/snackbar (transient notifications) | Notice ribbons are the only banner; nothing self-dismisses | D4 (dashboard/notification surface) if ever — needs product decision |
| Tooltip component | native `title=` only | D2, only if archetypes demand it |
| Date-RANGE filter | filter bars facet on discrete values only | D3 (saved views) |
| Sortable-column table component / sticky headers | one client-sorted table, zero sortable headers | D2 (List archetype) |
| Skeleton loading | Spinner only | not planned; keep Spinner |
| Autocomplete/combobox (typeahead) | all searches are plain inputs + client filter | D3+; SelectField stays a native select in D1 |
| Horizontal stepper / in-modal wizard | all steppers are vertical rails | D2 |
| Form-level validation framework | per-screen ad-hoc gates (BidForm’s isAnswered, OnboardingForm’s stepMissing) | D2 (Transaction archetype owns submit-gating); D1 ships field-level error DISPLAY only |
| Rich text editing | zero usages | out of scope entirely |
| Payment voucher screens | placeholder by explicit instruction (CLAUDE.md) | unchanged |
| Currency-aware money FORMATTING in inputs | money inputs are raw `type=number` | this one D1 DOES build (MoneyField) — listed because the census proves it’s a gap today, per the D1 prompt |

## Token findings (feeds D1 Phase 1 — recorded here, fixed there)

1. **Three CSS variables are referenced but never defined:** `--amber-bg`,
   `--blue-bg`, `--red-bg` (used by `.pill.b-*` and `.ribbon-error/-warn` with
   fallbacks or not at all). D1 Phase 1 defines them.
2. **The pill/badge palette is largely hardcoded hex** (dot colours `#d2952f`
   `#2f9e6e` `#3f78c2` `#cf5149`, pill text/backgrounds, `.lst` ambers, swchip
   teals). Deliberate contrast choices — Phase 1 NAMES them as tokens without
   changing a value.
3. **Fonts: neither Inter nor JetBrains Mono is actually loaded.** `--sans`
   names Inter but there is no `@font-face`/link, so users get system fonts;
   `.mono` uses `ui-monospace`, and JetBrains Mono (charter) appears nowhere.
   **Operator decision needed at D1:** ship the two font files (self-hosted)
   or amend the charter to the de-facto system stacks. Flagged in CHARTER.md.
4. **Square corners hold** (`--radius: 0`) with a consistent intentional
   exception family: pills/chips (20px), health/chat badges (9px), ribbons
   (12px). CHARTER.md codifies the exception so D1 doesn’t “fix” it.
5. **~44 inline `style={{color:var(--…)}}` overrides**, mostly the ghost-red
   danger idiom — retired by the Button primitive, not by a restyle.
6. **11 status→tone maps** live in 10 files (lib/prStatus, lib/rfqStatus,
   vendors/badges + 8 screen-local `Record<string,string>` maps). Tones are
   used consistently; the MAPS are scattered. D1’s StatusBadge keeps the maps
   at the domain edge (they are business vocabulary) but one component renders
   them.

---

## Five-lens review

**Vendor/end-user.** The census shows vendors already live on the one shared
renderer (AnswerInput) — bids and onboarding feel alike. D1’s chrome brings
required/help/error to the places vendors actually fumble today (BidForm shows
“required” only after a failed submit; onboarding hides completeness until the
amber warning). Nothing in this inventory adds vendor-facing noise.

**Buyer/procurement (Faridah).** Her daily surfaces (Requisitions, RfqBuilder,
award grid) are the heaviest bare-input zones — 5 different qty-guard idioms
and 2 danger-button idioms. One NumberField with real min/max and one Button
family mean fewer wrong-qty submissions bounced by the API and faster muscle
memory across screens. The filter-bar and kanban patterns she relies on are
captured for D2, untouched by D1.

**Solution architect.** FieldSpec keeps the typed core intact: the only JSON in
the contract is the pre-existing questionnaire `config` at the configurable
edge, imported not invented. The `customList`/`parentField` options source
reuses `getCustomLists` + ParentValueCode — no new endpoint, and the L3/L4 seam
(D5 hydrating specs from the server) is exactly the reserved `displayType`/
`sourcing` members, typed but inert. No census row forced a KV escape hatch.

**Senior programmer.** The three rival Fields (and twin Selects) die into one
chrome + one primitive set; the union table (§1h) is the checklist that nothing
regresses. Misfits are named with dispositions instead of being silently
absorbed into a bloated base component. The one contract risk — labelled vs
bare rendering — is resolved as a render-site prop, keeping FieldSpec free of
presentation state. Tests-per-primitive requirements (render, states, a11y)
land in D1 with this list as the enumeration.

**CTO.** Marginal cost of the next module drops on two axes: every future
screen composes 16 primitives instead of hand-rolling inputs (the census says
~330 raw elements were hand-rolled to date), and D5 custom fields ship on the
same pipeline — the single most expensive future feature becomes a data change.
Handover risk: the inventory documents every one-off and every rival pattern in
one place; the grandfather-list mechanism (D1 Phase 4) turns migration into a
shrinking, auditable list rather than tribal knowledge. The font decision
(finding 3) is surfaced now, before D1 locks tokens, so it can’t become silent
brand drift.

---

**STOP per PROMPT-D0:** D1 does not begin until the FieldSpec above carries the
operator’s explicit sign-off. The TypedDates prerequisite is already satisfied
(`e4d5aca` on main).

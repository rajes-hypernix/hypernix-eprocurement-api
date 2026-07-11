# eProcure UI Primitives — the FieldSpec pipeline (D1)

Every field in the product renders from a `FieldSpec` object through one
pipeline (charter rule 3). Built-in fields define their specs in code; D5
custom fields will hydrate the same shape from the server. Derivation and
census evidence: `D0-INVENTORY.md`. Live catalogue: the dev-only
**/#design gallery route** (`web/src/ui/gallery/Gallery.tsx` — tree-shaken out
of production builds).

## The contract

`web/src/ui/fieldSpec.ts` — signed off at D0, implemented verbatim.

- **FieldSpec is pure metadata.** Primitives take `(spec, value, onChange)`;
  runtime state is passed at the render site: `error?: string` and
  `chrome?: 'labelled' | 'bare'` are props, never spec members.
- `chrome='labelled'` renders the `.field` block (label, required `*`, help,
  error); `chrome='bare'` renders the control alone with
  `aria-label={spec.label}` — the table-cell mode.
- Values are STRINGS in the stored formats the app already uses (pipe-joined
  multi/attachment, JSON table/group, ISO dates, raw numeric strings — never
  floats). `CheckboxField` alone takes a boolean.
- Reserved members: `displayType` renders normal/disabled/readOnly/hidden and
  no-ops otherwise (D5); `sourcing` is typed and inert (D7);
  `validation.maxLength` reserved.

## Catalogue (16 field primitives + Button family + badge set)

| Primitive | dataType | Notes |
|---|---|---|
| TextField | text, email | readOnly renders the `.ro` treatment |
| TextAreaField | longText | `rows` prop (2–4 in use) |
| CodeField | code | monospace via `--mono` (JetBrains Mono) |
| SelectField | select | options: static flat/grouped (optgroups) or customList; stores the CODE |
| DependentSelectField | select | customList + `parentField`; render site passes `parentValue`; degrades to free text when the parent has no children |
| MultiSelectField | multiSelect | checkbox column; pipe-joined value |
| YesNoField | yesNo | — / Yes / No |
| SegmentedField | segmented | radio-semantics button group (btn-pri/btn-out idiom) |
| CheckboxField | boolean | boolean value; label-after-box `.ck` idiom |
| NumberField | number, percent | `validation.min/max` = the business caps; `unit` suffix; `commitOnBlur`/`onCommit` render-site props (EvalScreens cells) |
| MoneyField | money | raw numeric string in state (live totals work); display formats on blur via `fmt()`; `currencyCode` affordance |
| DateField | date, dateTime | ISO in (Slice H T4), emits bare `yyyy-MM-dd`; readOnly shows dd/MM/yyyy via `fmtDay`; dateTime = instant (datetime-local + timezone affordance, `validation.min` floor) |
| AttachmentField | attachment | wraps existing uploadFile/fileUrl; `<id>::<name>` pipe value; config.multiple/filetypes |
| TableField | table | rows×columns from config; cellType drives input type; JSON value |
| RepeatingGroupField | group | entries to config.max; per-field types; JSON value |
| FieldChrome | (internal) | the one shell — do not hand-roll `.field` blocks in new code |
| Button / LinkButton | — | variant primary/outline/ghost/danger, `size='sm'`, `icon`, `busy`, `red` (replaces inline red styles) |
| StatusBadge / Pill / Chip | — | the six tones; domain status→tone maps stay at the domain edge |

## Usage rules

1. **New fields go through primitives.** Enforced:
   `web/src/test/no-raw-form-elements.test.ts` fails on any raw
   `<input>/<select>/<textarea>` in `web/src/components` beyond the
   grandfather list at `web/src/test/raw-form-elements.grandfather.json`
   (25 files / 121 elements at D1 close). The list SHRINKS at migrations —
   regenerate it in the migration commit — and never grows.
2. **No style overrides.** Primitives accept no className/style props; tokens
   (`web/src/tokens.css`, mirror `web/src/ui/tokens.ts`) are the only styling
   channel. Layout margins belong on wrapper elements at the render site.
3. **Migrate when touched.** Old markup coexists (charter: additive
   coexistence); a screen you're materially editing should adopt primitives
   for the parts you touch, then shrink the grandfather list.
4. **Sanctioned composition — checkbox-gated cell** (award grid, bid-line
   gate): `CheckboxField chrome='bare'` + conditional `NumberField
   chrome='bare'` in one `<td>`. A composition, not a primitive.
5. **Misfits stay bespoke, by written disposition** (D0 §Task 2): chat
   MessageInput (share one comm component when touched), SwecPicker tree
   (single-sourced already), bulk row-select (D2 List archetype).

## Proof gate

PrForm is the reference implementation (D1 Phase 3): header from a FieldSpec
array, bare-chrome line cells, zero behaviour-test edits, full e2e 42/42.
The other two rival Field components (ManualVendorForm, OnboardingForm) are
BACKLOG rows targeted at the D2 window.

import type { FormItemConfig } from '../lib/formTypes'

/**
 * FieldSpec — the single field contract (D0 §Task 4, operator-signed-off).
 *
 * PURE METADATA: values and change-handlers stay props on the primitives
 * (`spec, value, onChange`), exactly as AnswerInput works today, so specs are
 * reusable data — D5 custom fields will hydrate this same shape from the
 * server. Runtime state (error text, chrome mode) is passed at the render
 * site, never stored in the spec (operator ruling 1).
 */

export type FieldDataType =
  | 'text' | 'longText' | 'email' | 'code'          // TextField / TextAreaField / CodeField
  | 'number' | 'money' | 'percent'                  // NumberField / MoneyField
  | 'date' | 'dateTime'                             // DateField (ISO in, dd/MM/yyyy out)
  | 'boolean' | 'yesNo'                             // CheckboxField / YesNoField
  | 'select' | 'multiSelect' | 'segmented'          // SelectField / MultiSelectField / SegmentedField
  | 'attachment' | 'table' | 'group'                // AttachmentField / TableField / RepeatingGroupField

export interface FieldOption { code: string; label: string }
export interface FieldOptionGroup { label: string; options: FieldOption[] }

/**
 * Where a select-like field's options come from. `customList` rides the
 * existing getCustomLists client + ParentValueCode filtering (lib/lookups.ts);
 * `parentField` names the sibling FieldSpec key whose VALUE is the
 * parentValueCode (country → state → city; parent-value pickers).
 */
export type FieldOptionsSource =
  | { kind: 'static'; options: FieldOption[] | FieldOptionGroup[] }
  | { kind: 'customList'; listCode: string; parentField?: string }

export interface FieldValidation {
  /** number/money floor, or ISO date floor (qty guards F12, score F14, extension F18) */
  min?: number | string
  /** business caps: remaining / shipped / billable / offered qty; 100 for scores */
  max?: number | string
  /** RESERVED — no census row enforces a max length today */
  maxLength?: number
}

/** The questionnaire edge — the EXISTING FormItemConfig, one source of truth. */
export type FieldConfig = FormItemConfig

export interface FieldSpec {
  /** state/DTO key; stable identity (the answerKey pattern) */
  key: string
  label: string
  dataType: FieldDataType
  required?: boolean
  disabled?: boolean
  readOnly?: boolean
  help?: string
  placeholder?: string
  defaultValue?: string | number | boolean | null
  /** select / multiSelect / segmented */
  options?: FieldOptionsSource
  validation?: FieldValidation
  /** suffix affordance: %, weeks, months, RM’000 */
  unit?: string
  /** MoneyField currency affordance (e.g. the RFQ header currency) */
  currencyCode?: string
  /** table/group/attachment shape — parsed FormItemConfig, unchanged */
  config?: FieldConfig
  // ---- Reserved for D5 (custom fields) / D7 (sourcing) — accepted, inert in D1 ----
  /** Only normal/disabled/readOnly/hidden render today; anything else no-ops (D5/D7). */
  displayType?: 'normal' | 'disabled' | 'readOnly' | 'hidden' | (string & {})
  /** RESERVED (D7): where a custom field's value comes from. No-op in D1. */
  sourcing?: unknown
}

export const isOptionGroups = (
  o: FieldOption[] | FieldOptionGroup[],
): o is FieldOptionGroup[] => o.length > 0 && 'options' in o[0]

/** Chrome mode is the RENDER SITE's choice (operator ruling 1), not the spec's. */
export type ChromeMode = 'labelled' | 'bare'

/** The prop surface every field primitive shares. */
export interface FieldProps {
  spec: FieldSpec
  value: string
  onChange: (v: string) => void
  chrome?: ChromeMode
  /** Runtime validation message — chrome-level state, never in the spec. */
  error?: string
}

/**
 * Effective interaction state: the reserved displayType (D5) is honoured for
 * the four modes the census shows in use; unknown values no-op to normal (D7).
 */
export const effectiveMode = (
  spec: FieldSpec,
): 'normal' | 'disabled' | 'readOnly' | 'hidden' => {
  if (spec.displayType === 'hidden') return 'hidden'
  if (spec.displayType === 'disabled' || spec.disabled) return 'disabled'
  if (spec.displayType === 'readOnly' || spec.readOnly) return 'readOnly'
  return 'normal'
}

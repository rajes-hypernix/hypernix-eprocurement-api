import type { ResolvedFormFieldDto } from '../../api/client'
import type { FieldSpec, FieldDataType } from '../../ui/fieldSpec'
import type { TransactionSection } from '../../ui/archetypes/TransactionPage'

/**
 * Resolved entry-form definition → the TransactionPage layout (D7). One deterministic
 * projection: fields group by FieldGroup (order of first appearance), sorted fields chunk
 * 3-across into .frow rows, FullWidth fields render after the rows (memo-style) — exactly
 * how PrForm's hardcoded HEADER_SECTION laid out, so the seeded Standard form renders
 * byte-identical to the markup it replaced (the parity pin). Subtab fields become
 * TransactionPage tabs — pure layout containers holding FIELDS only (sublists keep their
 * built-in homes; custom sublists are the L4 boundary).
 */

const SPEC_TYPE: Record<string, FieldDataType> = {
  Code: 'text', Text: 'text', Enum: 'select', Date: 'date', Instant: 'date',
  Money: 'money', Number: 'number', Bool: 'yesNo', Tags: 'text',
}

const DISPLAY: Record<string, FieldSpec['displayType']> = {
  Normal: 'normal', Disabled: 'disabled', ReadOnly: 'readOnly', Hidden: 'hidden',
}

/** Registry FieldKey → local state/DTO key (PascalCase → camelCase; cf_/seg_ keys pass through). */
export const stateKey = (fieldKey: string): string =>
  fieldKey.startsWith('cf_') || fieldKey.startsWith('seg_')
    ? fieldKey
    : fieldKey[0].toLowerCase() + fieldKey.slice(1)

export const toSpec = (f: ResolvedFormFieldDto): FieldSpec => ({
  key: stateKey(f.fieldKey),
  label: f.label,
  // CFH-T3: a field carrying concrete options IS a select (a native dimension field backed
  // by a segment's values renders as the searchable picker, not free text).
  dataType: f.options && f.options.length > 0 ? 'select' : SPEC_TYPE[f.dataType] ?? 'text',
  required: f.requiredOnForm,
  placeholder: f.placeholder ?? undefined,
  displayType: DISPLAY[f.displayType] ?? 'normal',
  // CF-FIX2-T2: every list-backed field renders THE searchable select (operator ruling).
  ...(f.options && f.options.length > 0
    ? { searchable: true as const, options: { kind: 'static' as const, options: f.options.map((o) => ({ code: o.code, label: o.label })) } }
    : f.customListCode
      ? { searchable: true as const, options: { kind: 'customList' as const, listCode: f.customListCode, ...(f.sourceFieldKey ? { parentField: stateKey(f.sourceFieldKey) } : {}) } }
      : {}),
})

const chunk3 = (specs: FieldSpec[]): FieldSpec[][] => {
  const rows: FieldSpec[][] = []
  for (let i = 0; i < specs.length; i += 3) rows.push(specs.slice(i, i + 3))
  return rows
}

export function buildSections(fields: ResolvedFormFieldDto[]): TransactionSection[] {
  const groups = new Map<string, ResolvedFormFieldDto[]>()
  for (const f of [...fields].sort((a, b) => a.sort - b.sort)) {
    if (!groups.has(f.fieldGroup)) groups.set(f.fieldGroup, [])
    groups.get(f.fieldGroup)!.push(f)
  }
  return [...groups.entries()].map(([title, gf]) => ({
    title,
    rows: chunk3(gf.filter((f) => !f.fullWidth).map(toSpec)),
    fullWidth: gf.filter((f) => f.fullWidth).map(toSpec),
    columnBreak: gf.some((f) => f.groupColumnBreak),   // CF5-T3: this group starts the second column
  }))
}

/** Split the resolved fields by subtab: null → the main body, else one tab per name. */
export function splitSubtabs(fields: ResolvedFormFieldDto[]): { main: ResolvedFormFieldDto[]; tabs: [string, ResolvedFormFieldDto[]][] } {
  const main = fields.filter((f) => !f.subtab)
  const tabs = new Map<string, ResolvedFormFieldDto[]>()
  for (const f of fields.filter((f) => f.subtab)) {
    if (!tabs.has(f.subtab!)) tabs.set(f.subtab!, [])
    tabs.get(f.subtab!)!.push(f)
  }
  return { main, tabs: [...tabs.entries()] }
}

/** Native defaults for a NEW record (never applied to edits): stateKey → resolved value. */
export function nativeDefaults(fields: ResolvedFormFieldDto[]): Record<string, string> {
  return Object.fromEntries(
    fields
      .filter((f) => f.kind === 'Native' && f.defaultValue != null && f.defaultValue !== '')
      .map((f) => [stateKey(f.fieldKey), f.defaultValue!]),
  )
}

/** Client-side face of the submit gate: required NATIVE fields that are empty. The server
 * re-resolves and enforces the same rule (OD-D7-2) — this is UX, not the boundary. */
export function missingRequired(fields: ResolvedFormFieldDto[], values: Record<string, string>): string[] {
  return fields
    .filter((f) => f.requiredOnForm && f.kind === 'Native' && !(values[stateKey(f.fieldKey)] ?? '').trim())
    .map((f) => f.label)
}

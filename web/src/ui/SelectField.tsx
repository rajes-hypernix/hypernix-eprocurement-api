import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { useFieldOptions } from './useFieldOptions'
import { SearchSelectField } from './SearchSelectField'

/**
 * Single select (dataType 'select'). Options from a static list (flat or
 * grouped — the ChatDock/Clarifications topic pattern) or a Custom List code
 * (currency/payment terms/bank, reason codes). Stores the CODE, shows the
 * label. Absorbs census rows F19, F20, F22, F24, F25, F26.
 */
export function SelectField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  const { flat, groups } = useFieldOptions(spec)
  if (mode === 'hidden') return null
  // CF-FIX1-T7: searchable specs render THE standardized type-to-filter list — one
  // contract, one dispatch; render sites opt in with `searchable: true` on the spec.
  if (spec.searchable) return <SearchSelectField spec={spec} value={value} onChange={onChange} chrome={chrome} error={error} />
  const opt = (o: { code: string; label: string }) => <option key={o.code} value={o.code}>{o.label}</option>
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <select
        id={id}
        value={value}
        disabled={mode === 'disabled' || mode === 'readOnly'}
        required={spec.required}
        aria-label={spec.label}
        aria-invalid={error ? true : undefined}
        onChange={(e) => onChange(e.target.value)}
      >
        {spec.placeholder !== undefined && <option value="">{spec.placeholder}</option>}
        {groups
          ? groups.map((g) => <optgroup key={g.label} label={g.label}>{g.options.map(opt)}</optgroup>)
          : flat.map(opt)}
      </select>
    </FieldChrome>
  )
}

import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { useFieldOptions } from './useFieldOptions'
import { SearchSelectField } from './SearchSelectField'
import { useLookups } from '../lib/lookups'

/**
 * Dependent select (census F21): options are the Custom List values whose
 * ParentValueCode equals the PARENT field's current value (country → state →
 * city; parent-value pickers). The render site passes `parentValue` — specs
 * stay pure metadata. Degrades to a free-text input when the parent has no
 * children (the hasCities pattern in ManualVendorForm/OnboardingForm).
 */
export function DependentSelectField({ spec, value, onChange, chrome, error, parentValue }: FieldProps & { parentValue: string }) {
  const id = useId()
  const mode = effectiveMode(spec)
  const { flat } = useFieldOptions(spec, parentValue)
  const { hasChildren } = useLookups()

  // CF-FIX2-T2: searchable specs render THE standardized type-to-filter list — the
  // dependent filtering rides the same parentValue → useFieldOptions path.
  if (spec.searchable) return <SearchSelectField spec={spec} value={value} onChange={onChange} chrome={chrome} error={error} parentValue={parentValue} />
  if (mode === 'hidden') return null
  const listCode = spec.options?.kind === 'customList' ? spec.options.listCode : ''
  const degrade = !parentValue || !hasChildren(listCode, parentValue)
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      {degrade ? (
        <input
          id={id}
          type="text"
          value={value}
          placeholder={spec.placeholder}
          disabled={mode === 'disabled'}
          readOnly={mode === 'readOnly'}
          aria-label={spec.label}
          aria-invalid={error ? true : undefined}
          onChange={(e) => onChange(e.target.value)}
        />
      ) : (
        <select
          id={id}
          value={value}
          disabled={mode === 'disabled' || mode === 'readOnly'}
          required={spec.required}
          aria-label={spec.label}
          aria-invalid={error ? true : undefined}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">{spec.placeholder ?? `Select ${spec.label.toLowerCase()}`}</option>
          {flat.map((o) => <option key={o.code} value={o.code}>{o.label}</option>)}
        </select>
      )}
    </FieldChrome>
  )
}

import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/** Yes/No select (census F23, the `yesno` QTYPE): — / Yes / No, stored as string. */
export function YesNoField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
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
        <option value="">—</option>
        <option value="Yes">Yes</option>
        <option value="No">No</option>
      </select>
    </FieldChrome>
  )
}

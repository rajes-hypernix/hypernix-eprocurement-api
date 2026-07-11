import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/**
 * Free-text input (dataType 'text' | 'email'). Absorbs census rows F1/F2/F3
 * (bare mode)/F5/F7/F9/F10. ReadOnly renders the existing .ro treatment
 * (locked PR lines, magic links).
 */
export function TextField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const ro = mode === 'readOnly'
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <input
        id={id}
        type={spec.dataType === 'email' ? 'email' : 'text'}
        className={ro ? 'ro' : undefined}
        value={value}
        placeholder={spec.placeholder}
        disabled={mode === 'disabled'}
        readOnly={ro}
        required={spec.required}
        maxLength={spec.validation?.maxLength}
        aria-label={spec.label}
        aria-invalid={error ? true : undefined}
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldChrome>
  )
}

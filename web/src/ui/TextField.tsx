import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/**
 * Free-text input (dataType 'text' | 'email'). Absorbs census rows F1/F2/F3
 * (bare mode)/F5/F7/F9/F10. ReadOnly renders the existing .ro treatment
 * (locked PR lines, magic links). CF-FIX2-T1: spec.affix renders a fixed,
 * non-editable leading namespace INSIDE the field (custbody_…) — the user
 * types only the meaningful part.
 */
export function TextField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const ro = mode === 'readOnly'
  const input = (
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
  )
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      {spec.affix
        ? <span className="affix-wrap"><span className="affix mono" aria-hidden>{spec.affix}</span>{input}</span>
        : input}
    </FieldChrome>
  )
}

import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/**
 * Code input (dataType 'code') — monospace via the --mono token (JetBrains
 * Mono). Absorbs census row F8 (custom-list value codes; read side is .mono).
 * Codes are readOnly once persisted (AdminCustomLists edit modal).
 */
export function CodeField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const ro = mode === 'readOnly'
  const input = (
    <input
      id={id}
      type="text"
      className={`mono${ro ? ' ro' : ''}`}
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
      {/* CF-FIX2-T1: fixed namespace affix INSIDE the field (CUSTLIST_…) */}
      {spec.affix
        ? <span className="affix-wrap"><span className="affix mono" aria-hidden>{spec.affix}</span>{input}</span>
        : input}
    </FieldChrome>
  )
}

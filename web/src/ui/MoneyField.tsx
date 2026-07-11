import { useId, useState } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { fmt } from '../lib/format'

/**
 * Money input (dataType 'money'). Right-aligned, currency affordance from
 * spec.currencyCode, formats ON BLUR (display only — the VALUE stays the raw
 * numeric string, never a float, so live totals keep recomputing per
 * keystroke exactly as today; operator ruling: existing behaviour wins).
 * Absorbs census rows F4 (est rate), F13, F16 (money).
 */
export function MoneyField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const [focused, setFocused] = useState(false)
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const ro = mode === 'readOnly'
  const display = !focused && value !== '' && !isNaN(Number(value)) ? fmt(Number(value)) : value
  const input = (
    <input
      id={id}
      type="text"
      inputMode="decimal"
      className={`amt${ro ? ' ro' : ''}`}
      value={display}
      placeholder={spec.placeholder}
      disabled={mode === 'disabled'}
      readOnly={ro}
      required={spec.required}
      aria-label={spec.label}
      aria-invalid={error ? true : undefined}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      onChange={(e) => onChange(e.target.value.replace(/[^0-9.]/g, ''))}
    />
  )
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      {spec.currencyCode ? (
        <span className="fwrap">
          <span className="fsuf" aria-hidden="true">{spec.currencyCode}</span>
          {input}
        </span>
      ) : input}
    </FieldChrome>
  )
}

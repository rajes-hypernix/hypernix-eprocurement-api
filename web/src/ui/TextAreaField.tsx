import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/** Long text (dataType 'longText'). Absorbs census row F27 (rows 2–4 in use). */
export function TextAreaField({ spec, value, onChange, chrome, error, rows = 3 }: FieldProps & { rows?: number }) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <textarea
        id={id}
        rows={rows}
        value={value}
        placeholder={spec.placeholder}
        disabled={mode === 'disabled'}
        readOnly={mode === 'readOnly'}
        required={spec.required}
        aria-label={spec.label}
        aria-invalid={error ? true : undefined}
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldChrome>
  )
}

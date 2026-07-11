import { useId } from 'react'
import { effectiveMode, type ChromeMode, type FieldSpec } from './fieldSpec'

/**
 * Single boolean toggle (census F28: active flag, allow-multiple, broadcast).
 * Value is boolean — the one primitive whose state isn't a string, matching
 * how every censused site holds it. Label renders AFTER the box (the .ck
 * idiom), so this draws its own chrome rather than FieldChrome's block form.
 */
export function CheckboxField({ spec, value, onChange, chrome = 'labelled', error }: {
  spec: FieldSpec
  value: boolean
  onChange: (v: boolean) => void
  chrome?: ChromeMode
  error?: string
}) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const locked = mode === 'disabled' || mode === 'readOnly'
  const box = (
    <input
      id={id}
      type="checkbox"
      style={{ width: 'auto' }}
      checked={value}
      disabled={locked}
      aria-label={spec.label}
      aria-invalid={error ? true : undefined}
      onChange={(e) => onChange(e.target.checked)}
    />
  )
  if (chrome === 'bare') return box
  return (
    <div className={`field${error ? ' f-error' : ''}`}>
      <label className="ck" htmlFor={id}>
        {box}
        {spec.label} {spec.required && <span className="req">*</span>}
        {error && <span className="ferr"> {error}</span>}
      </label>
      {spec.help && <div className="hint">{spec.help}</div>}
    </div>
  )
}

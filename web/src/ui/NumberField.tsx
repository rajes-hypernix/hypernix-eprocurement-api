import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

/**
 * Numeric input (dataType 'number' | 'percent'). Absorbs census rows F4
 * (qty), F11, F12 (business-cap min/max guards), F14 (commitOnBlur — the
 * EvalScreens blur-commit cell; a render-site behaviour prop per ruling 1,
 * NOT a spec member), F15/F16. `unit` renders as a suffix affordance
 * (dataType 'percent' defaults it to %). Value stays a string in state.
 */
export function NumberField({ spec, value, onChange, chrome, error, commitOnBlur, onCommit }: FieldProps & {
  /** Fire onCommit with the final value on blur (EvalScreens scoring cells). */
  commitOnBlur?: boolean
  onCommit?: (v: string) => void
}) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const ro = mode === 'readOnly'
  const unit = spec.unit ?? (spec.dataType === 'percent' ? '%' : undefined)
  const input = (
    <input
      id={id}
      type="number"
      className={`amt${ro ? ' ro' : ''}`}
      value={value}
      min={spec.validation?.min as number | undefined}
      max={spec.validation?.max as number | undefined}
      placeholder={spec.placeholder}
      disabled={mode === 'disabled'}
      readOnly={ro}
      required={spec.required}
      aria-label={spec.label}
      aria-invalid={error ? true : undefined}
      onChange={(e) => onChange(e.target.value)}
      onBlur={commitOnBlur ? (e) => { if (e.target.value !== '') onCommit?.(e.target.value) } : undefined}
    />
  )
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      {unit ? (
        <span className="fwrap">
          {input}
          <span className="fsuf" aria-hidden="true">{unit}</span>
        </span>
      ) : input}
    </FieldChrome>
  )
}

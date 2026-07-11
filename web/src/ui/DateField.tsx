import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { fmtDay } from '../lib/format'

// datetime-local <-> ISO instant, as ExtendModal converts today.
const toLocalInput = (iso: string): string => {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}
const localInputToIso = (v: string): string => (v ? new Date(v).toISOString() : '')

/**
 * Date input (dataType 'date' | 'dateTime'). Consumes ISO-8601 (Slice H T4
 * DTOs: plain dates are bare yyyy-MM-dd). ReadOnly renders dd/MM/yyyy via the
 * existing fmtDay helper. 'dateTime' is an INSTANT: datetime-local editing
 * with the timezone label affordance (deadlines display Malaysia time).
 * Absorbs census rows F17, F18.
 */
export function DateField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const withTime = spec.dataType === 'dateTime'

  if (mode === 'readOnly') {
    return (
      <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
        <input id={id} type="text" className="ro" readOnly value={fmtDay(value)} aria-label={spec.label} />
      </FieldChrome>
    )
  }

  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      {withTime ? (
        <span className="fwrap">
          <input
            id={id}
            type="datetime-local"
            value={toLocalInput(value)}
            min={spec.validation?.min ? toLocalInput(String(spec.validation.min)) : undefined}
            disabled={mode === 'disabled'}
            required={spec.required}
            aria-label={spec.label}
            aria-invalid={error ? true : undefined}
            onChange={(e) => onChange(localInputToIso(e.target.value))}
          />
          <span className="fsuf" aria-hidden="true">local time</span>
        </span>
      ) : (
        <input
          id={id}
          type="date"
          value={(value ?? '').slice(0, 10)}
          min={spec.validation?.min ? String(spec.validation.min).slice(0, 10) : undefined}
          max={spec.validation?.max ? String(spec.validation.max).slice(0, 10) : undefined}
          disabled={mode === 'disabled'}
          required={spec.required}
          aria-label={spec.label}
          aria-invalid={error ? true : undefined}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </FieldChrome>
  )
}

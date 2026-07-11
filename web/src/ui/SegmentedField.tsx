import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { useFieldOptions } from './useFieldOptions'

/**
 * Small enum choice rendered as a button group (census F33 envelope radio +
 * F34 segmented toggles). Radio semantics: exactly one option active. Reuses
 * the existing conditional btn-pri/btn-out idiom from OnboardingInvite/Forms.
 */
export function SegmentedField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  const { flat } = useFieldOptions(spec)
  if (mode === 'hidden') return null
  const locked = mode === 'disabled' || mode === 'readOnly'
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <div id={id} role="radiogroup" aria-label={spec.label} aria-invalid={error ? true : undefined} style={{ display: 'flex', gap: 8 }}>
        {flat.map((o) => (
          <button
            key={o.code}
            type="button"
            role="radio"
            aria-checked={value === o.code}
            className={`btn btn-sm ${value === o.code ? 'btn-pri' : 'btn-out'}`}
            disabled={locked}
            onClick={() => onChange(o.code)}
          >
            {o.label}
          </button>
        ))}
      </div>
    </FieldChrome>
  )
}

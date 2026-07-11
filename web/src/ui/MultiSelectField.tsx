import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'
import { useFieldOptions } from './useFieldOptions'

/**
 * Multi-pick checkbox column (census F29: roles, evaluators, question packs,
 * `multi` answers). Value is the pipe-joined code string — the format the
 * answer pipeline already stores, so nothing re-serialises.
 */
export function MultiSelectField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  const { flat } = useFieldOptions(spec)
  if (mode === 'hidden') return null
  const selected = value ? value.split('|') : []
  const locked = mode === 'disabled' || mode === 'readOnly'
  const toggle = (code: string) =>
    onChange((selected.includes(code) ? selected.filter((x) => x !== code) : [...selected, code]).join('|'))
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <div className="ckcol" role="group" aria-label={spec.label} aria-invalid={error ? true : undefined} id={id}>
        {flat.map((o) => (
          <label className="ck" key={o.code}>
            <input type="checkbox" checked={selected.includes(o.code)} disabled={locked} onChange={() => toggle(o.code)} />
            {o.label}
          </label>
        ))}
      </div>
    </FieldChrome>
  )
}

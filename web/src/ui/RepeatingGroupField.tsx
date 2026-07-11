import { useId } from 'react'
import { Icon } from '../components/Icon'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

const safeParse = (v: string): string[][] => {
  try { const p = JSON.parse(v || '[]'); return Array.isArray(p) ? p : [] } catch { return [] }
}

/**
 * Repeatable group (census F37, the `group` QTYPE) — up to config.max entries,
 * each with the configured typed fields; JSON-serialised value. The
 * AnswerInput GroupAnswer renderer as a primitive.
 */
export function RepeatingGroupField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const locked = mode === 'disabled' || mode === 'readOnly'
  const fields = spec.config?.fields ?? []
  const max = spec.config?.max ?? 3
  const entries = safeParse(value)
  const commit = (next: string[][]) => onChange(JSON.stringify(next))
  const setField = (ei: number, fi: number, v: string) =>
    commit(entries.map((e, i) => (i === ei ? fields.map((_, f) => (f === fi ? v : (e[f] ?? ''))) : e)))
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <div id={id} style={{ display: 'flex', flexDirection: 'column', gap: 10 }} aria-invalid={error ? true : undefined}>
        {entries.map((entry, ei) => (
          <div key={ei} className="card" style={{ padding: 0 }}>
            <div className="chead" style={{ padding: '8px 12px' }}>
              <span className="hint" style={{ fontWeight: 700 }}>Entry {ei + 1}</span>
              <div className="spacer" />
              {!locked && (
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => commit(entries.filter((_, i) => i !== ei))} aria-label={`Remove entry ${ei + 1}`}>
                  <Icon name="x" size={13} />
                </button>
              )}
            </div>
            <div className="cbody" style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
              {fields.map((f, fi) => (
                <div className="field" key={fi} style={{ margin: 0 }}>
                  <label>{f.label}</label>
                  <input
                    type={f.type === 'number' || f.type === 'money' ? 'number' : f.type === 'date' ? 'date' : 'text'}
                    value={entry[fi] ?? ''}
                    disabled={locked}
                    aria-label={`${f.label} entry ${ei + 1}`}
                    onChange={(e) => setField(ei, fi, e.target.value)}
                  />
                </div>
              ))}
            </div>
          </div>
        ))}
        {!locked && entries.length < max && (
          <button type="button" className="btn btn-out btn-sm" style={{ alignSelf: 'flex-start' }} onClick={() => commit([...entries, fields.map(() => '')])}>
            <Icon name="plus" size={13} /> Add entry ({entries.length}/{max})
          </button>
        )}
      </div>
    </FieldChrome>
  )
}

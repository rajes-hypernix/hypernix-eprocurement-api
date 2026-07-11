import { useId } from 'react'
import { FieldChrome } from './FieldChrome'
import { effectiveMode, type FieldProps } from './fieldSpec'

const safeParse = (v: string): string[][] => {
  try { const p = JSON.parse(v || '[]'); return Array.isArray(p) ? p : [] } catch { return [] }
}

/**
 * Table/matrix answer grid (census F36, the `table` QTYPE) — rows × columns
 * from spec.config, cellType drives the input type, value JSON-serialised as
 * data[rowIndex][colIndex]. The AnswerInput TableAnswer renderer as a
 * primitive.
 */
export function TableField({ spec, value, onChange, chrome, error }: FieldProps) {
  const id = useId()
  const mode = effectiveMode(spec)
  if (mode === 'hidden') return null
  const locked = mode === 'disabled' || mode === 'readOnly'
  const cols = spec.config?.columns ?? []
  const rows = spec.config?.rows ?? []
  const data = safeParse(value)
  const cell = (ri: number, ci: number) => data[ri]?.[ci] ?? ''
  const setCell = (ri: number, ci: number, v: string) =>
    onChange(JSON.stringify(rows.map((_, r) => cols.map((_, c) => (r === ri && c === ci ? v : cell(r, c))))))
  const inputType = spec.config?.cellType === 'number' || spec.config?.cellType === 'money' ? 'number' : 'text'
  return (
    <FieldChrome spec={spec} chrome={chrome} error={error} htmlFor={id}>
      <div style={{ overflowX: 'auto' }} aria-invalid={error ? true : undefined}>
        <table id={id} aria-label={spec.label}>
          <thead><tr><th /> {cols.map((c, ci) => <th key={ci} className="amt">{c}</th>)}</tr></thead>
          <tbody>
            {rows.map((r, ri) => (
              <tr key={ri}>
                <td style={{ fontWeight: 600 }}>{r}</td>
                {cols.map((_, ci) => (
                  <td key={ci} className="amt">
                    <input type={inputType} value={cell(ri, ci)} disabled={locked} onChange={(e) => setCell(ri, ci, e.target.value)} aria-label={`${r} ${cols[ci]}`} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </FieldChrome>
  )
}

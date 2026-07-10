import { fileUrl } from '../api/client'
import { parseConfig } from './formTypes'

const safeParse = (v: string): string[][] => {
  try { const p = JSON.parse(v || '[]'); return Array.isArray(p) ? (p as string[][]) : [] } catch { return [] }
}

// Renders a stored bid answer for the evaluator/comparison tables. Attachments are
// "<fileId>::<name>" pairs (joined by "|") and become real download links. Matrix (table) and
// repeatable (group) answers are rendered as a grid/list — not the raw JSON they're stored as.
export function AnswerCell({ type, value, config }: { type?: string | null; value?: string | null; config?: string | null }) {
  const v = value ?? ''
  if (!v) return <span className="hint">—</span>

  if (type === 'attachment') {
    return (
      <>
        {v.split('|').filter(Boolean).map((entry, i) => {
          const [id, name] = entry.includes('::') ? entry.split('::') : ['', entry]
          return id
            ? <a key={i} href={fileUrl(id)} target="_blank" rel="noreferrer" style={{ display: 'block' }}>📎 {name}</a>
            : <span key={i} style={{ display: 'block' }}>📎 {name}</span>
        })}
      </>
    )
  }
  if (type === 'multi') return <>{v.split('|').filter(Boolean).join(', ')}</>

  if (type === 'table') {
    const cfg = parseConfig(config)
    const cols = cfg.columns ?? []
    const rows = cfg.rows ?? []
    const data = safeParse(v)
    const nCols = cols.length || Math.max(1, ...data.map((r) => r.length))
    return (
      <table className="ans-matrix">
        <thead><tr><th />{Array.from({ length: nCols }).map((_, c) => <th key={c} className="amt">{cols[c] ?? `Col ${c + 1}`}</th>)}</tr></thead>
        <tbody>
          {(rows.length ? rows : data.map((_, i) => `Row ${i + 1}`)).map((rl, ri) => (
            <tr key={ri}><td style={{ fontWeight: 600 }}>{rl}</td>{Array.from({ length: nCols }).map((_, ci) => <td key={ci} className="amt">{data[ri]?.[ci] || '—'}</td>)}</tr>
          ))}
        </tbody>
      </table>
    )
  }

  if (type === 'group') {
    const cfg = parseConfig(config)
    const fields = cfg.fields ?? []
    const entries = safeParse(v).filter((e) => e.some((x) => (x ?? '').trim()))
    if (entries.length === 0) return <span className="hint">—</span>
    return (
      <div className="ans-group">
        {entries.map((entry, i) => (
          <div className="ans-group-item" key={i}>
            {(fields.length ? fields.map((f) => f.label) : entry.map((_, j) => `Field ${j + 1}`)).map((lbl, j) => (
              entry[j] ? <div key={j}><span className="k">{lbl}:</span> {entry[j]}</div> : null
            ))}
          </div>
        ))}
      </div>
    )
  }

  return <>{v}</>
}

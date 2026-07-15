import { useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getNumberingSchemes, updateNumberingScheme, type NumberingSchemeDto } from '../../api/client'
import { Notice } from '../ui'
import { Button } from '../../ui/Button'
import { TextField } from '../../ui/TextField'
import { NumberField } from '../../ui/NumberField'
import { CheckboxField } from '../../ui/CheckboxField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { useNotify } from '../../ui/Notify'

/**
 * Numbering schemes (D7) — the Admin Setup surface (A70). Config over the gap-free
 * generator: the scheme shapes codes at MINT time only — existing documents keep their
 * codes forever, counters are never reset, and a prefix that returns re-attaches to its
 * preserved counter (no re-issue, ever).
 *
 * T6: the Entry Forms navigation shell — a LIST of schemes → Open → a dedicated FULL PAGE
 * editor (list hidden) with a Back control and an unsaved-changes guard.
 */

const spec = (key: string, label: string, dataType: FieldSpec['dataType']): FieldSpec => ({ key, label, dataType })

export function AdminNumbering() {
  const qc = useQueryClient()
  const { data: schemes = [] } = useQuery({ queryKey: ['numbering-schemes'], queryFn: getNumberingSchemes })
  const [editingType, setEditingType] = useState<string | null>(null)
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['numbering-schemes'] }) }
  const editing = schemes.find((s) => s.recordType === editingType)

  if (editing) {
    return <SchemeEditor scheme={editing} onBack={() => setEditingType(null)} onChanged={refresh} />
  }

  return (
    <div className="panel-fade">
      <div className="pagehead">
        <div><h1>Numbering</h1><p>Document code formats per record type — prefix, year segment, digits. History is never rewritten.</p></div>
        <div className="spacer" />
      </div>
      <table>
        <thead><tr><th>Record type</th><th>Prefix</th><th>Year segment</th><th>Digits</th><th>Next code</th><th /></tr></thead>
        <tbody>
          {schemes.map((s) => (
            <tr key={s.recordType}>
              <td style={{ fontWeight: 600 }}>{s.recordType}</td>
              <td className="mono">{s.prefix}</td>
              <td>{s.yearSegment ? 'Yes' : 'No'}</td>
              <td>{s.digits}</td>
              <td className="mono">{s.nextPreview}</td>
              <td className="amt">
                <div className="rowactions">
                  <Button variant="ghost" size="sm" onClick={() => setEditingType(s.recordType)} ariaLabel={`Open ${s.recordType}`}>Open</Button>
                </div>
              </td>
            </tr>
          ))}
          {schemes.length === 0 && <tr><td colSpan={6} className="hint">No numbering schemes yet.</td></tr>}
        </tbody>
      </table>
    </div>
  )
}

function SchemeEditor({ scheme, onBack, onChanged }: { scheme: NumberingSchemeDto; onBack: () => void; onChanged: () => void }) {
  const notify = useNotify()
  const [prefix, setPrefix] = useState(scheme.prefix)
  const [yearSegment, setYearSegment] = useState(scheme.yearSegment)
  const [digits, setDigits] = useState(String(scheme.digits))
  const [error, setError] = useState<string | null>(null)
  const dirty = useRef(false)
  const touch = () => { dirty.current = true }

  const save = useMutation({
    mutationFn: () => updateNumberingScheme(scheme.recordType, { prefix, yearSegment, digits: Number(digits) || 0 }),
    onSuccess: () => { dirty.current = false; notify('Numbering saved', { kind: 'toast' }); onChanged(); onBack() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the scheme.'),
  })

  const back = () => {
    if (!dirty.current || window.confirm('You have unsaved changes — all changes will be lost. Leave anyway?')) onBack()
  }

  return (
    <div className="panel-fade">
      <div className="chead" style={{ paddingLeft: 0 }}>
        <Button variant="ghost" size="sm" icon="back" onClick={back} ariaLabel="Back to numbering">Back</Button>
        <h3 style={{ marginLeft: 8 }}>{scheme.recordType} numbering</h3>
        <div className="spacer" />
        <Button variant="primary" size="sm" busy={save.isPending} disabled={!prefix.trim()} onClick={() => save.mutate()} ariaLabel="Save format">Save format</Button>
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="card" style={{ padding: 18, marginTop: 12, maxWidth: 560 }}>
        <TextField spec={spec('num-prefix', 'Prefix (A–Z, 0–9, dash)', 'text')} value={prefix} onChange={(v) => { touch(); setPrefix(String(v ?? '')) }} />
        <CheckboxField spec={spec('num-year', 'Year segment (PREFIX-YYYY-…)', 'boolean')} value={yearSegment} onChange={(v) => { touch(); setYearSegment(v === true) }} />
        <NumberField spec={{ ...spec('num-digits', 'Digits (3–6)', 'number'), validation: { min: 3, max: 6 } }} value={digits} onChange={(v) => { touch(); setDigits(String(v ?? '')) }} />
      </div>
      <p className="hint" style={{ marginTop: 10, maxWidth: 560 }}>
        The format applies to the NEXT document minted — existing codes are strings on
        records and never change. Counters are keyed by prefix (and year) and never
        reset: changing a prefix starts a fresh counter; changing it back re-attaches
        to the old one, so no code is ever issued twice. Without a year segment the
        counter runs continuously across years.
      </p>
    </div>
  )
}

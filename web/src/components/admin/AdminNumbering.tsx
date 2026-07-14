import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getNumberingSchemes, updateNumberingScheme, type NumberingSchemeDto } from '../../api/client'
import { SetupPage } from '../../ui/archetypes/SetupPage'
import { Modal, Notice } from '../ui'
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
 */

const spec = (key: string, label: string, dataType: FieldSpec['dataType']): FieldSpec => ({ key, label, dataType })

export function AdminNumbering() {
  const qc = useQueryClient()
  const { data: schemes = [] } = useQuery({ queryKey: ['numbering-schemes'], queryFn: getNumberingSchemes })
  const [selected, setSelected] = useState<string | null>(null)
  const [editing, setEditing] = useState<NumberingSchemeDto | null>(null)
  const scheme = schemes.find((s) => s.recordType === selected) ?? schemes[0]

  return (
    <SetupPage
      title="Numbering"
      subtitle="Document code formats per record type — prefix, year segment, digits. History is never rewritten."
      railItems={schemes.map((s) => ({ key: s.recordType, label: s.recordType, hint: s.nextPreview }))}
      selectedKey={scheme?.recordType ?? ''}
      onSelect={setSelected}
      detail={scheme && (
        <div>
          <div className="chead" style={{ paddingLeft: 0 }}>
            <h3>{scheme.recordType}</h3>
            <div className="spacer" />
            <Button variant="primary" size="sm" onClick={() => setEditing(scheme)} ariaLabel={`Edit ${scheme.recordType} numbering`}>Edit format</Button>
          </div>
          <table>
            <thead><tr><th>Prefix</th><th>Year segment</th><th>Digits</th><th>Next code</th></tr></thead>
            <tbody>
              <tr>
                <td className="mono">{scheme.prefix}</td>
                <td>{scheme.yearSegment ? 'Yes' : 'No'}</td>
                <td>{scheme.digits}</td>
                <td className="mono">{scheme.nextPreview}</td>
              </tr>
            </tbody>
          </table>
          <p className="hint" style={{ marginTop: 10 }}>
            The format applies to the NEXT document minted — existing codes are strings on
            records and never change. Counters are keyed by prefix (and year) and never
            reset: changing a prefix starts a fresh counter; changing it back re-attaches
            to the old one, so no code is ever issued twice. Without a year segment the
            counter runs continuously across years.
          </p>
        </div>
      )}
    >
      {editing && (
        <SchemeModal scheme={editing} onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void qc.invalidateQueries({ queryKey: ['numbering-schemes'] }) }} />
      )}
    </SetupPage>
  )
}

function SchemeModal({ scheme, onClose, onSaved }: { scheme: NumberingSchemeDto; onClose: () => void; onSaved: () => void }) {
  const [prefix, setPrefix] = useState(scheme.prefix)
  const [yearSegment, setYearSegment] = useState(scheme.yearSegment)
  const [digits, setDigits] = useState(String(scheme.digits))
  const [error, setError] = useState<string | null>(null)
  const notify = useNotify()

  const save = useMutation({
    mutationFn: () => updateNumberingScheme(scheme.recordType, { prefix, yearSegment, digits: Number(digits) || 0 }),
    onSuccess: () => { notify('Numbering saved', { kind: 'toast' }); onSaved() },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save the scheme.'),
  })

  return (
    <Modal
      title={`${scheme.recordType} numbering`}
      icon="edit"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose}>Cancel</button>
          <button type="button" className="btn btn-pri" disabled={!prefix.trim() || save.isPending} onClick={() => save.mutate()}>Save format</button>
        </>
      }
    >
      <TextField spec={spec('num-prefix', 'Prefix (A–Z, 0–9, dash)', 'text')} value={prefix} onChange={(v) => setPrefix(String(v ?? ''))} />
      <CheckboxField spec={spec('num-year', 'Year segment (PREFIX-YYYY-…)', 'boolean')} value={yearSegment} onChange={(v) => setYearSegment(v === true)} />
      <NumberField spec={{ ...spec('num-digits', 'Digits (3–6)', 'number'), validation: { min: 3, max: 6 } }} value={digits} onChange={(v) => setDigits(String(v ?? ''))} />
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

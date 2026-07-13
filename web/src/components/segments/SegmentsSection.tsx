import { useEffect, useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getSegmentAssignments, saveSegmentAssignments, type SegmentAssignmentDto } from '../../api/client'
import { useIdentity } from '../../identity'
import { renderField } from '../../ui/renderField'
import type { FieldSpec } from '../../ui/fieldSpec'
import { Button } from '../../ui/Button'
import { Notice } from '../ui'

/**
 * The "Segments" section (D6) — the D5 CustomFieldsSection pattern applied to dimensions.
 * Assignments arrive WITH the def metadata and options (one call), render through the
 * IDENTICAL D1 select pipeline, save via the folded A67 endpoint. With lineId set it is
 * the per-LINE picker (only line-level applications appear — the server filters).
 * Renders nothing when nothing applies; read-only without EditCustomValues. System
 * segments on PRs are server-refused ("edit the PR") — they never reach this section
 * as editable rows on other types.
 */

const toSpec = (a: SegmentAssignmentDto): FieldSpec => ({
  key: a.segmentCode,
  label: a.segmentName,
  dataType: 'select',
  searchable: true,   // CF-FIX4-T6: segment pickers use the standardized type-to-filter list
  required: a.required,
  options: { kind: 'static', options: a.options.map((o) => ({ code: o.code, label: o.label })) },
})

export function SegmentsSection({ recordType, recordId, lineId, title = 'Segments', excludeKeys = [] }: {
  recordType: string; recordId: string; lineId?: string; title?: string
  /** D7: keys the resolved entry form PLACED inline — the residual section owns the rest. */
  excludeKeys?: string[]
}) {
  const qc = useQueryClient()
  const { permissions } = useIdentity()
  const canEdit = permissions.includes('EditCustomValues')
  const { data: allAssignments } = useQuery({
    queryKey: ['segment-assignments', recordType, recordId, lineId ?? null],
    queryFn: () => getSegmentAssignments(recordType, recordId, lineId),
  })
  const assignments = useMemo(
    () => allAssignments?.filter((a) => !excludeKeys.includes(a.segmentCode)),
    [allAssignments, excludeKeys.join('|')],   // eslint-disable-line react-hooks/exhaustive-deps
  )
  const [draft, setDraft] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (assignments) setDraft(Object.fromEntries(assignments.map((a) => [a.segmentCode, a.valueCode ?? ''])))
  }, [assignments])

  const save = useMutation({
    mutationFn: () => saveSegmentAssignments(recordType, recordId,
      Object.fromEntries(Object.entries(draft).map(([k, v]) => [k, v === '' ? null : v])), lineId),
    onSuccess: () => { setError(null); void qc.invalidateQueries({ queryKey: ['segment-assignments', recordType, recordId] }) },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save segments.'),
  })

  if (!assignments || assignments.length === 0) return null   // nothing applied → no section

  const dirty = assignments.some((a) => (a.valueCode ?? '') !== (draft[a.segmentCode] ?? ''))
  return (
    <div className="card" style={{ padding: 14, marginTop: 14 }} aria-label={title}>
      <div className="chead">
        <h3>{title}</h3>
        <div className="spacer" />
        {canEdit && (
          <Button variant="primary" size="sm" disabled={!dirty || save.isPending} onClick={() => save.mutate()} ariaLabel={`Save ${title.toLowerCase()}`}>
            Save
          </Button>
        )}
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="grid g2">
        {assignments.map((a) =>
          renderField(
            toSpec(a),
            draft[a.segmentCode] ?? '',
            (next) => canEdit && setDraft((d) => ({ ...d, [a.segmentCode]: next })),
          ))}
      </div>
    </div>
  )
}

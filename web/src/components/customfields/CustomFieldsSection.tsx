import { useEffect, useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getCustomValues, saveCustomValues, type CustomValueDto } from '../../api/client'
import { useIdentity } from '../../identity'
import { renderField } from '../../ui/renderField'
import type { FieldSpec, FieldDataType } from '../../ui/fieldSpec'
import { Button } from '../../ui/Button'
import { Notice } from '../ui'

/**
 * The "Custom fields" section (D5) — drops onto any record surface. Defs arrive WITH the
 * values (one call), become a FieldSpec array and render through the IDENTICAL D1 pipeline
 * as every built-in field (charter rule 3 — no second path). Saves via the A67 endpoint;
 * Required is enforced at save (and only there — D7 owns lifecycle gating). Renders nothing
 * when the record type has no active defs; read-only (no Save) without EditCustomValues.
 */

const SPEC_TYPE: Record<string, FieldDataType> = {
  Text: 'text', LongText: 'longText', Int: 'number', Decimal: 'number',
  Money: 'money', Date: 'date', Bool: 'yesNo', ListValue: 'select',
}

const toSpec = (v: CustomValueDto): FieldSpec => ({
  key: v.code,
  label: v.label,
  dataType: SPEC_TYPE[v.dataType] ?? 'text',
  required: v.required,
  help: v.helpText || undefined,
  ...(v.dataType === 'ListValue' && v.customListCode
    ? { options: { kind: 'customList', listCode: v.customListCode } }
    : {}),
})

export function CustomFieldsSection({ recordType, recordId, excludeKeys = [] }: {
  recordType: string; recordId: string
  /** D7: keys the resolved entry form PLACED inline — the residual section owns the rest. */
  excludeKeys?: string[]
}) {
  const qc = useQueryClient()
  const { permissions } = useIdentity()
  const canEdit = permissions.includes('EditCustomValues')
  const { data: allValues } = useQuery({
    queryKey: ['custom-values', recordType, recordId],
    queryFn: () => getCustomValues(recordType, recordId),
  })
  const values = useMemo(
    () => allValues?.filter((v) => !excludeKeys.includes(v.code)),
    [allValues, excludeKeys.join('|')],   // eslint-disable-line react-hooks/exhaustive-deps
  )
  const [draft, setDraft] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (values) setDraft(Object.fromEntries(values.map((v) => [v.code, v.value ?? ''])))
  }, [values])

  const save = useMutation({
    mutationFn: () => saveCustomValues(recordType, recordId,
      Object.fromEntries(Object.entries(draft).map(([k, v]) => [k, v === '' ? null : v]))),
    onSuccess: () => { setError(null); void qc.invalidateQueries({ queryKey: ['custom-values', recordType, recordId] }) },
    onError: (e) => setError(e instanceof Error ? e.message : 'Could not save custom fields.'),
  })

  if (!values || values.length === 0) return null   // no active defs → no section, no noise

  const dirty = values.some((v) => (v.value ?? '') !== (draft[v.code] ?? ''))
  return (
    <div className="card" style={{ padding: 14, marginTop: 14 }} aria-label="Custom fields">
      <div className="chead">
        <h3>Custom fields</h3>
        <div className="spacer" />
        {canEdit && (
          <Button variant="primary" size="sm" disabled={!dirty || save.isPending} onClick={() => save.mutate()} ariaLabel="Save custom fields">
            Save
          </Button>
        )}
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="grid g2">
        {values.map((v) =>
          renderField(
            toSpec(v),
            draft[v.code] ?? '',
            (next) => canEdit && setDraft((d) => ({ ...d, [v.code]: next })),
          ))}
      </div>
    </div>
  )
}

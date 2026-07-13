import { useState } from 'react'
import { recordTypeOptions } from '../../lib/recordTypeLabel'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getViews, getViewFields, deleteView, type SavedViewDto } from '../../api/client'
import { ListPage, type ListColumn, type ListFacet } from '../../ui/archetypes/ListPage'
import { Button } from '../../ui/Button'
import { SelectField } from '../../ui/SelectField'
import { Modal } from '../ui'
import { ViewBuilder } from './SavedViewControls'
import { useIdentity } from '../../identity'

/**
 * Saved Views HOME (D7.5 task 1) — the builder's front door: every view the caller can
 * see (mine / shared / system) across all record types, in one place. Existing rules,
 * no new machinery: edit own, share inside the builder per A61, delete own; system
 * views are read-only. "New view" works for ANY record type from here — no list screen
 * involved (the operator's journey). Nav rides A59 UseSavedViews (ruled: an existing
 * D3 catalog action, nothing minted).
 */

const RECORD_TYPES = ['Requisition', 'Rfq', 'PurchaseOrder', 'Invoice', 'Asn', 'Vendor', 'Onboarding']

export function SavedViewsHome({ onOpenList }: {
  /** navigate to a record type's list screen with a view selected (the rollout routes) */
  onOpenList: (recordType: string, viewId: string) => void
}) {
  const qc = useQueryClient()
  const { code: me } = useIdentity()
  const { data: views = [] } = useQuery({ queryKey: ['views', 'all'], queryFn: () => getViews() })
  const [creating, setCreating] = useState(false)
  const [newType, setNewType] = useState('Requisition')
  const [builder, setBuilder] = useState<{ recordType: string; existing: SavedViewDto | null } | null>(null)
  const { data: newTypeFields = [] } = useQuery({
    queryKey: ['view-fields', builder?.recordType ?? newType],
    queryFn: () => getViewFields(builder?.recordType ?? newType),
    staleTime: Infinity,
  })
  const refresh = () => { void qc.invalidateQueries({ queryKey: ['views'] }) }
  const remove = useMutation({ mutationFn: (id: string) => deleteView(id), onSuccess: refresh })

  const kind = (v: SavedViewDto) => (v.isSystem ? 'System' : v.ownerUserId === me ? 'Mine' : 'Shared')

  const columns: ListColumn<SavedViewDto>[] = [
    { key: 'name', header: 'View', render: (v) => <span style={{ fontWeight: 700, color: 'var(--teal)' }}>{v.name}</span> },
    { key: 'type', header: 'Record type', render: (v) => v.recordType },
    { key: 'code', header: 'Code', render: (v) => <span className="mono">{v.code}</span> },
    { key: 'criteria', header: 'Criteria', amt: true, render: (v) => v.filters.length },
    {
      key: 'kind', header: 'Kind',
      render: (v) => (
        <span className={`badge ${v.isSystem ? 'b-blue' : v.ownerUserId === me ? 'b-teal' : 'b-grey'}`}>
          {kind(v)}{!v.isSystem && v.isShared ? ' · shared' : ''}
        </span>
      ),
    },
  ]

  const facets: ListFacet<SavedViewDto>[] = [
    { key: 'type', label: 'Record type', value: (v) => v.recordType },
    { key: 'kind', label: 'Kind', value: kind },
  ]

  const rowActions = (v: SavedViewDto) => (
    <>
      <Button variant="ghost" size="sm" onClick={() => onOpenList(v.recordType, v.id)} ariaLabel={`Open list with ${v.name}`}>Open</Button>
      {!v.isSystem && v.ownerUserId === me && (
        <>
          <Button variant="ghost" size="sm" icon="edit" onClick={() => setBuilder({ recordType: v.recordType, existing: v })} ariaLabel={`Edit ${v.name}`}>Edit</Button>
          <Button variant="ghost" size="sm" red onClick={() => remove.mutate(v.id)} ariaLabel={`Delete ${v.name}`}>Delete</Button>
        </>
      )}
    </>
  )

  return (
    <ListPage
      title="Saved Views"
      subtitle="Every view you can run — yours, shared, and system — across all record types."
      primaryAction={<Button variant="primary" size="sm" icon="plus" onClick={() => setCreating(true)}>New view</Button>}
      searchLabel="Search views"
      searchPlaceholder="Name or code…"
      searchAriaLabel="Search saved views"
      searchMatch={(v, q) => `${v.name} ${v.code}`.toLowerCase().includes(q.toLowerCase())}
      facets={facets}
      rows={views}
      rowKey={(v) => v.id}
      columns={columns}
      rowActions={rowActions}
      emptyNone="No views yet — create the first one."
      emptyFiltered="No views match."
    >
      {creating && (
        <Modal
          title="New saved view" icon="eye"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setCreating(false)}>Cancel</button>
              <button type="button" className="btn btn-pri" onClick={() => { setCreating(false); setBuilder({ recordType: newType, existing: null }) }}>
                Choose fields…
              </button>
            </>
          }
        >
          <SelectField
            spec={{
              key: 'nv-type', label: 'Record type', dataType: 'select',
              options: { kind: 'static', options: recordTypeOptions(RECORD_TYPES) },
            }}
            value={newType} onChange={(v) => setNewType(String(v ?? 'Requisition'))} />
        </Modal>
      )}
      {builder && (
        <ViewBuilder
          recordType={builder.recordType}
          existing={builder.existing}
          defaultColumns={newTypeFields.filter((f) => f.kind === 'Native').slice(0, 5).map((f) => f.fieldKey)}
          onClose={() => setBuilder(null)}
          onSaved={() => { setBuilder(null); refresh() }}
        />
      )}
    </ListPage>
  )
}

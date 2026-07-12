import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getViews, runView, type SavedViewDto, type ViewRunResult } from '../../api/client'
import { ViewPicker, ViewBuilder } from './SavedViewControls'
import { Pager } from './Pager'

/**
 * The D7.5 rollout adapter: everything a GRANDFATHERED list screen needs to consume
 * saved views without archetype migration — picker (default = the record type's system
 * view), paged run, builder modal, pager. The screen keeps its native table and maps
 * `run.rows` to its own item shape (the RfqList recipe); the archetype migration rows
 * in BACKLOG stand untouched.
 */
export function useViewRows(recordType: string, initialViewId?: string) {
  const { data: views = [] } = useQuery({ queryKey: ['views', recordType], queryFn: () => getViews(recordType) })
  const [picked, setPicked] = useState<string | null>(initialViewId ?? null)
  const [page, setPage] = useState(1)
  const [builder, setBuilder] = useState<{ existing: SavedViewDto | null } | null>(null)
  const systemView = views.find((v) => v.isSystem)
  const viewId = picked ?? systemView?.id ?? null
  const { data: run, refetch } = useQuery({
    queryKey: ['view-run', viewId, page],
    queryFn: () => runView(viewId!, page),
    enabled: viewId !== null,
  })

  const picker = (
    <ViewPicker
      views={views}
      selectedId={viewId}
      onSelect={(id) => { setPicked(id); setPage(1) }}
      onNew={() => setBuilder({ existing: null })}
      onEdit={(v) => setBuilder({ existing: v })}
    />
  )
  const builderModal = builder && (
    <ViewBuilder
      recordType={recordType}
      existing={builder.existing}
      defaultColumns={(systemView?.columns ?? []).map((c) => c.fieldKey)}
      onClose={() => setBuilder(null)}
      onSaved={(v) => { setBuilder(null); setPicked(v.id); setPage(1); void refetch() }}
    />
  )
  const pager = run ? <Pager page={run.page} size={run.size} total={run.total} onPage={setPage} /> : null

  return { views, viewId, run: run as ViewRunResult | undefined, picker, builderModal, pager }
}

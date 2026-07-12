import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getMyDashboard, personalizeDashboard, updateMyDashboard, resetMyDashboard,
  type PortletDto, type PortletUpsert,
} from '../api/client'
import { Spinner } from './ui'
import { Button } from '../ui/Button'
import { DashboardPage } from '../ui/archetypes/DashboardPage'
import { KpiMeterPortlet } from './portlets/KpiMeterPortlet'
import { KpiScorecardPortlet } from './portlets/KpiScorecardPortlet'
import { RemindersPortlet } from './portlets/RemindersPortlet'
import { SavedViewListPortlet } from './portlets/SavedViewListPortlet'
import { ShortcutsPortlet } from './portlets/ShortcutsPortlet'
import { RecentRecordsPortlet } from './portlets/RecentRecordsPortlet'
import { ChartPortlet } from './portlets/ChartPortlet'
import { MyInvitationsPortlet } from './portlets/MyInvitationsPortlet'
import { AddKpiModal } from './portlets/AddKpiModal'

/**
 * D4: the dashboard renderer. Reads the caller's resolved dashboard (personalized copy or
 * the role-default union) and renders each portlet LIVE through the DashboardPage archetype.
 * Personalize is copy-on-write; Arrange is arrow-based (ruled); Reset falls back to the
 * role default. Every number is computed or honestly absent — no mock imports here.
 */
const PORTLETS: Record<string, (p: PortletDto, nav: (k: string) => void) => React.ReactNode> = {
  KpiMeter: (p, nav) => <KpiMeterPortlet portlet={p} onNavigate={nav} />,
  KpiScorecard: (p, nav) => <KpiScorecardPortlet portlet={p} onNavigate={nav} />,
  Reminders: (p, nav) => <RemindersPortlet portlet={p} onNavigate={nav} />,
  SavedViewList: (p, nav) => <SavedViewListPortlet portlet={p} onNavigate={nav} />,
  Shortcuts: (p, nav) => <ShortcutsPortlet portlet={p} onNavigate={nav} />,
  RecentRecords: (p, nav) => <RecentRecordsPortlet portlet={p} onNavigate={nav} />,
  Chart: (p, nav) => <ChartPortlet portlet={p} onNavigate={nav} />,
  MyInvitations: (p, nav) => <MyInvitationsPortlet portlet={p} onNavigate={nav} />,
}

/** Re-pack a linear (row,col)-ordered list into rows: width-2 portlets own a row; width-1
 *  portlets pair up. Keeps arrange semantics deterministic for the arrow-based reorder. */
function pack(portlets: PortletUpsert[]): PortletUpsert[] {
  const packed: PortletUpsert[] = []
  let row = 0; let col = 0
  for (const p of portlets) {
    if (p.width >= 2) {
      if (col > 0) { row += 1; col = 0 }
      packed.push({ ...p, row, col: 0 })
      row += 1
    } else {
      packed.push({ ...p, row, col })
      if (col === 0) col = 1
      else { col = 0; row += 1 }
    }
  }
  return packed
}

const toUpsert = (p: PortletDto): PortletUpsert => ({
  id: p.id, portletType: p.portletType, title: p.title,
  col: p.col, row: p.row, width: p.width, savedViewId: p.savedViewId, configJson: p.configJson,
})

export function Dashboard({ onNavigate }: { onNavigate: (key: string) => void }) {
  const qc = useQueryClient()
  const { data: dash, isPending } = useQuery({ queryKey: ['my-dashboard'], queryFn: getMyDashboard })
  const [arrange, setArrange] = useState(false)
  const [draft, setDraft] = useState<PortletUpsert[] | null>(null)
  const [addKpi, setAddKpi] = useState(false)
  const invalidate = () => { void qc.invalidateQueries({ queryKey: ['my-dashboard'] }) }

  const save = useMutation({
    mutationFn: (portlets: PortletUpsert[]) => updateMyDashboard({ name: null, portlets: pack(portlets) }),
    onSuccess: () => { setArrange(false); setDraft(null); invalidate() },
  })
  const personalize = useMutation({ mutationFn: personalizeDashboard, onSuccess: invalidate })
  const reset = useMutation({
    mutationFn: resetMyDashboard,
    onSuccess: () => { setArrange(false); setDraft(null); invalidate() },
  })
  const addKpiPortlet = useMutation({
    mutationFn: async (p: PortletUpsert) => {
      const current = dash!.isPersonalized ? dash! : await personalizeDashboard()
      return updateMyDashboard({ name: null, portlets: pack([...current.portlets.map(toUpsert), p]) })
    },
    onSuccess: () => { setAddKpi(false); invalidate() },
  })

  if (isPending || !dash) return <Spinner />

  const working: PortletUpsert[] = draft ?? [...dash.portlets].sort((a, b) => a.row - b.row || a.col - b.col).map(toUpsert)
  const move = (p: PortletDto, dir: -1 | 1) => {
    const list = [...working]
    const i = list.findIndex((x) => x.id === p.id)
    const j = i + dir
    if (i < 0 || j < 0 || j >= list.length) return
    ;[list[i], list[j]] = [list[j], list[i]]
    setDraft(pack(list))
  }
  const toggleWidth = (p: PortletDto) =>
    setDraft(pack(working.map((x) => (x.id === p.id ? { ...x, width: x.width >= 2 ? 1 : 2 } : x))))
  const remove = (p: PortletDto) => setDraft(pack(working.filter((x) => x.id !== p.id)))

  const shown: PortletDto[] = arrange
    ? working.map((p, i) => ({ ...p, id: p.id ?? `draft-${i}`, savedViewId: p.savedViewId ?? null } as PortletDto))
    : dash.portlets

  return (
    <>
      <DashboardPage
        title={dash.name}
        subtitle={dash.isPersonalized ? 'Your personalized dashboard.' : 'Your role’s standard dashboard.'}
        toolbar={
          <>
            <Button variant="ghost" size="sm" icon="plus" onClick={() => setAddKpi(true)} ariaLabel="Add KPI">Add KPI</Button>
            {dash.isPersonalized ? (
              <>
                <Button variant={arrange ? 'primary' : 'outline'} size="sm" icon="grip"
                  onClick={() => (arrange ? save.mutate(working) : setArrange(true))} ariaLabel="Arrange portlets">
                  {arrange ? 'Done' : 'Arrange'}
                </Button>
                <Button variant="ghost" size="sm" red onClick={() => reset.mutate()} ariaLabel="Reset to role default">Reset</Button>
              </>
            ) : (
              <Button variant="outline" size="sm" icon="edit" onClick={() => personalize.mutate()} ariaLabel="Personalize dashboard">Personalize</Button>
            )}
          </>
        }
        portlets={shown}
        renderPortlet={(p) => PORTLETS[p.portletType]?.(p, onNavigate) ?? <span className="hint">Unknown portlet type.</span>}
        arrangeMode={arrange}
        onMoveUp={(p) => move(p, -1)}
        onMoveDown={(p) => move(p, 1)}
        onToggleWidth={toggleWidth}
        onRemove={remove}
      />
      {addKpi && <AddKpiModal onClose={() => setAddKpi(false)} onAdd={(p) => addKpiPortlet.mutate(p)} />}
    </>
  )
}

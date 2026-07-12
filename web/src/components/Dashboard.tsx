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
import { AddReminderModal } from './portlets/AddReminderModal'
import { AddPortletModal } from './portlets/AddPortletModal'
import { parseConfig, type RemindersConfig, type ReminderItem } from './portlets/portletConfig'

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
  const [addReminder, setAddReminder] = useState(false)
  const [addPortlet, setAddPortlet] = useState(false)
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
    onSuccess: () => { setAddKpi(false); setAddPortlet(false); invalidate() },
  })
  // CF3-T10: a portlet edits its OWN config (tile authoring) — persisted like any arrange.
  const updatePortletConfig = useMutation({
    mutationFn: async (v: { portletId: string; configJson: string }) => {
      const current = dash!.isPersonalized ? dash! : await personalizeDashboard()
      return updateMyDashboard({
        name: null,
        portlets: pack(current.portlets.map(toUpsert).map((x) => (x.id === v.portletId ? { ...x, configJson: v.configJson } : x))),
      })
    },
    onSuccess: invalidate,
  })
  // D7.5 task 5: append a reminder item — into the EXISTING Reminders portlet when one
  // is on the dashboard, else a new Reminders portlet carries it. Same copy-on-write.
  const addReminderItem = useMutation({
    mutationFn: async (item: ReminderItem) => {
      const current = dash!.isPersonalized ? dash! : await personalizeDashboard()
      const ups = current.portlets.map(toUpsert)
      const existing = ups.find((p) => p.portletType === 'Reminders')
      if (existing) {
        const cfg = parseConfig<RemindersConfig>(existing.configJson, { items: [] })
        existing.configJson = JSON.stringify({ items: [...cfg.items, item] })
        return updateMyDashboard({ name: null, portlets: pack(ups) })
      }
      return updateMyDashboard({
        name: null,
        portlets: pack([...ups, {
          id: null, portletType: 'Reminders', title: 'Reminders', col: 0, row: 99, width: 1,
          savedViewId: null, configJson: JSON.stringify({ items: [item] }),
        }]),
      })
    },
    onSuccess: () => { setAddReminder(false); invalidate() },
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
  // CF3-T7: drag-drop = swap the two portlets' positions in the working order.
  const dropSwap = (from: PortletDto, to: PortletDto) => {
    const list = [...working]
    const i = list.findIndex((x) => x.id === from.id)
    const j = list.findIndex((x) => x.id === to.id)
    if (i < 0 || j < 0) return
    ;[list[i], list[j]] = [list[j], list[i]]
    setDraft(pack(list))
  }

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
            <Button variant="ghost" size="sm" icon="plus" onClick={() => setAddPortlet(true)} ariaLabel="Add portlet">Add portlet</Button>
            <Button variant="ghost" size="sm" icon="plus" onClick={() => setAddKpi(true)} ariaLabel="Add KPI">Add KPI</Button>
            <Button variant="ghost" size="sm" icon="clock" onClick={() => setAddReminder(true)} ariaLabel="Add reminder">Add reminder</Button>
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
        renderPortlet={(p) =>
          p.portletType === 'Shortcuts' && dash.isPersonalized ? (
            <ShortcutsPortlet portlet={p} onNavigate={onNavigate}
              onUpdateConfig={(portletId, configJson) => updatePortletConfig.mutate({ portletId, configJson })} />
          ) : (PORTLETS[p.portletType]?.(p, onNavigate) ?? <span className="hint">Unknown portlet type.</span>)}
        arrangeMode={arrange}
        onMoveUp={(p) => move(p, -1)}
        onMoveDown={(p) => move(p, 1)}
        onToggleWidth={toggleWidth}
        onRemove={remove}
        onDropSwap={dropSwap}
      />
      {addPortlet && (
        <AddPortletModal onClose={() => setAddPortlet(false)} onAdd={(p) => addKpiPortlet.mutate(p)}
          onKpi={() => setAddKpi(true)} onReminder={() => setAddReminder(true)} />
      )}
      {addKpi && <AddKpiModal onClose={() => setAddKpi(false)} onAdd={(p) => addKpiPortlet.mutate(p)} onGoCreateViews={() => onNavigate('views')} />}
      {addReminder && <AddReminderModal onClose={() => setAddReminder(false)} onAdd={(i) => addReminderItem.mutate(i)} onGoCreateViews={() => onNavigate('views')} />}
    </>
  )
}

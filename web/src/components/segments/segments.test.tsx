import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from '../../test/utils'
import { SegmentsSection } from './SegmentsSection'
import { AddKpiModal } from '../portlets/AddKpiModal'
import { KpiMeterPortlet } from '../portlets/KpiMeterPortlet'
import * as client from '../../api/client'

const identity = (permissions: string[]) => {
  vi.spyOn(client, 'getPersonas').mockResolvedValue([
    { code: 'u_faridah', name: 'Faridah', kind: 'internal', roles: ['Buyer'] } as never,
  ])
  vi.spyOn(client, 'getPermissions').mockResolvedValue(permissions)
}

const val = (id: string, code: string, label: string): client.SegmentValueDto =>
  ({ id, code, label, parentValueId: null, active: true, sort: 0 })

beforeEach(() => {
  vi.restoreAllMocks()
  window.history.replaceState(null, '', '?as=u_faridah')
})

describe('SegmentsSection — dimensions ride the D1 select pipeline', () => {
  it('renders applied segments as selects and saves via the assignment endpoint (lineId threaded)', async () => {
    identity(['ReadCustomValues', 'EditCustomValues'])
    vi.spyOn(client, 'getSegmentAssignments').mockResolvedValue([
      {
        segmentCode: 'seg_project', segmentName: 'Project', required: false,
        options: [val('1', 'ALPHA-PLANT', 'Alpha Plant'), val('2', 'BETA-PLANT', 'Beta Plant')],
        valueCode: null, valueLabel: null,
      },
    ])
    const save = vi.spyOn(client, 'saveSegmentAssignments').mockResolvedValue([])

    renderWithProviders(<SegmentsSection recordType="PurchaseOrder" recordId="p1" lineId="L1" title="Line segments" />)
    const select = await screen.findByLabelText('Project')
    await userEvent.selectOptions(select, 'ALPHA-PLANT')
    await userEvent.click(screen.getByRole('button', { name: 'Save line segments' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith('PurchaseOrder', 'p1',
      { seg_project: 'ALPHA-PLANT' }, 'L1'))
  })

  it('is read-only without EditCustomValues and absent when nothing applies', async () => {
    identity(['ReadCustomValues'])
    vi.spyOn(client, 'getSegmentAssignments').mockResolvedValue([
      { segmentCode: 'seg_x', segmentName: 'X', required: false, options: [val('1', 'A', 'A')], valueCode: 'A', valueLabel: 'A' },
    ])
    const { unmount } = renderWithProviders(<SegmentsSection recordType="Vendor" recordId="v1" />)
    await screen.findByLabelText('X')
    expect(screen.queryByRole('button', { name: 'Save segments' })).not.toBeInTheDocument()
    unmount()

    vi.spyOn(client, 'getSegmentAssignments').mockResolvedValue([])
    renderWithProviders(<><SegmentsSection recordType="Vendor" recordId="v1" /><span>page</span></>)
    await screen.findByText('page')
    expect(screen.queryByText('Segments')).not.toBeInTheDocument()
  })
})

describe('Sliced KPI — the D6 group-by surfaced on the dashboard', () => {
  it('AddKpi offers "Slice by segment" from the registry palette (Segment kind only)', async () => {
    identity(['UseSavedViews', 'ManageOwnSavedViews', 'UseDashboards', 'ManageOwnDashboard'])
    vi.spyOn(client, 'getViews').mockResolvedValue([
      { id: 'v1', code: 'VIEW-1', name: 'All POs', recordType: 'PurchaseOrder', ownerUserId: 'u_faridah', isSystem: false, isShared: false, filters: [], columns: [] },
    ])
    vi.spyOn(client, 'getViewFields').mockResolvedValue([
      { fieldKey: 'Total', label: 'Value', dataType: 'Money', kind: 'Native', options: null },
      { fieldKey: 'seg_project', label: 'Project', dataType: 'Enum', kind: 'Segment', options: ['ALPHA-PLANT'] },
    ])
    const added: client.PortletUpsert[] = []
    renderWithProviders(<AddKpiModal onClose={() => {}} onAdd={(p) => added.push(p)} />)
    await userEvent.selectOptions(await screen.findByLabelText('Record type'), 'PurchaseOrder')
    const slice = await screen.findByLabelText('Slice by segment (optional)')
    expect(Array.from(slice.querySelectorAll('option')).map((o) => o.getAttribute('value')))
      .not.toContain('Total')                                   // only Segment-kind fields slice
    await userEvent.selectOptions(slice, 'seg_project')
    await userEvent.type(screen.getByLabelText('KPI title'), 'PO value by project')
    await userEvent.selectOptions(screen.getByLabelText('Saved view'), 'v1')
    await userEvent.click(screen.getByRole('button', { name: 'Add KPI' }))
    expect(JSON.parse(added[0]!.configJson).groupBy).toBe('seg_project')
  })

  it('KpiMeter renders one row per slice INCLUDING the named Unassigned bucket', async () => {
    identity(['UseSavedViews', 'UseDashboards'])
    vi.spyOn(client, 'aggregateView').mockResolvedValue({
      viewId: 'v1', fn: 'sum', fieldKey: 'Total', value: 1750, excludedNullCount: 0,
      groupedBy: 'seg_project',
      groups: [
        { key: 'ALPHA-PLANT', label: 'Alpha Plant', value: 1000 },
        { key: 'BETA-PLANT', label: 'Beta Plant', value: 500 },
        { key: '__unassigned', label: 'Unassigned', value: 250 },
      ],
    })
    renderWithProviders(
      <KpiMeterPortlet
        portlet={{
          id: 'p1', portletType: 'KpiMeter', title: 'PO value by project', col: 0, row: 0, width: 1,
          savedViewId: 'v1', configJson: JSON.stringify({ fn: 'sum', fieldKey: 'Total', groupBy: 'seg_project' }),
        }}
        onNavigate={() => {}}
      />,
    )
    const slices = await screen.findByTestId('kpi-slices')
    expect(slices).toHaveTextContent('Alpha Plant')
    expect(slices).toHaveTextContent('Unassigned')              // honest-null: the record is surfaced
    expect(slices).toHaveTextContent('RM 250.00')
  })
})

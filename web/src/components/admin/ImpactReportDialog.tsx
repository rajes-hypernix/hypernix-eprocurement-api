import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { ImpactReportDto } from '../../api/client'
import { Modal, Notice, Spinner } from '../ui'
import { Button } from '../../ui/Button'
import { recordTypeLabel } from '../../lib/recordTypeLabel'

/**
 * CF-FIX3-T3/T4: the shared three-tier confirm dialog. Before any destructive verb the
 * admin SEES the impact report — config references grouped by consumer (mandatory
 * placements red-flagged) and stored values split Live vs Historical per record type —
 * and is offered ONLY the tiers the report allows:
 *   Deactivate  — always (Tier 1, reversible)
 *   Delete      — zero references AND zero values, live or historical (Tier 2)
 *   Purge       — references clear, zero LIVE, historical remain (Tier 3, A73-governed:
 *                 removes closed-record history with a per-value audit snapshot)
 * The server re-checks every verb — this dialog explains, it does not enforce.
 */
export function ImpactReportDialog({ title, kind, load, onDeactivate, onDelete, onPurge, onClose }: {
  title: string
  kind: 'field' | 'value' | 'segment' | 'segment-value'
  load: () => Promise<ImpactReportDto>
  onDeactivate?: () => Promise<unknown>
  onDelete: () => Promise<unknown>
  onPurge: () => Promise<unknown>
  onClose: (changed: boolean) => void
}) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const [confirmPurge, setConfirmPurge] = useState(false)
  const { data: report, isPending } = useQuery({ queryKey: ['impact-report', title], queryFn: load, gcTime: 0 })

  const run = (verb: string, fn: () => Promise<unknown>) => {
    setError(null); setBusy(verb)
    fn().then(() => onClose(true)).catch((e: unknown) => {
      setBusy(null); setError(e instanceof Error ? e.message : `Could not ${verb.toLowerCase()}.`)
    })
  }

  const noun = { field: 'field', value: 'list value', segment: 'segment', 'segment-value': 'segment value' }[kind]
  return (
    <Modal title={title} icon="flag"
      footer={
        <>
          <Button variant="outline" onClick={() => onClose(false)}>Cancel</Button>
          {onDeactivate && (
            <Button variant="outline" busy={busy === 'Deactivate'} onClick={() => run('Deactivate', onDeactivate)}
              ariaLabel="Deactivate instead">Deactivate instead</Button>
          )}
          {report?.canPurge && !confirmPurge && (
            <Button variant="outline" red onClick={() => setConfirmPurge(true)} ariaLabel="Purge history">Purge history…</Button>
          )}
          {report?.canPurge && confirmPurge && (
            <Button variant="primary" red busy={busy === 'Purge'} onClick={() => run('Purge', onPurge)}
              ariaLabel="Confirm purge">Purge {report.historicalCount} historical value{report.historicalCount === 1 ? '' : 's'} and delete</Button>
          )}
          <Button variant="primary" red disabled={!report?.canDelete} busy={busy === 'Delete'}
            onClick={() => run('Delete', onDelete)} ariaLabel={`Delete ${noun}`}>Delete</Button>
        </>
      }
    >
      {isPending && <Spinner label="Checking references…" />}
      {report && (
        <div data-testid="impact-report">
          {report.configReferences.length > 0 && (
            <>
              <div className="hint" style={{ fontWeight: 700, marginBottom: 4 }}>Where it is used</div>
              <table style={{ marginBottom: 12 }}>
                <tbody>
                  {report.configReferences.map((r, i) => (
                    <tr key={i}>
                      <td style={{ fontWeight: 600 }}>{r.consumerName}</td>
                      <td>{r.targetLabel}</td>
                      <td className="hint">
                        {r.detail}
                        {r.isMandatory && <span className="badge b-red" style={{ marginLeft: 6 }}>mandatory</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
          {(report.liveCount > 0 || report.historicalCount > 0) && (
            <>
              <div className="hint" style={{ fontWeight: 700, marginBottom: 4 }}>Stored values</div>
              <table style={{ marginBottom: 12 }}>
                <thead><tr><th>Store</th><th>Record type</th><th className="amt">Live</th><th className="amt">Historical</th></tr></thead>
                <tbody>
                  {report.data.filter((d) => d.liveCount > 0 || d.historicalCount > 0).flatMap((d) =>
                    d.byRecordType.map((c) => (
                      <tr key={`${d.storeName}-${c.recordType}`}>
                        <td className="hint">{d.storeName}</td>
                        <td>{recordTypeLabel(c.recordType)}</td>
                        <td className="amt">{c.live}</td>
                        <td className="amt">{c.historical}</td>
                      </tr>
                    )))}
                </tbody>
              </table>
            </>
          )}
          {report.canDelete && (
            <p className="hint">Nothing references this {noun} and no values are stored — it can be deleted safely.</p>
          )}
          {!report.canDelete && report.blockedReason && (
            <Notice tone={report.canPurge ? 'info' : 'error'}>{report.blockedReason}</Notice>
          )}
          {report.canPurge && (
            <p className="hint">
              All {report.historicalCount} remaining value{report.historicalCount === 1 ? ' is' : 's are'} on closed records.
              Purge removes them permanently — each removed value is snapshotted to the audit trail first. Requires the
              purge permission (admin).
            </p>
          )}
        </div>
      )}
      {error && <Notice tone="error">{error}</Notice>}
    </Modal>
  )
}

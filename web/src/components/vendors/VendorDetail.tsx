import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getVendor,
  getVendorAudit,
  setVendorCategories,
  toggleVendorStatus,
} from '../../api/client'
import { useSwec } from '../../api/swec'
import { Icon } from '../Icon'
import { Spinner } from '../ui'
import { fmt, dateMY, initials } from '../../lib/format'
import { StatusBadge, TypeBadge } from './badges'
import { SwecPicker } from './SwecPicker'

const TABS: [string, string][] = [
  ['overview', 'Overview'],
  ['categories', 'Categories'],
  ['contacts', 'Contacts'],
  ['addresses', 'Addresses'],
  ['banking', 'Banking & Currencies'],
  ['compliance', 'Compliance'],
  ['performance', 'Performance'],
  ['audit', 'Audit'],
]

function Stat({ label, value, sub }: { label: string; value: React.ReactNode; sub?: string }) {
  return (
    <div className="card stat">
      <div className="lbl">{label}</div>
      <div className="num">{value}</div>
      {sub && <div className="sub">{sub}</div>}
    </div>
  )
}

function Kv({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div className="vkv">
      <span className="hint">{k}</span>
      <span className="vv">{v}</span>
    </div>
  )
}

export function VendorDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const qc = useQueryClient()
  const { data: swec } = useSwec()
  const [tab, setTab] = useState('overview')
  const [picking, setPicking] = useState(false)

  const { data: v, isPending } = useQuery({ queryKey: ['vendor', id], queryFn: () => getVendor(id) })
  const { data: audit = [] } = useQuery({
    queryKey: ['vendor-audit', id],
    queryFn: () => getVendorAudit(id),
    enabled: tab === 'audit',
  })

  const saveCats = useMutation({
    mutationFn: (codes: string[]) => setVendorCategories(id, { categories: codes }),
    onSuccess: () => {
      setPicking(false)
      void qc.invalidateQueries({ queryKey: ['vendor', id] })
      void qc.invalidateQueries({ queryKey: ['vendors'] })
    },
  })
  const toggle = useMutation({
    mutationFn: () => toggleVendorStatus(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['vendor', id] })
      void qc.invalidateQueries({ queryKey: ['vendors'] })
    },
  })

  if (isPending || !v) return <Spinner label="Loading vendor…" />
  const perf = v.performance!
  // Derived metrics (Slice H T7): null = "not yet available" (never a fabricated number).
  const naPct = (n: number | null | undefined) => (n == null ? 'not yet available' : `${n}%`)
  const naDays = (n: number | null | undefined) => (n == null ? 'not yet available' : `${n} days`)
  const cats = v.categories ?? []

  return (
    <>
      <div className="crumb">
        <a onClick={onBack}>Vendor Master</a> <Icon name="chev" size={13} />{' '}
        <span>{v.registeredName}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            {v.registeredName} <TypeBadge type={v.type} />
          </h1>
          <p style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {v.code} · {v.region} · {v.state} · <StatusBadge status={v.status} />
          </p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-out btn-sm" onClick={() => setPicking(true)}>
          <Icon name="edit" size={14} /> Categories
        </button>
        <button type="button" className="btn btn-out btn-sm" onClick={() => toggle.mutate()}>
          {v.status === 'Inactive' ? 'Reactivate' : 'Deactivate'}
        </button>
      </div>

      <div className="vtabs">
        {TABS.map(([k, l]) => (
          <button key={k} type="button" className={`vtab${tab === k ? ' on' : ''}`} onClick={() => setTab(k)}>
            {l}
          </button>
        ))}
      </div>

      {tab === 'overview' && (
        <>
          <div className="grid g4" style={{ marginBottom: 16 }}>
            <Stat label="On-time delivery" value={naPct(perf.otd)} sub="rolling 12 mo" />
            <Stat label="Quality acceptance" value={naPct(perf.quality)} sub="goods accepted" />
            <Stat label="Win rate" value={naPct(perf.winRate)} sub="RFQs won" />
            <Stat label="Spend YTD" value={`RM ${fmt(perf.spendYtd)}`} sub={`${perf.pos} POs`} />
          </div>
          <div className="grid g2">
            <div className="card">
              <div className="chead"><h3>Identity</h3></div>
              <div className="cbody">
                <Kv k="Registered name" v={v.registeredName} />
                <Kv k="Vendor code" v={v.code} />
                <Kv k="SSM / reg. no." v={v.registrationNo} />
                <Kv k="Tax ID (SST)" v={v.taxId} />
                <Kv k="Country" v={v.country} />
                <Kv k="Region / state" v={`${v.region} · ${v.state}`} />
                <Kv k="City" v={v.city} />
              </div>
            </div>
            <div className="card">
              <div className="chead"><h3>Registration &amp; Terms</h3></div>
              <div className="cbody">
                <Kv k="Type" v={v.type === 'SWEC' ? 'PETRONAS SWEC' : 'Non-SWEC'} />
                <Kv k="LLRC tier" v={v.llrcTier ?? '—'} />
                <Kv k="Status" v={<StatusBadge status={v.status} />} />
                <Kv k="Payment terms" v={v.paymentTerms} />
                <Kv k="Credit limit" v={`RM ${fmt(v.creditLimit)}`} />
                <Kv k="Created" v={dateMY(v.createdUtc)} />
                <Kv k="Last updated" v={dateMY(v.updatedUtc)} />
              </div>
            </div>
          </div>
        </>
      )}

      {tab === 'categories' && (
        <div className="card">
          <div className="chead">
            <h3>SWEC Categories</h3>
            <div className="spacer" />
            <button type="button" className="btn btn-out btn-sm" onClick={() => setPicking(true)}>
              <Icon name="edit" size={14} /> Edit categories
            </button>
          </div>
          <div className="cbody">
            {cats.length === 0 && <p className="hint">No categories tagged yet.</p>}
            {cats.map((c) => (
              <div key={c} className="swrow">
                <span className="swpath">{swec?.path(c) ?? c}</span>
                <span className="swcode">{c}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {tab === 'contacts' && (
        <div className="card">
          <div className="chead">
            <h3>Contacts</h3>
            <div className="spacer" />
            <span className="hint">{v.contacts!.length} contact(s)</span>
          </div>
          <div className="cbody">
            {v.contacts!.map((c, i) => (
              <div className="ctc" key={i}>
                <div className="ctc-av">{initials(c.name)}</div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 700 }}>
                    {c.name} {c.isPrimary && <span className="badge b-teal">Primary</span>}
                  </div>
                  <div className="hint">{c.role}</div>
                </div>
                <div style={{ textAlign: 'right', fontSize: 12.5 }}>
                  <div>{c.email}</div>
                  <div className="hint">{c.phone}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {tab === 'addresses' && (
        <div className="card">
          <div className="chead">
            <h3>Addresses</h3>
            <div className="spacer" />
            <span className="hint">{v.addresses!.length} address(es)</span>
          </div>
          <div className="cbody grid g2">
            {v.addresses!.map((a, i) => (
              <div className="addr" key={i}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                  <span className="pill b-grey">{a.type}</span>
                  {a.isPrimary && <span className="badge b-teal">Primary</span>}
                </div>
                <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                  {a.line}
                  <br />
                  {a.city}, {a.postcode}
                  <br />
                  {a.state}, {a.country}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {tab === 'banking' && (
        <>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="chead"><h3>Transacting Currencies</h3></div>
            <div className="cbody">
              {v.currencies!.map((c) => (
                <span key={c.code} className="swchip">
                  {c.code}
                  {c.isPrimary ? ' · primary' : ''}
                </span>
              ))}
            </div>
          </div>
          <div className="card">
            <div className="chead">
              <h3>Bank Accounts</h3>
              <div className="spacer" />
              <span className="hint">for electronic payment</span>
            </div>
            <table>
              <thead>
                <tr>
                  <th>Bank</th>
                  <th>Account no.</th>
                  <th>SWIFT</th>
                  <th>Currency</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {v.bankAccounts!.map((b, i) => (
                  <tr key={i}>
                    <td style={{ fontWeight: 600 }}>{b.bank}</td>
                    <td>{b.accountNo}</td>
                    <td>{b.swift}</td>
                    <td>{b.currency}</td>
                    <td>{b.isPrimary && <span className="badge b-teal">Primary</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {tab === 'compliance' && (
        <div className="card">
          <div className="chead"><h3>Certifications &amp; Compliance</h3></div>
          <table>
            <thead>
              <tr>
                <th>Certificate / document</th>
                <th>Reference</th>
                <th>Valid to</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {v.certifications!.map((c, i) => (
                <tr key={i}>
                  <td style={{ fontWeight: 600 }}>{c.name}</td>
                  <td>{c.number}</td>
                  <td>{c.validTo}</td>
                  <td><span className="badge b-green">{c.status}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'performance' && (
        <>
          <div className="grid g4" style={{ marginBottom: 16 }}>
            <Stat label="Compliance breaches" value={perf.breaches ?? 'not yet available'} sub="rolling 12 mo" />
            <Stat label="Avg lead time" value={naDays(perf.lead)} sub="order to receipt" />
            <Stat label="Response rate" value={naPct(perf.response)} sub="RFQ invites" />
            <Stat label="Rating" value={`★ ${v.rating}`} sub="overall" />
          </div>
          <div className="card">
            <div className="chead"><h3>Performance KPIs</h3></div>
            <div className="cbody">
              {[
                ['On-time delivery', perf.otd],
                ['Quality acceptance', perf.quality],
                ['RFQ response rate', perf.response],
                ['Win rate', perf.winRate],
              ].map(([label, pct]) => (
                <div key={label as string} style={{ marginBottom: 10 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 4 }}>
                    <span className="hint">{label}</span>
                    <span style={{ fontWeight: 700 }}>{pct == null ? 'not yet available' : `${pct}%`}</span>
                  </div>
                  <div className="pbar">
                    <i style={{ width: `${pct ?? 0}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}

      {tab === 'audit' && (
        <div className="card">
          <div className="chead">
            <h3>Change History</h3>
            <div className="spacer" />
            <span className="hint">audit trail</span>
          </div>
          <div className="cbody">
            {audit.length === 0 && <p className="hint">No audit entries.</p>}
            {audit.map((a, i) => (
              <div className="ctc" key={i}>
                <span className="cav">{initials(a.actorName)}</span>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 600 }}>{a.action}</div>
                  <div className="hint">
                    {a.actorName}
                    {a.after ? ` · ${a.after}` : ''}
                  </div>
                </div>
                <span className="hint">{dateMY(a.utcTimestamp)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {picking && (
        <SwecPicker
          initial={cats}
          vendorName={v.registeredName!}
          onCancel={() => setPicking(false)}
          onSave={(codes) => saveCats.mutate(codes)}
        />
      )}
    </>
  )
}

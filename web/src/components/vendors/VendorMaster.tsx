import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getVendors, type VendorListItem } from '../../api/client'
import { useSwec } from '../../api/swec'
import { Icon } from '../Icon'
import { EmptyState } from '../ui'
import { StatusBadge, TypeBadge } from './badges'

const REGIONS = ['Peninsular', 'Sarawak', 'Sabah']

export function VendorMaster({ onOpen, onNavigate }: { onOpen: (id: string) => void; onNavigate?: (key: string) => void }) {
  const { data: swec } = useSwec()
  const [q, setQ] = useState('')
  const [type, setType] = useState('all')
  const [region, setRegion] = useState('all')

  const { data: vendors = [], isPending } = useQuery({
    queryKey: ['vendors', q, type, region],
    queryFn: () => getVendors({ q, type, region }),
  })

  const swecCount = vendors.filter((v) => v.type === 'SWEC').length

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Vendor Master</h1>
          <p>
            Centralised supplier register — categories, contacts, addresses, currencies,
            banking, compliance, and performance.
          </p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri btn-sm" onClick={() => onNavigate?.('vendors/new')}>
          <Icon name="plus" size={15} /> New vendor
        </button>
      </div>

      <div className="ribbon">
        {vendors.length} vendors · {swecCount} SWEC-registered · {vendors.length - swecCount} Non-SWEC
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search vendor / code</label>
              <input
                type="text"
                value={q}
                placeholder="name or SWK-V-…"
                onChange={(e) => setQ(e.target.value)}
              />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Type</label>
              <select value={type} onChange={(e) => setType(e.target.value)}>
                <option value="all">All</option>
                <option value="SWEC">SWEC</option>
                <option value="Non-SWEC">Non-SWEC</option>
              </select>
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Region</label>
              <select value={region} onChange={(e) => setRegion(e.target.value)}>
                <option value="all">All</option>
                {REGIONS.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Vendor</th>
              <th>Type</th>
              <th>SWEC codes</th>
              <th>Region</th>
              <th className="amt">Rating</th>
              <th className="amt">OTD</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {vendors.map((v: VendorListItem) => (
              <tr key={v.id} className="drillrow" onClick={() => v.id && onOpen(v.id)}>
                <td>
                  <div style={{ fontWeight: 700 }}>{v.name}</div>
                  <div className="hint">{v.code}</div>
                </td>
                <td>
                  <TypeBadge type={v.type} />
                </td>
                <td style={{ whiteSpace: 'normal', maxWidth: 280 }}>
                  {(v.categories ?? []).map((c) => (
                    <span key={c} className="swchip" title={swec?.path(c)}>
                      {swec?.label(c) ?? c}
                    </span>
                  ))}
                </td>
                <td>
                  {v.region}
                  <div className="hint">{v.state}</div>
                </td>
                <td className="amt">★ {v.rating}</td>
                <td className="amt">{v.otd == null ? '—' : `${v.otd}%`}</td>
                <td>
                  <StatusBadge status={v.status} />
                </td>
                <td className="amt">
                  <span className="btn btn-ghost btn-sm">
                    Open <Icon name="chev" size={13} />
                  </span>
                </td>
              </tr>
            ))}
            {!isPending && vendors.length === 0 && (
              <tr>
                <td colSpan={8}>
                  <EmptyState>No vendors match the filter.</EmptyState>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="hint" style={{ marginTop: 10 }}>
        {vendors.length} vendor(s)
      </p>
    </>
  )
}

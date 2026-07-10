import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getUsers,
  getVendorLogins,
  createUser,
  updateUser,
  type UserDto,
} from '../../api/client'
import { Icon } from '../Icon'
import { initials, roleLabel } from '../../lib/format'

// Internal roles only — the Vendor role is deliberately absent (SoD): internal
// users are never vendors.
const INTERNAL_ROLES = ['Buyer', 'Approver', 'TechEvaluator', 'CommEvaluator', 'Admin']

interface Draft {
  id: string | null
  name: string
  email: string
  roles: string[]
  isActive: boolean
}

function UserModal({
  draft,
  onClose,
  onSave,
}: {
  draft: Draft
  onClose: () => void
  onSave: (d: Draft) => void
}) {
  const [d, setD] = useState<Draft>(draft)
  const toggleRole = (r: string) =>
    setD((p) => ({
      ...p,
      roles: p.roles.includes(r) ? p.roles.filter((x) => x !== r) : [...p.roles, r],
    }))

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal">
        <div className="mhead">
          <Icon name="vendor" size={20} />
          <h3>{d.id ? 'Edit User' : 'Add User'}</h3>
        </div>
        <div className="mbody">
          <div className="field">
            <label>Name</label>
            <input type="text" value={d.name} placeholder="Full name" onChange={(e) => setD({ ...d, name: e.target.value })} />
          </div>
          <div className="field">
            <label>Email</label>
            <input type="text" value={d.email} placeholder="name@hypernix.test" onChange={(e) => setD({ ...d, email: e.target.value })} />
          </div>
          <div className="field">
            <label>Roles</label>
            <div className="ckcol">
              {INTERNAL_ROLES.map((r) => (
                <label className="ck" key={r}>
                  <input type="checkbox" checked={d.roles.includes(r)} onChange={() => toggleRole(r)} /> {roleLabel(r)}
                </label>
              ))}
            </div>
            <p className="hint" style={{ marginTop: 6 }}>
              Internal users cannot be assigned the Vendor role (segregation of duties).
            </p>
          </div>
        </div>
        <div className="mfoot">
          <button type="button" className="btn btn-out" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn-pri" onClick={() => onSave(d)}>
            <Icon name="chev" size={15} /> Save user
          </button>
        </div>
      </div>
    </div>
  )
}

export function AdminUsers() {
  const qc = useQueryClient()
  const [draft, setDraft] = useState<Draft | null>(null)
  const { data: users = [] } = useQuery({ queryKey: ['users'], queryFn: getUsers })
  const { data: vendorLogins = [] } = useQuery({ queryKey: ['vendor-logins'], queryFn: getVendorLogins })

  const save = useMutation({
    mutationFn: (d: Draft) =>
      d.id
        ? updateUser(d.id, { name: d.name, email: d.email, roles: d.roles, isActive: d.isActive })
        : createUser({ name: d.name, email: d.email, roles: d.roles }),
    onSuccess: () => {
      setDraft(null)
      void qc.invalidateQueries({ queryKey: ['users'] })
    },
  })

  const edit = (u: UserDto) =>
    setDraft({ id: u.id ?? null, name: u.name ?? '', email: u.email ?? '', roles: [...(u.roles ?? [])], isActive: u.isActive ?? true })
  const add = () => setDraft({ id: null, name: '', email: '', roles: [], isActive: true })

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>User Management</h1>
          <p>Roles and access — how each person is provisioned. Internal users are never vendors.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri" onClick={add}>
          <Icon name="chev" size={15} /> Add user
        </button>
      </div>

      <div className="card" style={{ marginBottom: 16 }}>
        <table>
          <thead>
            <tr>
              <th>User</th>
              <th>Roles</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span className="cav">{initials(u.name)}</span>
                    <div>
                      <div style={{ fontWeight: 700 }}>{u.name}</div>
                      <div className="hint">{u.email}</div>
                    </div>
                  </div>
                </td>
                <td>
                  {(u.roles ?? []).length ? (
                    (u.roles ?? []).map((r) => (
                      <span className="chip" key={r}>
                        {roleLabel(r)}
                      </span>
                    ))
                  ) : (
                    <span className="hint">none</span>
                  )}
                </td>
                <td>
                  <span className={`badge ${u.isActive ? 'b-green' : 'b-grey'}`}>
                    {u.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td className="amt">
                  <div className="rowactions">
                    <button type="button" className="btn btn-out btn-sm" onClick={() => edit(u)}>
                      <Icon name="edit" size={14} /> Edit
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Vendor logins</h3>
          <div className="spacer" />
          <span className="hint">separate supplier-portal principals · {vendorLogins.length}</span>
        </div>
        <table>
          <thead>
            <tr>
              <th>Login</th>
              <th>Acts for vendor</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {vendorLogins.map((vu) => (
              <tr key={vu.id}>
                <td>
                  <div style={{ fontWeight: 600 }}>{vu.email}</div>
                  <div className="hint">{vu.code}</div>
                </td>
                <td>{vu.vendorName}</td>
                <td>
                  <span className={`badge ${vu.isActive ? 'b-green' : 'b-grey'}`}>
                    {vu.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="ribbon" style={{ marginTop: 16 }}>
        A user can hold several internal roles. Vendor logins are a separate principal type
        and may only act for their own vendor company.
      </div>

      {draft && <UserModal draft={draft} onClose={() => setDraft(null)} onSave={(d) => save.mutate(d)} />}
    </>
  )
}

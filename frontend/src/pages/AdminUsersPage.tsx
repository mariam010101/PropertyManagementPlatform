import { useEffect, useState } from 'react'
import { api, type UserAdminDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ALL_ROLES = ['Administrator', 'PropertyManager', 'Resident', 'Technician', 'Accountant']

export default function AdminUsersPage() {
  const { user } = useAuth()
  const [users, setUsers] = useState<UserAdminDto[]>([])
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<Record<string, string[]>>({})
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)

  const load = async (term?: string) => {
    const list = await api.getUsers(term)
    setUsers(list)
    const sel: Record<string, string[]> = {}
    list.forEach((u) => (sel[u.id] = [...u.roles]))
    setSelected(sel)
  }

  useEffect(() => {
    load().catch((e) => setError((e as Error).message))
  }, [])

  const toggleRole = (userId: string, role: string) => {
    setSelected((prev) => {
      const current = prev[userId] ?? []
      const next = current.includes(role) ? current.filter((r) => r !== role) : [...current, role]
      return { ...prev, [userId]: next }
    })
  }

  const saveRoles = async (u: UserAdminDto) => {
    setBusyId(u.id)
    setError('')
    try {
      await api.setUserRoles(u.id, selected[u.id] ?? [])
      await load(search)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusyId(null)
    }
  }

  const toggleActive = async (u: UserAdminDto) => {
    setBusyId(u.id)
    setError('')
    try {
      if (u.isActive) {
        await api.deactivateUser(u.id)
      } else {
        await api.activateUser(u.id)
      }
      await load(search)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusyId(null)
    }
  }

  const isSelf = (id: string) => id === user?.id

  return (
    <div className="container">
      <h2>Users & Roles</h2>
      <p className="muted">
        Role-based access control (FR-RBAC-001..006). Permissions are granted by role; role changes apply immediately.
      </p>
      {error && <div className="error">{error}</div>}

      <div className="row">
        <input placeholder="Search by name or email" value={search} onChange={(e) => setSearch(e.target.value)} />
        <button onClick={() => load(search)}>Search</button>
      </div>

      {users.map((u) => (
        <div key={u.id} className="panel">
          <div className="row space-between">
            <div>
              <strong>{u.firstName} {u.lastName}</strong> — {u.email}
              {isSelf(u.id) && <span className="badge">you</span>}
              {u.isActive ? <span className="badge">Active</span> : <span className="badge red">Inactive</span>}
            </div>
            <div className="row">
              <button className="ghost" disabled={busyId === u.id} onClick={() => saveRoles(u)}>Save roles</button>
              {u.isActive ? (
                <button
                  className="ghost danger"
                  disabled={busyId === u.id || isSelf(u.id)}
                  onClick={() => toggleActive(u)}
                >
                  Deactivate
                </button>
              ) : (
                <button className="ghost" disabled={busyId === u.id} onClick={() => toggleActive(u)}>
                  Activate
                </button>
              )}
            </div>
          </div>
          <div className="row roles">
            {ALL_ROLES.map((role) => {
              const checked = (selected[u.id] ?? []).includes(role)
              const disabled = isSelf(u.id) && role === 'Administrator'
              return (
                <label key={role} className="check">
                  <input type="checkbox" checked={checked} disabled={disabled} onChange={() => toggleRole(u.id, role)} />
                  {role}
                </label>
              )
            })}
          </div>
        </div>
      ))}
    </div>
  )
}

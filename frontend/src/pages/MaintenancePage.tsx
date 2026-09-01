import { useEffect, useState, type FormEvent } from 'react'
import { api, type MaintenancePriority, type MaintenanceRequestDto, type MaintenanceStatus, type ResidentProfileDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const TECHNICIAN = 'Technician'

export default function MaintenancePage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isResident = roles.includes(RESIDENT)
  const isManager = roles.includes(MANAGER) || roles.includes(ADMIN)
  const isTech = roles.includes(TECHNICIAN)

  const [requests, setRequests] = useState<MaintenanceRequestDto[]>([])
  const [profile, setProfile] = useState<ResidentProfileDto | null>(null)
  const [technicians, setTechnicians] = useState<{ id: string; firstName: string; lastName: string; email: string }[]>([])
  const [error, setError] = useState('')

  // submit form
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState<MaintenancePriority>('Medium')

  const load = async () => {
    setRequests(await api.getMaintenance())
  }

  useEffect(() => {
    ;(async () => {
      try {
        if (isResident) {
          setProfile(await api.getMyProfile())
        }
        if (isManager || isTech) {
          setTechnicians(await api.getStaff('Technician'))
        }
        await load()
      } catch (e) {
        setError((e as Error).message)
      }
    })()
  }, [])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      if (!profile?.currentUnitId) {
        setError('You have no assigned unit yet. A property manager must assign you one first.')
        return
      }
      await api.submitMaintenance({ title, description, unitId: profile.currentUnitId, priority })
      setTitle('')
      setDescription('')
      await load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const assign = async (id: string, technicianUserId: string) => {
    await api.assignMaintenance(id, technicianUserId)
    await load()
  }

  const changeStatus = async (id: string, status: MaintenanceStatus) => {
    await api.updateMaintenanceStatus(id, status)
    await load()
  }

  const confirm = async (id: string) => {
    await api.confirmMaintenance(id)
    await load()
  }

  return (
    <div className="container">
      <h2>Maintenance</h2>
      {error && <div className="error">{error}</div>}

      {isResident && (
        <form className="panel" onSubmit={submit}>
          <h3>Submit a request</h3>
          <label>Title <input value={title} onChange={(e) => setTitle(e.target.value)} required /></label>
          <label>Description <textarea value={description} onChange={(e) => setDescription(e.target.value)} /></label>
          <label>Priority
            <select value={priority} onChange={(e) => setPriority(e.target.value as MaintenancePriority)}>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
              <option value="Urgent">Urgent</option>
            </select>
          </label>
          <button type="submit">Submit</button>
        </form>
      )}

      {requests.map((r) => (
        <div key={r.id} className="panel">
          <div className="row space-between">
            <strong>{r.title}</strong>
            <span className="badge">{r.status}</span>
          </div>
          <p className="muted">{r.description}</p>
          <div className="muted">
            Unit {r.unitNumber} · Priority {r.priority} · Submitted {new Date(r.createdAt).toLocaleString()}
          </div>
          {r.history.length > 0 && (
            <details>
              <summary>History</summary>
              <ul>
                {r.history.map((h) => (
                  <li key={h.id}>
                    {h.fromStatus ?? '—'} → {h.toStatus}
                    {h.comment ? ` · ${h.comment}` : ''} ({new Date(h.changedAt).toLocaleString()})
                  </li>
                ))}
              </ul>
            </details>
          )}

          {isManager && r.status === 'Submitted' && (
            <div className="row">
              <select
                defaultValue=""
                onChange={(e) => e.target.value && assign(r.id, e.target.value)}
              >
                <option value="">Assign to technician…</option>
                {technicians.map((t) => (
                  <option key={t.id} value={t.id}>{t.firstName} {t.lastName} ({t.email})</option>
                ))}
              </select>
            </div>
          )}

          {isTech && r.status === 'Assigned' && (
            <button onClick={() => changeStatus(r.id, 'InProgress')}>Start (In Progress)</button>
          )}
          {isTech && r.status === 'InProgress' && (
            <button onClick={() => changeStatus(r.id, 'Completed')}>Mark completed</button>
          )}

          {isResident && r.status === 'Completed' && (
            <button onClick={() => confirm(r.id)}>Confirm completion</button>
          )}
        </div>
      ))}

      {requests.length === 0 && <p className="muted">No maintenance requests.</p>}
    </div>
  )
}

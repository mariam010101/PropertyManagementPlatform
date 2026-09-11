import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import {
  api,
  type MaintenancePriority,
  type MaintenanceRequestDto,
  type MaintenanceStatus,
  type ResidentProfileDto,
} from '../api/client'
import { useAuth } from '../auth/AuthContext'

/**
 * Maintenance (BR-004, ADR-0009). The client mirrors the server state machine
 * exactly — it only offers transitions the API will accept for the signed-in
 * role, and it never assumes a transition succeeded: every action reloads the
 * request from the API afterwards.
 */

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const TECHNICIAN = 'Technician'

const PRIORITIES: MaintenancePriority[] = ['Low', 'Medium', 'High', 'Urgent']
const STATUSES: MaintenanceStatus[] = [
  'Submitted',
  'Assigned',
  'InProgress',
  'Completed',
  'Confirmed',
  'Closed',
  'Cancelled',
]

const PRIORITY_PILL: Record<MaintenancePriority, string> = {
  Low: 'muted',
  Medium: 'info',
  High: 'warn',
  Urgent: 'danger',
}

const STATUS_PILL: Record<MaintenanceStatus, string> = {
  Submitted: 'info',
  Assigned: 'info',
  InProgress: 'warn',
  Completed: 'warn',
  Confirmed: 'ok',
  Closed: 'ok',
  Cancelled: 'muted',
}

const STATUS_LABEL: Record<MaintenanceStatus, string> = {
  Submitted: 'Submitted',
  Assigned: 'Assigned',
  InProgress: 'In progress',
  Completed: 'Completed',
  Confirmed: 'Confirmed',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
}

/** History rows carry the raw status string, so map it back to a readable label. */
const statusLabel = (value: string) => STATUS_LABEL[value as MaintenanceStatus] ?? value

const formatWhen = (value?: string) => (value ? new Date(value).toLocaleString() : '—')

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
  const [notice, setNotice] = useState('')
  const [loaded, setLoaded] = useState(false)
  const [busy, setBusy] = useState(false)

  // filters (staff)
  const [filterStatus, setFilterStatus] = useState<'' | MaintenanceStatus>('')
  const [filterPriority, setFilterPriority] = useState<'' | MaintenancePriority>('')

  // expanded request detail
  const [openId, setOpenId] = useState<string | null>(null)
  const [comments, setComments] = useState<Record<string, string>>({})

  // submit form
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState<MaintenancePriority>('Medium')

  const load = async (opts?: { status?: '' | MaintenanceStatus; priority?: '' | MaintenancePriority }) => {
    const status = opts?.status ?? filterStatus
    const prio = opts?.priority ?? filterPriority
    setRequests(
      await api.getMaintenance({
        status: status || undefined,
        priority: prio || undefined,
      }),
    )
    setLoaded(true)
  }

  /** Filters apply immediately; there is no separate submit step to keep the list scannable. */
  const changeFilters = async (status: '' | MaintenanceStatus, priority: '' | MaintenancePriority) => {
    setFilterStatus(status)
    setFilterPriority(priority)
    setBusy(true)
    setError('')
    try {
      await load({ status, priority })
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    void (async () => {
      try {
        if (isResident) {
          setProfile(await api.getMyProfile())
        }
        if (isManager || isTech) {
          setTechnicians(await api.getStaff('Technician'))
        }
        setRequests(await api.getMaintenance())
        setLoaded(true)
      } catch (e) {
        setError((e as Error).message)
      }
    })()
  }, [isResident, isManager, isTech])

  const run = async (action: () => Promise<string>) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      setNotice(await action())
      await load()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const commentFor = (id: string) => comments[id]?.trim() || undefined

  const refreshOne = async (id: string) => {
    const fresh = await api.getMaintenanceRequest(id)
    setRequests((current) => current.map((r) => (r.id === id ? fresh : r)))
  }

  const submit = (e: FormEvent) =>
    run(async () => {
      e.preventDefault()
      if (!profile?.currentUnitId) {
        throw new Error('You have no assigned unit yet. A property manager must assign you one first.')
      }
      await api.submitMaintenance({ title, description, unitId: profile.currentUnitId, priority })
      setTitle('')
      setDescription('')
      setPriority('Medium')
      return 'Request submitted.'
    })

  const assign = (id: string, technicianUserId: string) =>
    run(async () => {
      if (!technicianUserId) return 'No change.'
      await api.assignMaintenance(id, technicianUserId, commentFor(id))
      return 'Request assigned.'
    })

  const setPriorityFor = (id: string, next: MaintenancePriority) =>
    run(async () => {
      await api.setMaintenancePriority(id, next)
      return `Priority set to ${next}.`
    })

  const changeStatus = (id: string, status: MaintenanceStatus) =>
    run(async () => {
      await api.updateMaintenanceStatus(id, status, commentFor(id))
      return `Request moved to ${STATUS_LABEL[status]}.`
    })

  const confirm = (id: string) =>
    run(async () => {
      await api.confirmMaintenance(id, commentFor(id))
      return 'Completion confirmed — the request is closed.'
    })

  const onFileSelected = (id: string, event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    void run(async () => {
      await api.uploadMaintenanceAttachment(id, file)
      await refreshOne(id)
      return `Attached ${file.name}.`
    })
  }

  const isRequester = (r: MaintenanceRequestDto) => !!profile && profile.id === r.requestedByResidentId
  const isClosed = (r: MaintenanceRequestDto) => r.status === 'Closed' || r.status === 'Cancelled'
  const canAttach = (r: MaintenanceRequestDto) =>
    !isClosed(r) && (isManager || isRequester(r) || (isTech && r.assignedToUserId === user?.id))

  return (
    <div className="container">
      <div className="page-head">
        <div>
          <h2>Maintenance</h2>
          <p className="muted">
            {isResident
              ? 'Submit requests for your unit and confirm completed work.'
              : 'Triage requests, assign technicians and track the work to closure.'}
          </p>
        </div>
        <div className="row">
          <button type="button" className="ghost" disabled={busy} onClick={() => void load()}>
            Refresh
          </button>
        </div>
      </div>

      {error && <div className="error">{error}</div>}
      {notice && <div className="hint">{notice}</div>}

      {isResident && (
        <form className="panel" onSubmit={submit}>
          <h3>Submit a request</h3>
          <label className="field">
            <span>Title</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} required />
          </label>
          <label className="field">
            <span>Description</span>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} />
          </label>
          <label className="field">
            <span>Priority</span>
            <select value={priority} onChange={(e) => setPriority(e.target.value as MaintenancePriority)}>
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </label>
          <p className="muted">
            {profile?.currentUnitNumber
              ? `Will be raised against unit ${profile.currentUnitNumber}.`
              : 'No unit is assigned to you yet.'}
          </p>
          <button type="submit" disabled={busy}>Submit</button>
        </form>
      )}

      {(isManager || isTech) && (
        <div className="row">
          <label className="field">
            <span>Status</span>
            <select
              value={filterStatus}
              disabled={busy}
              onChange={(e) => void changeFilters(e.target.value as '' | MaintenanceStatus, filterPriority)}
            >
              <option value="">All statuses</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>{STATUS_LABEL[s]}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Priority</span>
            <select
              value={filterPriority}
              disabled={busy}
              onChange={(e) => void changeFilters(filterStatus, e.target.value as '' | MaintenancePriority)}
            >
              <option value="">All priorities</option>
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </label>
          <p className="muted">{requests.length} shown</p>
        </div>
      )}

      {!loaded && <p className="muted">Loading requests…</p>}
      {loaded && requests.length === 0 && (
        <div className="panel muted">
          {isResident ? 'You have not submitted any maintenance requests yet.' : 'No maintenance requests match this view.'}
        </div>
      )}

      {requests.map((r) => {
        const open = openId === r.id
        return (
          <article key={r.id} className="panel">
            <div className="row space-between">
              <div>
                <strong>{r.title}</strong>
                <div className="muted request__meta">
                  Unit {r.unitNumber ?? '—'} · Submitted {formatWhen(r.createdAt)}
                  {r.assignedToName ? ` · Assigned to ${r.assignedToName}` : ''}
                </div>
              </div>
              <div className="row">
                <span className={`pill pill--${PRIORITY_PILL[r.priority]}`}>{r.priority}</span>
                <span className={`pill pill--${STATUS_PILL[r.status]}`}>{STATUS_LABEL[r.status]}</span>
                {r.attachmentCount > 0 && (
                  <span className="pill pill--muted">
                    {r.attachmentCount} attachment{r.attachmentCount === 1 ? '' : 's'}
                  </span>
                )}
              </div>
            </div>

            {r.description && <p className="muted">{r.description}</p>}

            {r.cancellationReason && <p className="muted">Cancelled: {r.cancellationReason}</p>}

            <div className="row" style={{ marginTop: 10 }}>
              <button
                type="button"
                className="ghost"
                onClick={() => {
                  const next = open ? null : r.id
                  setOpenId(next)
                  if (next) void refreshOne(r.id).catch((err) => setError((err as Error).message))
                }}
              >
                {open ? 'Hide details' : 'View details'}
              </button>
            </div>

            {open && (
              <div className="indent" style={{ marginTop: 12 }}>
                <label className="field">
                  <span>Comment (optional, recorded with the next change)</span>
                  <input
                    value={comments[r.id] ?? ''}
                    onChange={(e) => setComments((c) => ({ ...c, [r.id]: e.target.value }))}
                    placeholder="Add context for the technician or the resident"
                  />
                </label>

                <div className="row">
                  {isManager && r.status === 'Submitted' && (
                    <>
                      <label className="field" style={{ maxWidth: 280 }}>
                        <span>Assign technician</span>
                        <select defaultValue="" disabled={busy} onChange={(e) => void assign(r.id, e.target.value)}>
                          <option value="">Select technician…</option>
                          {technicians.map((t) => (
                            <option key={t.id} value={t.id}>
                              {t.firstName} {t.lastName} ({t.email})
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="field" style={{ maxWidth: 200 }}>
                        <span>Priority</span>
                        <select
                          value={r.priority}
                          disabled={busy}
                          onChange={(e) => void setPriorityFor(r.id, e.target.value as MaintenancePriority)}
                        >
                          {PRIORITIES.map((p) => (
                            <option key={p} value={p}>{p}</option>
                          ))}
                        </select>
                      </label>
                    </>
                  )}

                  {isManager && r.status !== 'Submitted' && !isClosed(r) && (
                    <label className="field" style={{ maxWidth: 200 }}>
                      <span>Priority</span>
                      <select
                        value={r.priority}
                        disabled={busy}
                        onChange={(e) => void setPriorityFor(r.id, e.target.value as MaintenancePriority)}
                      >
                        {PRIORITIES.map((p) => (
                          <option key={p} value={p}>{p}</option>
                        ))}
                      </select>
                    </label>
                  )}

                  {(isTech || isManager) && r.status === 'Assigned' && (
                    <button type="button" disabled={busy} onClick={() => void changeStatus(r.id, 'InProgress')}>
                      Start work
                    </button>
                  )}
                  {(isTech || isManager) && r.status === 'InProgress' && (
                    <button type="button" disabled={busy} onClick={() => void changeStatus(r.id, 'Completed')}>
                      Mark completed
                    </button>
                  )}
                  {isResident && isRequester(r) && r.status === 'Completed' && (
                    <>
                      <button type="button" disabled={busy} onClick={() => void confirm(r.id)}>
                        Confirm completion
                      </button>
                      <button type="button" className="ghost" disabled={busy} onClick={() => void changeStatus(r.id, 'InProgress')}>
                        Reopen
                      </button>
                    </>
                  )}
                  {(isRequester(r) || isManager) &&
                    (r.status === 'Submitted' || r.status === 'Assigned' || r.status === 'InProgress') && (
                      <button type="button" className="ghost danger" disabled={busy} onClick={() => void changeStatus(r.id, 'Cancelled')}>
                        Cancel request
                      </button>
                    )}
                </div>

                {canAttach(r) && (
                  <label className="field" style={{ maxWidth: 320 }}>
                    <span>Add an attachment</span>
                    <input type="file" disabled={busy} onChange={(e) => onFileSelected(r.id, e)} />
                  </label>
                )}

                <h4>Attachments</h4>
                {r.attachments.length === 0 ? (
                  <p className="muted">No attachments yet.</p>
                ) : (
                  <ul className="muted">
                    {r.attachments.map((a) => (
                      <li key={a.id}>
                        {a.fileName} · {a.contentType} · uploaded {formatWhen(a.uploadedAt)}
                      </li>
                    ))}
                  </ul>
                )}

                <h4>History</h4>
                {r.history.length === 0 ? (
                  <p className="muted">No history recorded.</p>
                ) : (
                  <ul className="muted">
                    {r.history.map((h) => (
                      <li key={h.id}>
                        {h.fromStatus ? `${statusLabel(h.fromStatus)} → ` : ''}
                        {statusLabel(h.toStatus)}
                        {h.comment ? ` · ${h.comment}` : ''} · {formatWhen(h.changedAt)}
                      </li>
                    ))}
                  </ul>
                )}

                <p className="muted">
                  Assigned: {r.assignedToName ?? 'nobody yet'} {r.assignedAt ? `(${formatWhen(r.assignedAt)})` : ''} ·
                  Completed: {formatWhen(r.completedAt)} · Closed: {formatWhen(r.closedAt)}
                </p>
              </div>
            )}
          </article>
        )
      })}
    </div>
  )
}

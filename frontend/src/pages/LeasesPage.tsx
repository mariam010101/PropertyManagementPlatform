import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { api, type LeaseDetailDto, type LeaseDto, type LeaseStatus } from '../api/client'
import { useAuth } from '../auth/AuthContext'

/**
 * Leases (BR-006). Managers/admins create leases, update terms (each change is a
 * new version the backend records) and file documents; residents see their own
 * agreements with the same document and version history. Every action re-reads
 * the lease from the API rather than assuming the mutation succeeded.
 */

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'

const STATUSES: LeaseStatus[] = ['Active', 'Expired', 'Terminated', 'Cancelled']

const STATUS_PILL: Record<LeaseStatus, string> = {
  Active: 'ok',
  Expired: 'warn',
  Terminated: 'danger',
  Cancelled: 'muted',
}

const STATUS_LABEL: Record<LeaseStatus, string> = {
  Active: 'Active',
  Expired: 'Expired',
  Terminated: 'Terminated',
  Cancelled: 'Cancelled',
}

const money = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
const toDateInput = (iso: string) => iso.slice(0, 10)
const formatDate = (iso: string) => new Date(iso).toLocaleDateString()
const formatWhen = (iso: string) => new Date(iso).toLocaleString()

export default function LeasesPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const canManage = roles.includes(ADMIN) || roles.includes(MANAGER)

  const [leases, setLeases] = useState<LeaseDto[]>([])
  const [filterStatus, setFilterStatus] = useState<'' | LeaseStatus>('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [loaded, setLoaded] = useState(false)
  const [busy, setBusy] = useState(false)

  // create form (managers/admins)
  const [showCreate, setShowCreate] = useState(false)
  const [residents, setResidents] = useState<{ id: string; userId: string; firstName: string; lastName: string }[]>([])
  const [units, setUnits] = useState<{ id: string; label: string }[]>([])
  const [residentId, setResidentId] = useState('')
  const [unitId, setUnitId] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [monthlyRent, setMonthlyRent] = useState('')

  // expanded lease detail
  const [openId, setOpenId] = useState<string | null>(null)
  const [detail, setDetail] = useState<LeaseDetailDto | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [terminationComment, setTerminationComment] = useState('')

  // term editing
  const [editing, setEditing] = useState(false)
  const [editStart, setEditStart] = useState('')
  const [editEnd, setEditEnd] = useState('')
  const [editRent, setEditRent] = useState('')

  const load = async (status: '' | LeaseStatus = filterStatus) => {
    setLeases(await api.getLeases(status || undefined))
    setLoaded(true)
  }

  useEffect(() => {
    void (async () => {
      try {
        await load('')
        if (canManage) {
          const r = await api.getResidents()
          setResidents(r.map((x) => ({ id: x.id, userId: x.userId, firstName: x.firstName, lastName: x.lastName })))

          // Units carry no building/property label, so compose a readable one from
          // the property/building walk the existing create flow already performed.
          const properties = await api.getProperties()
          const collected: { id: string; label: string }[] = []
          for (const p of properties) {
            const buildings = await api.getBuildings(p.id)
            for (const b of buildings) {
              const buildingUnits = await api.getUnits(b.id)
              for (const u of buildingUnits) {
                collected.push({ id: u.id, label: `${p.name} · ${b.name} · ${u.unitNumber}` })
              }
            }
          }
          setUnits(collected)
        }
      } catch (e) {
        setError((e as Error).message)
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canManage])

  const changeFilter = async (status: '' | LeaseStatus) => {
    setFilterStatus(status)
    setBusy(true)
    setError('')
    try {
      await load(status)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const run = async (action: () => Promise<string>) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      setNotice(await action())
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const applyDetail = (fresh: LeaseDetailDto) => {
    setDetail(fresh)
    setEditStart(toDateInput(fresh.startDate))
    setEditEnd(toDateInput(fresh.endDate))
    setEditRent(String(fresh.monthlyRent))
  }

  const open = async (leaseId: string) => {
    if (openId === leaseId) {
      setOpenId(null)
      setDetail(null)
      setEditing(false)
      return
    }
    setOpenId(leaseId)
    setDetail(null)
    setEditing(false)
    setTerminationComment('')
    setDetailLoading(true)
    try {
      applyDetail(await api.getLease(leaseId))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setDetailLoading(false)
    }
  }

  const submitCreate = (e: FormEvent) =>
    run(async () => {
      e.preventDefault()
      const resident = residents.find((x) => x.id === residentId)
      if (!resident) throw new Error('Select a resident.')
      if (!unitId) throw new Error('Select a unit.')
      await api.createLease({
        residentUserId: resident.userId,
        unitId,
        startDate: new Date(startDate).toISOString(),
        endDate: new Date(endDate).toISOString(),
        monthlyRent: Number(monthlyRent),
      })
      setShowCreate(false)
      setResidentId('')
      setUnitId('')
      setStartDate('')
      setEndDate('')
      setMonthlyRent('')
      await load()
      return 'Lease created.'
    })

  const saveTerms = (leaseId: string) =>
    run(async () => {
      await api.updateLease(leaseId, {
        startDate: new Date(editStart).toISOString(),
        endDate: new Date(editEnd).toISOString(),
        monthlyRent: Number(editRent),
      })
      setEditing(false)
      applyDetail(await api.getLease(leaseId))
      await load()
      return 'Lease terms updated — the change is recorded as a new version.'
    })

  const terminate = (leaseId: string) =>
    run(async () => {
      await api.terminateLease(leaseId, terminationComment.trim() || undefined)
      setTerminationComment('')
      applyDetail(await api.getLease(leaseId))
      await load()
      return 'Lease terminated.'
    })

  const upload = (leaseId: string, event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    void run(async () => {
      await api.uploadLeaseDocument(leaseId, file)
      applyDetail(await api.getLease(leaseId))
      await load()
      return `Attached ${file.name}.`
    })
  }

  return (
    <div className="container">
      <div className="page-head">
        <div>
          <h2>Leases</h2>
          <p className="muted">
            {canManage
              ? 'Create leases, update terms (every change is a new version) and file the signed documents.'
              : 'Your lease agreements, their documents and version history.'}
          </p>
        </div>
        <div className="row">
          <button type="button" className="ghost" disabled={busy} onClick={() => void load()}>
            Refresh
          </button>
          {canManage && (
            <button type="button" onClick={() => setShowCreate((s) => !s)}>
              {showCreate ? 'Close form' : 'Create lease'}
            </button>
          )}
        </div>
      </div>

      {error && <div className="error">{error}</div>}
      {notice && <div className="hint">{notice}</div>}

      {canManage && showCreate && (
        <form className="panel" onSubmit={submitCreate}>
          <h3>Create lease</h3>
          <div className="row">
            <label className="field">
              <span>Resident</span>
              <select value={residentId} onChange={(e) => setResidentId(e.target.value)} required>
                <option value="">Select resident</option>
                {residents.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.firstName} {r.lastName}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Unit</span>
              <select value={unitId} onChange={(e) => setUnitId(e.target.value)} required>
                <option value="">Select unit</option>
                {units.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="row">
            <label className="field">
              <span>Start date</span>
              <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
            </label>
            <label className="field">
              <span>End date</span>
              <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} required />
            </label>
            <label className="field">
              <span>Monthly rent (USD)</span>
              <input
                type="number"
                step="0.01"
                min="0"
                value={monthlyRent}
                onChange={(e) => setMonthlyRent(e.target.value)}
                required
              />
            </label>
          </div>
          <button type="submit" disabled={busy}>
            Save lease
          </button>
        </form>
      )}

      {canManage && (
        <div className="row">
          <label className="field" style={{ maxWidth: 220 }}>
            <span>Status</span>
            <select
              value={filterStatus}
              disabled={busy}
              onChange={(e) => void changeFilter(e.target.value as '' | LeaseStatus)}
            >
              <option value="">All statuses</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABEL[s]}
                </option>
              ))}
            </select>
          </label>
          <p className="muted">{leases.length} shown</p>
        </div>
      )}

      {!loaded && <div className="state state--loading">Loading leases…</div>}
      {loaded && leases.length === 0 && (
        <div className="state state--empty">
          {canManage ? 'No leases match this view.' : 'You have no lease agreements yet.'}
        </div>
      )}

      {leases.map((l) => {
        const isOpen = openId === l.id
        const active = isOpen && detail?.id === l.id ? detail : null
        return (
          <article key={l.id} className="panel">
            <div className="row space-between">
              <div>
                <strong>
                  Unit {l.unitNumber} — {l.residentName}
                </strong>
                <div className="muted request__meta">
                  {l.propertyName} · v{l.currentVersion} · {l.documentCount} document
                  {l.documentCount === 1 ? '' : 's'}
                </div>
              </div>
              <span className={`pill pill--${STATUS_PILL[l.status]}`}>{STATUS_LABEL[l.status]}</span>
            </div>

            <div className="muted">
              {formatDate(l.startDate)} → {formatDate(l.endDate)} · {money(l.monthlyRent)}/mo
              {l.terminatedAt ? ` · terminated ${formatWhen(l.terminatedAt)}` : ''}
            </div>

            <div className="row" style={{ marginTop: 10 }}>
              <button type="button" className="ghost" disabled={busy} onClick={() => void open(l.id)}>
                {isOpen ? 'Hide details' : 'View details'}
              </button>
            </div>

            {isOpen && (
              <div className="indent" style={{ marginTop: 12 }}>
                {detailLoading && !active && <div className="state state--loading">Loading lease…</div>}

                {active && (
                  <>
                    {canManage && active.status !== 'Terminated' && active.status !== 'Cancelled' && (
                      <>
                        <h4>Terms</h4>
                        {!editing ? (
                          <button type="button" className="ghost" disabled={busy} onClick={() => setEditing(true)}>
                            Update terms
                          </button>
                        ) : (
                          <>
                            <div className="row">
                              <label className="field">
                                <span>Start date</span>
                                <input type="date" value={editStart} onChange={(e) => setEditStart(e.target.value)} />
                              </label>
                              <label className="field">
                                <span>End date</span>
                                <input type="date" value={editEnd} onChange={(e) => setEditEnd(e.target.value)} />
                              </label>
                              <label className="field">
                                <span>Monthly rent (USD)</span>
                                <input
                                  type="number"
                                  step="0.01"
                                  min="0"
                                  value={editRent}
                                  onChange={(e) => setEditRent(e.target.value)}
                                />
                              </label>
                            </div>
                            <div className="row">
                              <button type="button" disabled={busy} onClick={() => void saveTerms(active.id)}>
                                Save new version
                              </button>
                              <button type="button" className="ghost" disabled={busy} onClick={() => setEditing(false)}>
                                Cancel
                              </button>
                            </div>
                          </>
                        )}
                      </>
                    )}

                    <h4>Documents</h4>
                    {active.documents.length === 0 ? (
                      <p className="muted">No documents uploaded.</p>
                    ) : (
                      <ul className="muted">
                        {active.documents.map((d) => (
                          <li key={d.id}>
                            {d.fileName} · {d.contentType} · v{d.version} · uploaded {formatWhen(d.uploadedAt)}
                          </li>
                        ))}
                      </ul>
                    )}
                    {canManage && (
                      <label className="field" style={{ maxWidth: 320 }}>
                        <span>Upload a document</span>
                        <input type="file" disabled={busy} onChange={(e) => upload(active.id, e)} />
                      </label>
                    )}

                    <h4>Version history</h4>
                    {active.history.length === 0 ? (
                      <p className="muted">No history recorded.</p>
                    ) : (
                      <ul className="muted">
                        {active.history.map((h) => (
                          <li key={`${h.version}-${h.changedAt}`}>
                            v{h.version} · {h.changeType} · {h.description} · {formatWhen(h.changedAt)}
                          </li>
                        ))}
                      </ul>
                    )}

                    {canManage && active.status === 'Active' && (
                      <>
                        <h4>Terminate</h4>
                        <label className="field">
                          <span>Reason (optional)</span>
                          <input
                            value={terminationComment}
                            onChange={(e) => setTerminationComment(e.target.value)}
                            placeholder="Why is this lease ending early?"
                          />
                        </label>
                        <button
                          type="button"
                          className="ghost danger"
                          disabled={busy}
                          onClick={() => void terminate(active.id)}
                        >
                          Terminate lease
                        </button>
                      </>
                    )}
                  </>
                )}
              </div>
            )}
          </article>
        )
      })}
    </div>
  )
}

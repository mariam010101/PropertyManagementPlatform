import { useEffect, useState, type FormEvent } from 'react'
import { api, type LeaseDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'

export default function LeasesPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const canManage = roles.includes(ADMIN) || roles.includes(MANAGER)

  const [leases, setLeases] = useState<LeaseDto[]>([])
  const [error, setError] = useState('')

  // create form
  const [showCreate, setShowCreate] = useState(false)
  const [residentId, setResidentId] = useState('')
  const [unitId, setUnitId] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [monthlyRent, setMonthlyRent] = useState('')
  const [residents, setResidents] = useState<{ id: string; userId: string; firstName: string; lastName: string }[]>([])
  const [units, setUnits] = useState<{ id: string; buildingId: string; unitNumber: string }[]>([])

  const load = async () => {
    try {
      setLeases(await api.getLeases())
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    ;(async () => {
      try {
        await load()
        if (canManage) {
          const r = await api.getResidents()
          setResidents(r.map((x) => ({ id: x.id, userId: x.userId, firstName: x.firstName, lastName: x.lastName })))
        }
      } catch (e) {
        setError((e as Error).message)
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const chooseResident = async (residentProfileId: string) => {
    setResidentId(residentProfileId)
    const r = residents.find((x) => x.id === residentProfileId)
    if (!r) return
    const props = await api.getProperties()
    const buildings = await api.getBuildings(props[0].id)
    const allUnits: { id: string; buildingId: string; unitNumber: string }[] = []
    for (const b of buildings) {
      allUnits.push(...(await api.getUnits(b.id)))
    }
    setUnits(allUnits)
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      const resident = residents.find((x) => x.id === residentId)
      if (!resident) throw new Error('Select a resident')
      await api.createLease({
        residentUserId: resident.userId,
        unitId,
        startDate: new Date(startDate).toISOString(),
        endDate: new Date(endDate).toISOString(),
        monthlyRent: Number(monthlyRent),
      })
      setShowCreate(false)
      await load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const terminate = async (id: string) => {
    setError('')
    try {
      await api.terminateLease(id, 'Terminated by manager')
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const money = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <div className="container">
      <h2>Leases</h2>
      {error && <div className="error">{error}</div>}

      {canManage && (
        <button onClick={() => setShowCreate((s) => !s)}>{showCreate ? 'Cancel' : 'Create lease'}</button>
      )}

      {canManage && showCreate && (
        <form className="panel" onSubmit={submit}>
          <h3>Create lease</h3>
          <label>
            Resident
            <select value={residentId} onChange={(e) => chooseResident(e.target.value)} required>
              <option value="">Select resident</option>
              {residents.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.firstName} {r.lastName}
                </option>
              ))}
            </select>
          </label>
          <label>
            Unit
            <select value={unitId} onChange={(e) => setUnitId(e.target.value)} required>
              <option value="">Select unit</option>
              {units.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.unitNumber}
                </option>
              ))}
            </select>
          </label>
          <label>Start date <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required /></label>
          <label>End date <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} required /></label>
          <label>Monthly rent <input type="number" step="0.01" min="0" value={monthlyRent} onChange={(e) => setMonthlyRent(e.target.value)} required /></label>
          <button type="submit">Save lease</button>
        </form>
      )}

      {leases.length === 0 && <p className="muted">No leases found.</p>}
      {leases.map((l) => (
        <div key={l.id} className="panel">
          <div className="row space-between">
            <strong>Unit {l.unitNumber} — {l.residentName}</strong>
            <span className="badge">{l.status}</span>
          </div>
          <div className="muted">
            {new Date(l.startDate).toLocaleDateString()} → {new Date(l.endDate).toLocaleDateString()} A· {money(l.monthlyRent)}/mo A· v{l.currentVersion} A· {l.documentCount} doc(s)
          </div>
          {canManage && l.status === 'Active' && <button onClick={() => terminate(l.id)}>Terminate</button>}
        </div>
      ))}
    </div>
  )
}

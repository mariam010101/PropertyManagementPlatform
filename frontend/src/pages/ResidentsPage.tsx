import { useEffect, useState, type FormEvent } from 'react'
import { api, type BuildingDto, type PropertyDto, type ResidentProfileDto, type UnitDto } from '../api/client'

/**
 * Resident management: search, profile editing, effective-dated unit assignment,
 * move-out and deactivation (GAP-021). History is never deleted — move-out and
 * deactivation only change state.
 */
export default function ResidentsPage() {
  const [residents, setResidents] = useState<ResidentProfileDto[]>([])
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)

  const [assignFor, setAssignFor] = useState<ResidentProfileDto | null>(null)
  const [editFor, setEditFor] = useState<ResidentProfileDto | null>(null)
  const [moveOutFor, setMoveOutFor] = useState<ResidentProfileDto | null>(null)

  // cascading unit picker
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [buildings, setBuildings] = useState<BuildingDto[]>([])
  const [units, setUnits] = useState<UnitDto[]>([])
  const [selProperty, setSelProperty] = useState('')
  const [selBuilding, setSelBuilding] = useState('')
  const [selUnit, setSelUnit] = useState('')

  // edit form
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [moveOutDate, setMoveOutDate] = useState('')

  const load = async (term?: string) => {
    setResidents(await api.getResidents(term))
    setLoaded(true)
  }

  useEffect(() => {
    load().catch((e) => setError((e as Error).message))
  }, [])

  const run = async (action: () => Promise<string>) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      setNotice(await action())
      await load(search)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const openAssign = async (resident: ResidentProfileDto) => {
    setAssignFor(resident)
    setSelProperty('')
    setSelBuilding('')
    setSelUnit('')
    setBuildings([])
    setUnits([])
    try {
      setProperties(await api.getProperties())
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const onSelectProperty = async (propertyId: string) => {
    setSelProperty(propertyId)
    setSelBuilding('')
    setSelUnit('')
    setUnits([])
    setBuildings(await api.getBuildings(propertyId))
  }

  const onSelectBuilding = async (buildingId: string) => {
    setSelBuilding(buildingId)
    setSelUnit('')
    setUnits(await api.getUnits(buildingId))
  }

  const submitAssign = (event: FormEvent) =>
    run(async () => {
      event.preventDefault()
      if (!assignFor || !selUnit) return 'Nothing to assign.'
      await api.assignUnit(assignFor.id, selUnit)
      setAssignFor(null)
      return 'Unit assigned.'
    })

  const openEdit = (resident: ResidentProfileDto) => {
    setEditFor(resident)
    setFirstName(resident.firstName)
    setLastName(resident.lastName)
    setPhoneNumber(resident.phoneNumber ?? '')
  }

  const submitEdit = (event: FormEvent) =>
    run(async () => {
      event.preventDefault()
      if (!editFor) return 'Nothing to save.'
      await api.updateResident(editFor.id, {
        firstName,
        lastName,
        phoneNumber: phoneNumber || undefined,
      })
      setEditFor(null)
      return 'Resident updated.'
    })

  const submitMoveOut = (event: FormEvent) =>
    run(async () => {
      event.preventDefault()
      if (!moveOutFor) return 'Nothing to do.'
      await api.moveOut(moveOutFor.id, moveOutDate ? new Date(`${moveOutDate}T00:00:00`).toISOString() : undefined)
      setMoveOutFor(null)
      setMoveOutDate('')
      return 'Move-out recorded. Occupancy history is preserved.'
    })

  const deactivate = (resident: ResidentProfileDto) =>
    run(async () => {
      if (!window.confirm(`Deactivate ${resident.firstName} ${resident.lastName}? History is preserved.`)) {
        return 'No change.'
      }
      await api.deactivateResident(resident.id)
      return 'Resident deactivated.'
    })

  return (
    <div className="container">
      <h2>Residents</h2>
      {error && <div className="error">{error}</div>}
      {notice && <div className="hint">{notice}</div>}

      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault()
          void load(search).catch((err) => setError((err as Error).message))
        }}
      >
        <label className="field">
          <span>Search</span>
          <input
            placeholder="Name or email"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <div>
          <button type="submit" disabled={busy}>Search</button>
        </div>
      </form>

      {loaded && residents.length === 0 && <div className="panel muted">No residents match this search.</div>}

      {residents.map((resident) => (
        <article key={resident.id} className="panel">
          <div className="row space-between">
            <div>
              <strong>
                {resident.firstName} {resident.lastName}
              </strong>
              <div className="muted">
                {resident.email}
                {resident.phoneNumber ? ` · ${resident.phoneNumber}` : ''}
              </div>
            </div>
            <div className="row">
              <span className={`pill pill--${resident.currentUnitNumber ? 'info' : 'muted'}`}>
                {resident.currentUnitNumber ? `Unit ${resident.currentUnitNumber}` : 'Unassigned'}
              </span>
              <span className={`pill pill--${resident.isActive ? 'ok' : 'muted'}`}>
                {resident.isActive ? 'Active' : 'Inactive'}
              </span>
            </div>
          </div>

          <div className="row" style={{ marginTop: 10 }}>
            <button type="button" className="ghost" disabled={busy} onClick={() => openEdit(resident)}>Edit</button>
            <button type="button" className="ghost" disabled={busy} onClick={() => void openAssign(resident)}>Assign unit</button>
            {resident.currentUnitNumber && resident.isActive && (
              <button type="button" className="ghost" disabled={busy} onClick={() => setMoveOutFor(resident)}>Move out</button>
            )}
            {resident.isActive && (
              <button type="button" className="ghost danger" disabled={busy} onClick={() => void deactivate(resident)}>
                Deactivate
              </button>
            )}
          </div>
        </article>
      ))}

      {assignFor && (
        <div className="modal">
          <form onSubmit={submitAssign}>
            <h3>
              Assign unit to {assignFor.firstName} {assignFor.lastName}
            </h3>
            <label className="field">
              <span>Property</span>
              <select value={selProperty} onChange={(e) => void onSelectProperty(e.target.value)} required>
                <option value="">Select property</option>
                {properties.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Building</span>
              <select
                value={selBuilding}
                onChange={(e) => void onSelectBuilding(e.target.value)}
                required
                disabled={buildings.length === 0}
              >
                <option value="">Select building</option>
                {buildings.map((b) => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Unit</span>
              <select value={selUnit} onChange={(e) => setSelUnit(e.target.value)} required disabled={units.length === 0}>
                <option value="">Select unit</option>
                {units.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.unitNumber} ({u.isOccupied ? 'occupied' : 'vacant'})
                  </option>
                ))}
              </select>
            </label>
            <div className="row">
              <button type="submit" disabled={busy}>Assign</button>
              <button type="button" className="ghost" onClick={() => setAssignFor(null)}>Cancel</button>
            </div>
          </form>
        </div>
      )}

      {editFor && (
        <div className="modal">
          <form onSubmit={submitEdit}>
            <h3>Edit {editFor.firstName} {editFor.lastName}</h3>
            <label className="field">
              <span>First name</span>
              <input value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
            </label>
            <label className="field">
              <span>Last name</span>
              <input value={lastName} onChange={(e) => setLastName(e.target.value)} required />
            </label>
            <label className="field">
              <span>Phone number</span>
              <input value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} />
            </label>
            <p className="muted">Email is managed by the account and is not editable here.</p>
            <div className="row">
              <button type="submit" disabled={busy}>Save</button>
              <button type="button" className="ghost" onClick={() => setEditFor(null)}>Cancel</button>
            </div>
          </form>
        </div>
      )}

      {moveOutFor && (
        <div className="modal">
          <form onSubmit={submitMoveOut}>
            <h3>Move out {moveOutFor.firstName} {moveOutFor.lastName}</h3>
            <p className="muted">Leaves the occupancy history intact and frees the unit.</p>
            <label className="field">
              <span>Move-out date (optional)</span>
              <input type="date" value={moveOutDate} onChange={(e) => setMoveOutDate(e.target.value)} />
            </label>
            <div className="row">
              <button type="submit" disabled={busy}>Record move-out</button>
              <button type="button" className="ghost" onClick={() => setMoveOutFor(null)}>Cancel</button>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}

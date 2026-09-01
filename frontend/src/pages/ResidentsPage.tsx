import { useEffect, useState, type FormEvent } from 'react'
import { api, type BuildingDto, type PropertyDto, type ResidentProfileDto, type UnitDto } from '../api/client'

export default function ResidentsPage() {
  const [residents, setResidents] = useState<ResidentProfileDto[]>([])
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [assignFor, setAssignFor] = useState<ResidentProfileDto | null>(null)

  // cascading unit picker
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [buildings, setBuildings] = useState<BuildingDto[]>([])
  const [units, setUnits] = useState<UnitDto[]>([])
  const [selProperty, setSelProperty] = useState('')
  const [selBuilding, setSelBuilding] = useState('')
  const [selUnit, setSelUnit] = useState('')

  const load = async (term?: string) => {
    setResidents(await api.getResidents(term))
  }

  useEffect(() => {
    load().catch((e) => setError(e.message))
  }, [])

  const openAssign = async (resident: ResidentProfileDto) => {
    setAssignFor(resident)
    setSelProperty('')
    setSelBuilding('')
    setSelUnit('')
    setBuildings([])
    setUnits([])
    setProperties(await api.getProperties())
  }

  const onSelectProperty = async (propertyId: string) => {
    setSelProperty(propertyId)
    setSelBuilding('')
    setSelUnit('')
    setBuildings(await api.getBuildings(propertyId))
    setUnits([])
  }

  const onSelectBuilding = async (buildingId: string) => {
    setSelBuilding(buildingId)
    setSelUnit('')
    setUnits(await api.getUnits(buildingId))
  }

  const submitAssign = async (e: FormEvent) => {
    e.preventDefault()
    if (!assignFor || !selUnit) return
    await api.assignUnit(assignFor.id, selUnit)
    setAssignFor(null)
    await load(search)
  }

  const deactivate = async (resident: ResidentProfileDto) => {
    if (!window.confirm(`Deactivate ${resident.firstName} ${resident.lastName}? History is preserved.`)) return
    await api.deactivateResident(resident.id)
    await load(search)
  }

  return (
    <div className="container">
      <h2>Residents</h2>
      {error && <div className="error">{error}</div>}
      <div className="row">
        <input
          placeholder="Search by name or email"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <button onClick={() => load(search)}>Search</button>
      </div>

      {residents.map((r) => (
        <div key={r.id} className="panel">
          <strong>{r.firstName} {r.lastName}</strong> — {r.email}
          {r.currentUnitNumber ? <span className="badge">Unit {r.currentUnitNumber}</span> : <span className="badge">Unassigned</span>}
          {!r.isActive && <span className="badge red">Inactive</span>}
          <div className="row">
            <button className="ghost" onClick={() => openAssign(r)}>Assign unit</button>
            {r.isActive && <button className="ghost danger" onClick={() => deactivate(r)}>Deactivate</button>}
          </div>
        </div>
      ))}

      {assignFor && (
        <div className="modal">
          <h3>Assign unit to {assignFor.firstName} {assignFor.lastName}</h3>
          <form onSubmit={submitAssign}>
            <select value={selProperty} onChange={(e) => onSelectProperty(e.target.value)} required>
              <option value="">Select property</option>
              {properties.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <select value={selBuilding} onChange={(e) => onSelectBuilding(e.target.value)} required disabled={!buildings.length}>
              <option value="">Select building</option>
              {buildings.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
            <select value={selUnit} onChange={(e) => setSelUnit(e.target.value)} required disabled={!units.length}>
              <option value="">Select unit</option>
              {units.map((u) => <option key={u.id} value={u.id}>{u.unitNumber} ({u.operationalStatus})</option>)}
            </select>
            <div className="row">
              <button type="submit">Assign</button>
              <button type="button" className="ghost" onClick={() => setAssignFor(null)}>Cancel</button>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}

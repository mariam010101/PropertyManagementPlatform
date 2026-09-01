import { useEffect, useState, type FormEvent } from 'react'
import { api, type BuildingDto, type PropertyDto, type UnitDto } from '../api/client'

const OPERATIONAL_STATUSES = ['Active', 'Inactive', 'UnderMaintenance']

export default function PropertiesPage() {
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [expandedProperty, setExpandedProperty] = useState<string | null>(null)
  const [buildings, setBuildings] = useState<Record<string, BuildingDto[]>>({})
  const [expandedBuilding, setExpandedBuilding] = useState<string | null>(null)
  const [units, setUnits] = useState<Record<string, UnitDto[]>>({})
  const [error, setError] = useState('')

  // add-property form
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [city, setCity] = useState('')
  const [managerId, setManagerId] = useState('')

  const reload = async () => {
    const [props, me] = await Promise.all([api.getProperties(), api.me().catch(() => null)])
    setProperties(props)
    setManagerId((prev) => prev || me?.userId || '')
  }

  useEffect(() => {
    reload().catch((e) => setError(e.message))
  }, [])

  const createProperty = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await api.createProperty({ name, address, city: city || undefined, managerUserId: managerId })
      setName('')
      setAddress('')
      setCity('')
      await reload()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const toggleProperty = async (propertyId: string) => {
    if (expandedProperty === propertyId) {
      setExpandedProperty(null)
      return
    }
    setExpandedProperty(propertyId)
    try {
      const list = await api.getBuildings(propertyId)
      setBuildings((prev) => ({ ...prev, [propertyId]: list }))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const toggleBuilding = async (buildingId: string) => {
    if (expandedBuilding === buildingId) {
      setExpandedBuilding(null)
      return
    }
    setExpandedBuilding(buildingId)
    try {
      const list = await api.getUnits(buildingId)
      setUnits((prev) => ({ ...prev, [buildingId]: list }))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const addBuilding = async (propertyId: string, e: FormEvent) => {
    e.preventDefault()
    const form = e.target as HTMLFormElement
    const formData = new FormData(form)
    await api.createBuilding(propertyId, {
      name: String(formData.get('name') ?? ''),
      floors: formData.get('floors') ? Number(formData.get('floors')) : undefined,
    })
    const list = await api.getBuildings(propertyId)
    setBuildings((prev) => ({ ...prev, [propertyId]: list }))
    form.reset()
  }

  const addUnit = async (buildingId: string, e: FormEvent) => {
    e.preventDefault()
    const form = e.target as HTMLFormElement
    const formData = new FormData(form)
    await api.createUnit(buildingId, {
      unitNumber: String(formData.get('unitNumber') ?? ''),
      unitType: String(formData.get('unitType') ?? ''),
      bedrooms: formData.get('bedrooms') ? Number(formData.get('bedrooms')) : undefined,
      bathrooms: formData.get('bathrooms') ? Number(formData.get('bathrooms')) : undefined,
      areaSqM: formData.get('areaSqM') ? Number(formData.get('areaSqM')) : undefined,
      operationalStatus: String(formData.get('operationalStatus') ?? 'Active'),
    })
    const list = await api.getUnits(buildingId)
    setUnits((prev) => ({ ...prev, [buildingId]: list }))
    form.reset()
  }

  return (
    <div className="container">
      <h2>Properties</h2>
      {error && <div className="error">{error}</div>}

      <form className="row" onSubmit={createProperty}>
        <input placeholder="Property name" value={name} onChange={(e) => setName(e.target.value)} required />
        <input placeholder="Address" value={address} onChange={(e) => setAddress(e.target.value)} required />
        <input placeholder="City" value={city} onChange={(e) => setCity(e.target.value)} />
        <input placeholder="Manager user id" value={managerId} onChange={(e) => setManagerId(e.target.value)} />
        <button type="submit">Add property</button>
      </form>

      {properties.map((p) => (
        <div key={p.id} className="panel">
          <button className="ghost" onClick={() => toggleProperty(p.id)}>
            <strong>{p.name}</strong> — {p.address}, {p.city} ({p.buildingCount} buildings, {p.unitCount} units)
          </button>
          {expandedProperty === p.id && (
            <div className="indent">
              {buildings[p.id]?.map((b) => (
                <div key={b.id} className="panel">
                  <button className="ghost" onClick={() => toggleBuilding(b.id)}>
                    {b.name} ({b.unitCount} units)
                  </button>
                  {expandedBuilding === b.id && (
                    <div className="indent">
                      <form className="row" onSubmit={(e) => addBuilding(p.id, e)}>
                        <input name="name" placeholder="Building name" required />
                        <input name="floors" type="number" placeholder="Floors" />
                        <button type="submit">Add building</button>
                      </form>
                      {units[b.id]?.map((u) => (
                        <div key={u.id} className="unit">
                          {u.unitNumber} · {u.unitType} · {u.operationalStatus}
                          {u.isOccupied ? ' · 🟢 occupied' : ' · vacant'}
                        </div>
                      ))}
                      <form className="row" onSubmit={(e) => addUnit(b.id, e)}>
                        <input name="unitNumber" placeholder="Unit number" required />
                        <input name="unitType" placeholder="Type" />
                        <input name="bedrooms" type="number" placeholder="Beds" />
                        <input name="bathrooms" type="number" placeholder="Baths" />
                        <input name="areaSqM" type="number" step="0.1" placeholder="Area m²" />
                        <select name="operationalStatus">
                          {OPERATIONAL_STATUSES.map((s) => (
                            <option key={s} value={s}>{s}</option>
                          ))}
                        </select>
                        <button type="submit">Add unit</button>
                      </form>
                    </div>
                  )}
                </div>
              ))}
              {!buildings[p.id]?.length && (
                <form className="row" onSubmit={(e) => addBuilding(p.id, e)}>
                  <input name="name" placeholder="Building name" required />
                  <input name="floors" type="number" placeholder="Floors" />
                  <button type="submit">Add building</button>
                </form>
              )}
            </div>
          )}
        </div>
      ))}
    </div>
  )
}

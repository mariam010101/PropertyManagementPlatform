import { useEffect, useState, type FormEvent } from 'react'
import { api, type BuildingDto, type PropertyDto, type UnitDto } from '../api/client'

const OPERATIONAL_STATUSES = ['Active', 'Inactive', 'UnderMaintenance'] as const

const statusLabel = (status: string) =>
  status === 'UnderMaintenance' ? 'Under maintenance' : status

const statusTone = (status: string) =>
  status === 'Active' ? 'ok' : status === 'Inactive' ? 'muted' : 'warn'

interface PropertyForm {
  name: string
  address: string
  city: string
  description: string
  managerUserId: string
}

interface BuildingForm {
  name: string
  address: string
  floors: string
}

interface UnitForm {
  unitNumber: string
  unitType: string
  bedrooms: string
  bathrooms: string
  areaSqM: string
  operationalStatus: string
  notes: string
}

/**
 * Property → building → unit management, including the previously missing edit flows
 * (GAP-021). Edits are inline so the hierarchy stays in view.
 */
export default function PropertiesPage() {
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [expandedProperty, setExpandedProperty] = useState<string | null>(null)
  const [buildings, setBuildings] = useState<Record<string, BuildingDto[]>>({})
  const [expandedBuilding, setExpandedBuilding] = useState<string | null>(null)
  const [units, setUnits] = useState<Record<string, UnitDto[]>>({})
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  // create-property form
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [city, setCity] = useState('')
  const [managerId, setManagerId] = useState('')

  // inline edit state
  const [editPropertyId, setEditPropertyId] = useState<string | null>(null)
  const [propertyForm, setPropertyForm] = useState<PropertyForm>({ name: '', address: '', city: '', description: '', managerUserId: '' })
  const [editBuildingId, setEditBuildingId] = useState<string | null>(null)
  const [buildingForm, setBuildingForm] = useState<BuildingForm>({ name: '', address: '', floors: '' })
  const [editUnitId, setEditUnitId] = useState<string | null>(null)
  const [unitForm, setUnitForm] = useState<UnitForm>({ unitNumber: '', unitType: '', bedrooms: '', bathrooms: '', areaSqM: '', operationalStatus: 'Active', notes: '' })

  const reload = async () => {
    const [props, me] = await Promise.all([api.getProperties(), api.me().catch(() => null)])
    setProperties(props)
    setManagerId((prev) => prev || me?.userId || '')
  }

  useEffect(() => {
    reload().catch((e) => setError((e as Error).message))
  }, [])

  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await action()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const createProperty = (event: FormEvent) =>
    run(async () => {
      event.preventDefault()
      await api.createProperty({ name, address, city: city || undefined, managerUserId: managerId })
      setName('')
      setAddress('')
      setCity('')
      setNotice('Property created.')
      await reload()
    })

  const toggleProperty = async (propertyId: string) => {
    if (expandedProperty === propertyId) {
      setExpandedProperty(null)
      return
    }
    setExpandedProperty(propertyId)
    try {
      setBuildings((prev) => ({ ...prev, [propertyId]: prev[propertyId] ?? [] }))
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

  const startEditProperty = (property: PropertyDto) => {
    setEditPropertyId(property.id)
    setPropertyForm({
      name: property.name,
      address: property.address,
      city: property.city ?? '',
      description: property.description ?? '',
      managerUserId: property.managerUserId,
    })
  }

  const saveProperty = (event: FormEvent, propertyId: string) =>
    run(async () => {
      event.preventDefault()
      await api.updateProperty(propertyId, {
        name: propertyForm.name,
        address: propertyForm.address,
        city: propertyForm.city || undefined,
        description: propertyForm.description || undefined,
        managerUserId: propertyForm.managerUserId,
      })
      setEditPropertyId(null)
      setNotice('Property updated.')
      await reload()
    })

  const startEditBuilding = (building: BuildingDto) => {
    setEditBuildingId(building.id)
    setBuildingForm({ name: building.name, address: building.address ?? '', floors: building.floors?.toString() ?? '' })
  }

  const saveBuilding = (event: FormEvent, propertyId: string, buildingId: string) =>
    run(async () => {
      event.preventDefault()
      await api.updateBuilding(propertyId, buildingId, {
        name: buildingForm.name,
        address: buildingForm.address || undefined,
        floors: buildingForm.floors ? Number(buildingForm.floors) : undefined,
      })
      setEditBuildingId(null)
      setNotice('Building updated.')
      const list = await api.getBuildings(propertyId)
      setBuildings((prev) => ({ ...prev, [propertyId]: list }))
    })

  const startEditUnit = (unit: UnitDto) => {
    setEditUnitId(unit.id)
    setUnitForm({
      unitNumber: unit.unitNumber,
      unitType: unit.unitType,
      bedrooms: unit.bedrooms?.toString() ?? '',
      bathrooms: unit.bathrooms?.toString() ?? '',
      areaSqM: unit.areaSqM?.toString() ?? '',
      operationalStatus: unit.operationalStatus,
      notes: unit.notes ?? '',
    })
  }

  const saveUnit = (event: FormEvent, buildingId: string, unitId: string) =>
    run(async () => {
      event.preventDefault()
      await api.updateUnit(unitId, {
        unitNumber: unitForm.unitNumber,
        unitType: unitForm.unitType,
        bedrooms: unitForm.bedrooms ? Number(unitForm.bedrooms) : undefined,
        bathrooms: unitForm.bathrooms ? Number(unitForm.bathrooms) : undefined,
        areaSqM: unitForm.areaSqM ? Number(unitForm.areaSqM) : undefined,
        operationalStatus: unitForm.operationalStatus,
        notes: unitForm.notes || undefined,
      })
      setEditUnitId(null)
      setNotice('Unit updated.')
      const list = await api.getUnits(buildingId)
      setUnits((prev) => ({ ...prev, [buildingId]: list }))
    })

  const addBuilding = (propertyId: string, event: FormEvent) =>
    run(async () => {
      event.preventDefault()
      const form = event.target as HTMLFormElement
      const data = new FormData(form)
      await api.createBuilding(propertyId, {
        name: String(data.get('name') ?? ''),
        floors: data.get('floors') ? Number(data.get('floors')) : undefined,
      })
      form.reset()
      setNotice('Building added.')
      const list = await api.getBuildings(propertyId)
      setBuildings((prev) => ({ ...prev, [propertyId]: list }))
    })

  const addUnit = (buildingId: string, event: FormEvent) =>
    run(async () => {
      event.preventDefault()
      const form = event.target as HTMLFormElement
      const data = new FormData(form)
      await api.createUnit(buildingId, {
        unitNumber: String(data.get('unitNumber') ?? ''),
        unitType: String(data.get('unitType') ?? ''),
        bedrooms: data.get('bedrooms') ? Number(data.get('bedrooms')) : undefined,
        bathrooms: data.get('bathrooms') ? Number(data.get('bathrooms')) : undefined,
        areaSqM: data.get('areaSqM') ? Number(data.get('areaSqM')) : undefined,
        operationalStatus: String(data.get('operationalStatus') ?? 'Active'),
      })
      form.reset()
      setNotice('Unit added.')
      const list = await api.getUnits(buildingId)
      setUnits((prev) => ({ ...prev, [buildingId]: list }))
    })

  return (
    <div className="container">
      <h2>Properties</h2>
      {error && <div className="error">{error}</div>}
      {notice && <div className="hint">{notice}</div>}

      <section className="panel">
        <h3>Add a property</h3>
        <form className="stack" onSubmit={createProperty}>
          <label className="field">
            <span>Name</span>
            <input value={name} onChange={(e) => setName(e.target.value)} required />
          </label>
          <label className="field">
            <span>Address</span>
            <input value={address} onChange={(e) => setAddress(e.target.value)} required />
          </label>
          <label className="field">
            <span>City</span>
            <input value={city} onChange={(e) => setCity(e.target.value)} />
          </label>
          <label className="field">
            <span>Manager user id</span>
            <input value={managerId} onChange={(e) => setManagerId(e.target.value)} required />
          </label>
          <div>
            <button type="submit" disabled={busy}>
              Add property
            </button>
          </div>
        </form>
      </section>

      {properties.length === 0 && <div className="panel muted">No properties in your scope yet.</div>}

      {properties.map((property) => (
        <article key={property.id} className="panel">
          <div className="row space-between">
            <div>
              <strong>{property.name}</strong>
              <div className="muted">
                {property.address}
                {property.city ? `, ${property.city}` : ''} · {property.buildingCount} building(s) ·{' '}
                {property.unitCount} unit(s)
              </div>
            </div>
            <div className="row">
              <button type="button" className="ghost" onClick={() => startEditProperty(property)}>
                Edit
              </button>
              <button type="button" className="ghost" onClick={() => void toggleProperty(property.id)}>
                {expandedProperty === property.id ? 'Hide buildings' : 'Buildings'}
              </button>
            </div>
          </div>

          {editPropertyId === property.id && (
            <form className="stack indent" onSubmit={(e) => saveProperty(e, property.id)}>
              <label className="field">
                <span>Name</span>
                <input value={propertyForm.name} onChange={(e) => setPropertyForm({ ...propertyForm, name: e.target.value })} required />
              </label>
              <label className="field">
                <span>Address</span>
                <input value={propertyForm.address} onChange={(e) => setPropertyForm({ ...propertyForm, address: e.target.value })} required />
              </label>
              <label className="field">
                <span>City</span>
                <input value={propertyForm.city} onChange={(e) => setPropertyForm({ ...propertyForm, city: e.target.value })} />
              </label>
              <label className="field">
                <span>Description</span>
                <textarea value={propertyForm.description} onChange={(e) => setPropertyForm({ ...propertyForm, description: e.target.value })} />
              </label>
              <label className="field">
                <span>Manager user id</span>
                <input value={propertyForm.managerUserId} onChange={(e) => setPropertyForm({ ...propertyForm, managerUserId: e.target.value })} required />
              </label>
              <div className="row">
                <button type="submit" disabled={busy}>Save</button>
                <button type="button" className="ghost" onClick={() => setEditPropertyId(null)}>Cancel</button>
              </div>
            </form>
          )}

          {expandedProperty === property.id && (
            <div className="indent">
              {(buildings[property.id] ?? []).map((building) => (
                <div key={building.id} className="panel">
                  <div className="row space-between">
                    <div>
                      <strong>{building.name}</strong>
                      <div className="muted">
                        {building.floors ? `${building.floors} floor(s) · ` : ''}
                        {building.unitCount} unit(s)
                      </div>
                    </div>
                    <div className="row">
                      <button type="button" className="ghost" onClick={() => startEditBuilding(building)}>Edit</button>
                      <button type="button" className="ghost" onClick={() => void toggleBuilding(building.id)}>
                        {expandedBuilding === building.id ? 'Hide units' : 'Units'}
                      </button>
                    </div>
                  </div>

                  {editBuildingId === building.id && (
                    <form className="stack indent" onSubmit={(e) => saveBuilding(e, property.id, building.id)}>
                      <label className="field">
                        <span>Name</span>
                        <input value={buildingForm.name} onChange={(e) => setBuildingForm({ ...buildingForm, name: e.target.value })} required />
                      </label>
                      <label className="field">
                        <span>Address</span>
                        <input value={buildingForm.address} onChange={(e) => setBuildingForm({ ...buildingForm, address: e.target.value })} />
                      </label>
                      <label className="field">
                        <span>Floors</span>
                        <input type="number" min="1" value={buildingForm.floors} onChange={(e) => setBuildingForm({ ...buildingForm, floors: e.target.value })} />
                      </label>
                      <div className="row">
                        <button type="submit" disabled={busy}>Save</button>
                        <button type="button" className="ghost" onClick={() => setEditBuildingId(null)}>Cancel</button>
                      </div>
                    </form>
                  )}

                  {expandedBuilding === building.id && (
                    <div className="indent">
                      {(units[building.id] ?? []).map((unit) => (
                        <div key={unit.id} className="unit">
                          <div className="row space-between">
                            <div className="row">
                              <strong>{unit.unitNumber}</strong>
                              <span className={`pill pill--${statusTone(unit.operationalStatus)}`}>
                                {statusLabel(unit.operationalStatus)}
                              </span>
                              <span className={`pill pill--${unit.isOccupied ? 'ok' : 'muted'}`}>
                                {unit.isOccupied ? 'Occupied' : 'Vacant'}
                              </span>
                              <span className="muted">
                                {unit.unitType}
                                {unit.bedrooms ? ` · ${unit.bedrooms} bed` : ''}
                                {unit.bathrooms ? ` · ${unit.bathrooms} bath` : ''}
                                {unit.areaSqM ? ` · ${unit.areaSqM} m²` : ''}
                              </span>
                            </div>
                            <button type="button" className="ghost" onClick={() => startEditUnit(unit)}>Edit</button>
                          </div>

                          {editUnitId === unit.id && (
                            <form className="stack" onSubmit={(e) => saveUnit(e, building.id, unit.id)}>
                              <label className="field">
                                <span>Unit number</span>
                                <input value={unitForm.unitNumber} onChange={(e) => setUnitForm({ ...unitForm, unitNumber: e.target.value })} required />
                              </label>
                              <label className="field">
                                <span>Type</span>
                                <input value={unitForm.unitType} onChange={(e) => setUnitForm({ ...unitForm, unitType: e.target.value })} />
                              </label>
                              <label className="field">
                                <span>Bedrooms</span>
                                <input type="number" min="0" value={unitForm.bedrooms} onChange={(e) => setUnitForm({ ...unitForm, bedrooms: e.target.value })} />
                              </label>
                              <label className="field">
                                <span>Bathrooms</span>
                                <input type="number" min="0" value={unitForm.bathrooms} onChange={(e) => setUnitForm({ ...unitForm, bathrooms: e.target.value })} />
                              </label>
                              <label className="field">
                                <span>Area (m²)</span>
                                <input type="number" step="0.1" min="0" value={unitForm.areaSqM} onChange={(e) => setUnitForm({ ...unitForm, areaSqM: e.target.value })} />
                              </label>
                              <label className="field">
                                <span>Operational status</span>
                                <select value={unitForm.operationalStatus} onChange={(e) => setUnitForm({ ...unitForm, operationalStatus: e.target.value })}>
                                  {OPERATIONAL_STATUSES.map((status) => (
                                    <option key={status} value={status}>{statusLabel(status)}</option>
                                  ))}
                                </select>
                              </label>
                              <label className="field">
                                <span>Notes</span>
                                <textarea value={unitForm.notes} onChange={(e) => setUnitForm({ ...unitForm, notes: e.target.value })} />
                              </label>
                              <div className="row">
                                <button type="submit" disabled={busy}>Save</button>
                                <button type="button" className="ghost" onClick={() => setEditUnitId(null)}>Cancel</button>
                              </div>
                            </form>
                          )}
                        </div>
                      ))}

                      <form className="stack" onSubmit={(e) => addUnit(building.id, e)}>
                        <h4>Add a unit</h4>
                        <label className="field">
                          <span>Unit number</span>
                          <input name="unitNumber" required />
                        </label>
                        <label className="field">
                          <span>Type</span>
                          <input name="unitType" />
                        </label>
                        <label className="field">
                          <span>Bedrooms</span>
                          <input name="bedrooms" type="number" min="0" />
                        </label>
                        <label className="field">
                          <span>Bathrooms</span>
                          <input name="bathrooms" type="number" min="0" />
                        </label>
                        <label className="field">
                          <span>Area (m²)</span>
                          <input name="areaSqM" type="number" step="0.1" min="0" />
                        </label>
                        <label className="field">
                          <span>Operational status</span>
                          <select name="operationalStatus" defaultValue="Active">
                            {OPERATIONAL_STATUSES.map((status) => (
                              <option key={status} value={status}>{statusLabel(status)}</option>
                            ))}
                          </select>
                        </label>
                        <div>
                          <button type="submit" disabled={busy}>Add unit</button>
                        </div>
                      </form>
                    </div>
                  )}
                </div>
              ))}

              <form className="stack" onSubmit={(e) => addBuilding(property.id, e)}>
                <h4>Add a building</h4>
                <label className="field">
                  <span>Name</span>
                  <input name="name" required />
                </label>
                <label className="field">
                  <span>Floors</span>
                  <input name="floors" type="number" min="1" />
                </label>
                <div>
                  <button type="submit" disabled={busy}>Add building</button>
                </div>
              </form>
            </div>
          )}
        </article>
      ))}
    </div>
  )
}

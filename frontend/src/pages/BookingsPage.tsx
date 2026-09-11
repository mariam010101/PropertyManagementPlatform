import { useEffect, useState, type FormEvent } from 'react'
import { api, type AvailabilityDto, type FacilityBookingDto, type FacilityDto, type PropertyDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

/**
 * Facility booking (BR-009). Residents check a facility's availability for a day
 * and reserve a slot (the server rejects overlaps); managers/admins configure
 * facilities and can cancel any booking. Availability is always read from the
 * API — the client never assumes a slot is free because it was free a moment ago.
 */

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'

const minutesToTime = (m: number) =>
  `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`

const timeToMinutes = (t: string) => {
  const [h, min] = t.split(':').map(Number)
  return h * 60 + min
}

/** Minutes from local midnight for an instant, relative to the day being viewed. */
const minutesIntoDay = (iso: string, day: string) =>
  (new Date(iso).getTime() - new Date(`${day}T00:00:00`).getTime()) / 60000

const formatWhen = (iso: string) => new Date(iso).toLocaleString()

const startOfWeekInput = () => {
  const d = new Date()
  d.setDate(d.getDate() - d.getDay())
  return d.toISOString().slice(0, 10)
}

export default function BookingsPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isResident = roles.includes(RESIDENT)
  const isManager = roles.includes(MANAGER) || roles.includes(ADMIN)

  const [facilities, setFacilities] = useState<FacilityDto[]>([])
  const [bookings, setBookings] = useState<FacilityBookingDto[]>([])
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [loaded, setLoaded] = useState(false)
  const [busy, setBusy] = useState(false)

  // create facility (managers)
  const [showCreate, setShowCreate] = useState(false)
  const [newPropertyId, setNewPropertyId] = useState('')
  const [newName, setNewName] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [newOpen, setNewOpen] = useState('08:00')
  const [newClose, setNewClose] = useState('22:00')
  const [newSlot, setNewSlot] = useState('60')
  const [newCancellation, setNewCancellation] = useState('2')

  // edit facility (managers)
  const [editId, setEditId] = useState<string | null>(null)
  const [editName, setEditName] = useState('')
  const [editDescription, setEditDescription] = useState('')
  const [editOpen, setEditOpen] = useState('08:00')
  const [editClose, setEditClose] = useState('22:00')
  const [editSlot, setEditSlot] = useState('60')
  const [editCancellation, setEditCancellation] = useState('2')
  const [editActive, setEditActive] = useState(true)

  // availability / booking (residents)
  const [availFacilityId, setAvailFacilityId] = useState<string | null>(null)
  const [availDay, setAvailDay] = useState(startOfWeekInput())
  const [avail, setAvail] = useState<AvailabilityDto | null>(null)
  const [slotStart, setSlotStart] = useState('')
  const [durationSlots, setDurationSlots] = useState(1)

  // cancellation reasons, keyed by booking id
  const [cancelReasons, setCancelReasons] = useState<Record<string, string>>({})

  const load = async () => {
    setFacilities(await api.getFacilities())
    setBookings(await api.getBookings(isResident ? { mineOnly: true } : undefined))
    setLoaded(true)
  }

  useEffect(() => {
    void (async () => {
      try {
        await load()
        if (isManager) {
          setProperties(await api.getProperties())
        }
      } catch (e) {
        setError((e as Error).message)
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isResident, isManager])

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

  const loadAvailability = (facilityId: string, day: string) =>
    run(async () => {
      const result = await api.getAvailability(facilityId, new Date(`${day}T12:00:00`).toISOString())
      setAvail(result)
      setAvailFacilityId(facilityId)
      setSlotStart('')
      setDurationSlots(1)
      return `Availability loaded for ${new Date(`${day}T00:00:00`).toLocaleDateString()}.`
    })

  const openAvailability = (facilityId: string) => {
    if (availFacilityId === facilityId) {
      setAvailFacilityId(null)
      setAvail(null)
      return
    }
    void loadAvailability(facilityId, availDay)
  }

  const changeDay = (facilityId: string, day: string) => {
    setAvailDay(day)
    void loadAvailability(facilityId, day)
  }

  const submitCreate = (e: FormEvent) =>
    run(async () => {
      e.preventDefault()
      if (!newPropertyId) throw new Error('Select a property.')
      await api.createFacility({
        propertyId: newPropertyId,
        name: newName,
        description: newDescription || undefined,
        openMinutes: timeToMinutes(newOpen),
        closeMinutes: timeToMinutes(newClose),
        slotMinutes: Number(newSlot),
        cancellationWindowHours: Number(newCancellation),
      })
      setShowCreate(false)
      setNewName('')
      setNewDescription('')
      await load()
      return `Facility “${newName}” created.`
    })

  const beginEdit = (f: FacilityDto) => {
    setEditId(f.id)
    setEditName(f.name)
    setEditDescription(f.description)
    setEditOpen(minutesToTime(f.openMinutes))
    setEditClose(minutesToTime(f.closeMinutes))
    setEditSlot(String(f.slotMinutes))
    setEditCancellation(String(f.cancellationWindowHours))
    setEditActive(f.isActive)
  }

  const saveFacility = (facilityId: string) =>
    run(async () => {
      await api.updateFacility(facilityId, {
        name: editName,
        description: editDescription,
        isActive: editActive,
        openMinutes: timeToMinutes(editOpen),
        closeMinutes: timeToMinutes(editClose),
        slotMinutes: Number(editSlot),
        cancellationWindowHours: Number(editCancellation),
      })
      setEditId(null)
      await load()
      return 'Facility updated.'
    })

  const book = (facilityId: string) =>
    run(async () => {
      if (!slotStart) throw new Error('Select a start time.')
      const slotMinutes = avail?.slotMinutes ?? 60
      const start = new Date(`${availDay}T${slotStart}:00`)
      const end = new Date(start.getTime() + durationSlots * slotMinutes * 60000)
      if (avail && timeToMinutes(slotStart) + durationSlots * slotMinutes > avail.closeMinutes) {
        throw new Error('The selected duration runs past closing time.')
      }
      await api.bookFacility(facilityId, start.toISOString(), end.toISOString())
      setSlotStart('')
      setDurationSlots(1)
      await loadAvailability(facilityId, availDay)
      await load()
      return 'Booking confirmed.'
    })

  const cancel = (bookingId: string) =>
    run(async () => {
      await api.cancelBooking(bookingId, cancelReasons[bookingId]?.trim() || undefined)
      setCancelReasons((c) => ({ ...c, [bookingId]: '' }))
      await load()
      return 'Booking cancelled.'
    })

  /** True when [start, end) overlaps a reserved slot on the displayed day. */
  const isTaken = (a: AvailabilityDto, start: number, end: number) =>
    a.bookings.some((b) => {
      const bs = minutesIntoDay(b.startAt, availDay)
      const be = minutesIntoDay(b.endAt, availDay)
      return bs < end && be > start
    })

  const slotStarts = (a: AvailabilityDto) => {
    const out: number[] = []
    for (let s = a.openMinutes; s + a.slotMinutes <= a.closeMinutes; s += a.slotMinutes) out.push(s)
    return out
  }

  const durationOptions = (a: AvailabilityDto) => {
    const max = Math.max(1, Math.floor((a.closeMinutes - a.openMinutes) / a.slotMinutes))
    return Array.from({ length: Math.min(max, 4) }, (_, i) => i + 1)
  }

  return (
    <div className="container">
      <div className="page-head">
        <div>
          <h2>Facility booking</h2>
          <p className="muted">
            {isResident
              ? 'Check a facility’s availability for a day and reserve a slot.'
              : 'Configure facilities, review reservations and cancel when needed.'}
          </p>
        </div>
        <div className="row">
          <button type="button" className="ghost" disabled={busy} onClick={() => void run(async () => { await load(); return 'Refreshed.' })}>
            Refresh
          </button>
          {isManager && (
            <button type="button" onClick={() => setShowCreate((s) => !s)}>
              {showCreate ? 'Close form' : 'Add facility'}
            </button>
          )}
        </div>
      </div>

      {error && <div className="error">{error}</div>}
      {notice && <div className="hint">{notice}</div>}

      {isManager && showCreate && (
        <form className="panel" onSubmit={submitCreate}>
          <h3>New facility</h3>
          <div className="row">
            <label className="field">
              <span>Property</span>
              <select value={newPropertyId} onChange={(e) => setNewPropertyId(e.target.value)} required>
                <option value="">Select property</option>
                {properties.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Name</span>
              <input value={newName} onChange={(e) => setNewName(e.target.value)} required maxLength={150} />
            </label>
          </div>
          <label className="field">
            <span>Description</span>
            <textarea value={newDescription} onChange={(e) => setNewDescription(e.target.value)} maxLength={1000} />
          </label>
          <div className="row">
            <label className="field">
              <span>Opens</span>
              <input type="time" value={newOpen} onChange={(e) => setNewOpen(e.target.value)} required />
            </label>
            <label className="field">
              <span>Closes</span>
              <input type="time" value={newClose} onChange={(e) => setNewClose(e.target.value)} required />
            </label>
            <label className="field">
              <span>Slot length (min)</span>
              <input type="number" min="15" step="15" value={newSlot} onChange={(e) => setNewSlot(e.target.value)} required />
            </label>
            <label className="field">
              <span>Free cancellation (hours)</span>
              <input type="number" min="0" value={newCancellation} onChange={(e) => setNewCancellation(e.target.value)} required />
            </label>
          </div>
          <button type="submit" disabled={busy}>
            Create facility
          </button>
        </form>
      )}

      <h3>Facilities</h3>
      {!loaded && <div className="state state--loading">Loading facilities…</div>}
      {loaded && facilities.length === 0 && <div className="state state--empty">No facilities are available.</div>}

      {facilities.map((f) => {
        const showing = availFacilityId === f.id
        const day = availDay
        return (
          <article key={f.id} className="panel">
            <div className="row space-between">
              <div>
                <strong>{f.name}</strong>
                <div className="muted request__meta">
                  {f.propertyName} · {minutesToTime(f.openMinutes)}–{minutesToTime(f.closeMinutes)} ·{' '}
                  {f.slotMinutes} min slots · cancel {f.cancellationWindowHours}h before
                </div>
              </div>
              <span className={`pill pill--${f.isActive ? 'ok' : 'muted'}`}>{f.isActive ? 'Open' : 'Closed'}</span>
            </div>

            {f.description && <p className="muted">{f.description}</p>}

            <div className="row" style={{ marginTop: 10 }}>
              <button type="button" className="ghost" disabled={busy} onClick={() => openAvailability(f.id)}>
                {showing ? 'Hide availability' : 'Check availability'}
              </button>
              {isManager && editId !== f.id && (
                <button type="button" className="ghost" disabled={busy} onClick={() => beginEdit(f)}>
                  Edit facility
                </button>
              )}
            </div>

            {isManager && editId === f.id && (
              <div className="indent" style={{ marginTop: 12 }}>
                <h4>Edit facility</h4>
                <div className="row">
                  <label className="field">
                    <span>Name</span>
                    <input value={editName} onChange={(e) => setEditName(e.target.value)} maxLength={150} />
                  </label>
                  <label className="field">
                    <span>Opens</span>
                    <input type="time" value={editOpen} onChange={(e) => setEditOpen(e.target.value)} />
                  </label>
                  <label className="field">
                    <span>Closes</span>
                    <input type="time" value={editClose} onChange={(e) => setEditClose(e.target.value)} />
                  </label>
                </div>
                <div className="row">
                  <label className="field">
                    <span>Slot length (min)</span>
                    <input type="number" min="15" step="15" value={editSlot} onChange={(e) => setEditSlot(e.target.value)} />
                  </label>
                  <label className="field">
                    <span>Free cancellation (hours)</span>
                    <input type="number" min="0" value={editCancellation} onChange={(e) => setEditCancellation(e.target.value)} />
                  </label>
                  <label className="check field" style={{ alignSelf: 'end' }}>
                    <input type="checkbox" checked={editActive} onChange={(e) => setEditActive(e.target.checked)} />
                    Available for booking
                  </label>
                </div>
                <label className="field">
                  <span>Description</span>
                  <textarea value={editDescription} onChange={(e) => setEditDescription(e.target.value)} maxLength={1000} />
                </label>
                <div className="row">
                  <button type="button" disabled={busy} onClick={() => void saveFacility(f.id)}>
                    Save facility
                  </button>
                  <button type="button" className="ghost" disabled={busy} onClick={() => setEditId(null)}>
                    Cancel
                  </button>
                </div>
              </div>
            )}

            {showing && (
              <div className="indent" style={{ marginTop: 12 }}>
                <div className="row">
                  <label className="field" style={{ maxWidth: 220 }}>
                    <span>Day</span>
                    <input type="date" value={day} disabled={busy} onChange={(e) => changeDay(f.id, e.target.value)} />
                  </label>
                  <p className="muted">Times are shown in your local time zone.</p>
                </div>

                {!avail && <div className="state state--loading">Loading availability…</div>}

                {avail && (
                  <>
                    <h4>Slots</h4>
                    {slotStarts(avail).length === 0 ? (
                      <p className="muted">This facility has no bookable slots configured.</p>
                    ) : (
                      <ul className="muted">
                        {slotStarts(avail).map((s) => {
                          const taken = isTaken(avail, s, s + avail.slotMinutes)
                          return (
                            <li key={s}>
                              {minutesToTime(s)} – {minutesToTime(s + avail.slotMinutes)}{' '}
                              <span className={`pill pill--${taken ? 'danger' : 'ok'}`}>{taken ? 'Booked' : 'Free'}</span>
                            </li>
                          )
                        })}
                      </ul>
                    )}

                    {isResident && f.isActive && (
                      <>
                        <h4>Reserve</h4>
                        <div className="row">
                          <label className="field" style={{ maxWidth: 200 }}>
                            <span>Start time</span>
                            <select value={slotStart} onChange={(e) => setSlotStart(e.target.value)}>
                              <option value="">Select…</option>
                              {slotStarts(avail)
                                .filter((s) => !isTaken(avail, s, s + avail.slotMinutes))
                                .map((s) => (
                                  <option key={s} value={minutesToTime(s)}>
                                    {minutesToTime(s)}
                                  </option>
                                ))}
                            </select>
                          </label>
                          <label className="field" style={{ maxWidth: 200 }}>
                            <span>Duration</span>
                            <select value={durationSlots} onChange={(e) => setDurationSlots(Number(e.target.value))}>
                              {durationOptions(avail).map((n) => (
                                <option key={n} value={n}>
                                  {n} slot{n === 1 ? '' : 's'} ({n * avail.slotMinutes} min)
                                </option>
                              ))}
                            </select>
                          </label>
                          <button type="button" disabled={busy || !slotStart} onClick={() => void book(f.id)}>
                            Book
                          </button>
                        </div>
                      </>
                    )}
                  </>
                )}
              </div>
            )}
          </article>
        )
      })}

      <h3>{isResident ? 'My bookings' : 'All bookings'}</h3>
      {loaded && bookings.length === 0 && <div className="state state--empty">No bookings yet.</div>}
      {bookings.map((b) => (
        <article key={b.id} className="panel">
          <div className="row space-between">
            <div>
              <strong>{b.facilityName}</strong>
              <div className="muted request__meta">
                {b.bookedByName} · {formatWhen(b.startAt)} → {formatWhen(b.endAt)} · {b.bookingReference}
              </div>
            </div>
            <span className={`pill pill--${b.status === 'Reserved' ? 'ok' : b.status === 'Cancelled' ? 'muted' : 'info'}`}>
              {b.status}
            </span>
          </div>

          {b.cancellationReason && <p className="muted">Cancelled: {b.cancellationReason}</p>}

          {b.status === 'Reserved' && (isResident || isManager) && (
            <div className="row" style={{ marginTop: 10 }}>
              <label className="field" style={{ maxWidth: 280 }}>
                <span>Reason (optional)</span>
                <input
                  value={cancelReasons[b.id] ?? ''}
                  onChange={(e) => setCancelReasons((c) => ({ ...c, [b.id]: e.target.value }))}
                  placeholder={isResident ? 'Why are you cancelling?' : 'Reason recorded for the resident'}
                />
              </label>
              <button type="button" className="ghost danger" disabled={busy} onClick={() => void cancel(b.id)}>
                Cancel booking
              </button>
            </div>
          )}
        </article>
      ))}
    </div>
  )
}

import { useEffect, useState, type FormEvent } from 'react'
import { api, type FacilityBookingDto, type FacilityDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'

export default function BookingsPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isManager = roles.includes(ADMIN) || roles.includes(MANAGER)
  const isResident = roles.includes('Resident')

  const [facilities, setFacilities] = useState<FacilityDto[]>([])
  const [bookings, setBookings] = useState<FacilityBookingDto[]>([])
  const [error, setError] = useState('')

  // booking form per facility
  const [facilityId, setFacilityId] = useState('')
  const [day, setDay] = useState('')
  const [hour, setHour] = useState('10:00')
  const [duration, setDuration] = useState(60)

  const load = async () => {
    try {
      setFacilities(await api.getFacilities())
      setBookings(await api.getBookings(isResident))
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const book = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      const start = new Date(`${day}T${hour}:00`)
      const end = new Date(start.getTime() + duration * 60000)
      await api.bookFacility(facilityId, start.toISOString(), end.toISOString())
      await load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const cancel = async (id: string) => {
    setError('')
    try {
      await api.cancelBooking(id, 'Cancelled by user')
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const fmtTime = (iso: string) => new Date(iso).toLocaleString()

  return (
    <div className="container">
      <h2>Facility Booking</h2>
      {error && <div className="error">{error}</div>}

      <h3>Available facilities</h3>
      {facilities.map((f) => (
        <div key={f.id} className="panel">
          <div className="row space-between">
            <strong>{f.name}</strong>
            <span className="badge">{f.isActive ? 'Open' : 'Closed'}</span>
          </div>
          <p className="muted">{f.description}</p>
          <div className="muted">
            {Math.floor(f.openMinutes / 60)}:{String(f.openMinutes % 60).padStart(2, '0')} –{' '}
            {Math.floor(f.closeMinutes / 60)}:{String(f.closeMinutes % 60).padStart(2, '0')} A· {f.slotMinutes} min slots
          </div>
          {isResident && f.isActive && (
            <form className="row" onSubmit={book}>
              <input type="hidden" value={f.id} />
              <input type="date" value={facilityId === f.id ? day : day} onChange={(e) => { setFacilityId(f.id); setDay(e.target.value) }} required />
              <input type="time" value={hour} onChange={(e) => { setFacilityId(f.id); setHour(e.target.value) }} required />
              <select value={duration} onChange={(e) => { setFacilityId(f.id); setDuration(Number(e.target.value)) }}>
                <option value={60}>1 hour</option>
                <option value={120}>2 hours</option>
              </select>
              <button type="submit">Book</button>
            </form>
          )}
        </div>
      ))}

      <h3>{isResident ? 'My bookings' : 'All bookings'}</h3>
      {bookings.length === 0 && <p className="muted">No bookings.</p>}
      {bookings.map((b) => (
        <div key={b.id} className="panel">
          <div className="row space-between">
            <strong>{b.facilityName}</strong>
            <span className="badge">{b.status}</span>
          </div>
          <div className="muted">
            {b.bookedByName} A· {fmtTime(b.startAt)} → {fmtTime(b.endAt)} A· {b.bookingReference}
          </div>
          {isResident && b.status === 'Reserved' && <button onClick={() => cancel(b.id)}>Cancel</button>}
        </div>
      ))}
    </div>
  )
}

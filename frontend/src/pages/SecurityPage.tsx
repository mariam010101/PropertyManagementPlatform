import { useEffect, useState, type FormEvent } from 'react'
import { api, type AccessGrantDto, type VisitorDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function SecurityPage() {
  const { user } = useAuth()
  const isStaff = user?.roles.some((r) => r === 'Administrator' || r === 'PropertyManager')

  const [visitors, setVisitors] = useState<VisitorDto[]>([])
  const [grants, setGrants] = useState<AccessGrantDto[]>([])
  const [error, setError] = useState('')

  // register form
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [phone, setPhone] = useState('')
  const [unitNumber, setUnitNumber] = useState('')
  const [propertyId, setPropertyId] = useState('')
  const [properties, setProperties] = useState<{ id: string; name: string }[]>([])

  const load = async () => {
    try {
      setVisitors(await api.getVisitors())
      setGrants(await api.getAccessLog())
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    ;(async () => {
      try {
        if (isStaff) setProperties(await api.getProperties())
        await load()
      } catch (e) {
        setError((e as Error).message)
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const register = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await api.registerVisitor({ firstName, lastName, propertyId, phoneNumber: phone, unitNumber })
      setFirstName('')
      setLastName('')
      setPhone('')
      setUnitNumber('')
      await load()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  const checkIn = async (id: string) => {
    setError('')
    try {
      await api.checkInVisitor(id)
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const checkOut = async (id: string) => {
    setError('')
    try {
      await api.checkOutVisitor(id)
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const revoke = async (id: string) => {
    setError('')
    try {
      await api.revokeAccess(id)
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <div className="container">
      <h2>Security & Visitors</h2>
      {error && <div className="error">{error}</div>}

      <form className="panel" onSubmit={register}>
        <h3>Register visitor</h3>
        <label>
          Property
          <select value={propertyId} onChange={(e) => setPropertyId(e.target.value)} required>
            <option value="">Select property</option>
            {properties.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </label>
        <label>First name <input value={firstName} onChange={(e) => setFirstName(e.target.value)} required /></label>
        <label>Last name <input value={lastName} onChange={(e) => setLastName(e.target.value)} required /></label>
        <label>Phone <input value={phone} onChange={(e) => setPhone(e.target.value)} /></label>
        <label>Unit <input value={unitNumber} onChange={(e) => setUnitNumber(e.target.value)} /></label>
        <button type="submit">Register</button>
      </form>

      <h3>Visitors</h3>
      {visitors.length === 0 && <p className="muted">No visitors registered.</p>}
      {visitors.map((v) => (
        <div key={v.id} className="panel">
          <div className="row space-between">
            <strong>{v.fullName}</strong>
            <span
              className={`pill pill--${
                v.status === 'CheckedIn'
                  ? 'ok'
                  : v.status === 'CheckedOut'
                    ? 'muted'
                    : v.status === 'Cancelled'
                      ? 'danger'
                      : 'info'
              }`}
            >
              {v.status}
            </span>
          </div>
          <div className="muted">
            {v.propertyName} A· {v.unitNumber ?? 'n/a'} A· {v.phoneNumber ?? 'no phone'}
          </div>
          <div className="muted">
            {v.checkInAt ? `In ${new Date(v.checkInAt).toLocaleString()}` : ''}
            {v.checkOutAt ? ` A· Out ${new Date(v.checkOutAt).toLocaleString()}` : ''}
          </div>
          {v.status === 'Registered' && <button onClick={() => checkIn(v.id)}>Check in</button>}
          {v.status === 'CheckedIn' && <button onClick={() => checkOut(v.id)}>Check out</button>}
        </div>
      ))}

      <h3>Access log</h3>
      {grants.length === 0 && <p className="muted">No access grants.</p>}
      {grants.map((g) => (
        <div key={g.id} className="panel">
          <div className="row space-between">
            <strong>{g.subjectName} → {g.targetName} ({g.targetType})</strong>
            <span className={`pill pill--${g.isActive ? 'ok' : 'muted'}`}>{g.isActive ? 'Active' : 'Revoked'}</span>
          </div>
          <div className="muted">
            {g.propertyName} A· Granted {new Date(g.grantedAt).toLocaleString()}
            {g.expiresAt ? ` A· Expires ${new Date(g.expiresAt).toLocaleString()}` : ''}
          </div>
          {g.isActive && <button onClick={() => revoke(g.id)}>Revoke</button>}
        </div>
      ))}
    </div>
  )
}

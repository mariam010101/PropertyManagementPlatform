import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const TECHNICIAN = 'Technician'

export default function DashboardPage() {
  const { user, logout } = useAuth()
  const roles = user?.roles ?? []
  const isManager = roles.includes(MANAGER) || roles.includes(ADMIN)
  const isResident = roles.includes(RESIDENT)
  const isTech = roles.includes(TECHNICIAN)

  return (
    <div className="container">
      <header className="topbar">
        <div>
          <strong>PMP</strong> — {user?.firstName} {user?.lastName}
          <span className="badge">{roles.join(', ')}</span>
        </div>
        <button className="ghost" onClick={() => logout()}>Sign out</button>
      </header>

      <h2>Welcome to your dashboard</h2>
      <div className="grid">
        {isManager && (
          <>
            <Link className="card" to="/properties">
              <h3>Properties</h3>
              <p>Manage properties, buildings and units.</p>
            </Link>
            <Link className="card" to="/residents">
              <h3>Residents</h3>
              <p>Search residents and assign units.</p>
            </Link>
          </>
        )}
        {(isResident || isManager || isTech) && (
          <Link className="card" to="/maintenance">
            <h3>Maintenance</h3>
            <p>{isResident ? 'Submit and track your maintenance requests.' : 'Manage maintenance requests.'}</p>
          </Link>
        )}
      </div>
    </div>
  )
}

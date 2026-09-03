import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const TECHNICIAN = 'Technician'
const ACCOUNTANT = 'Accountant'

export default function DashboardPage() {
  const { user, logout } = useAuth()
  const roles = user?.roles ?? []
  const isAdmin = roles.includes(ADMIN)
  const isManager = roles.includes(MANAGER) || isAdmin
  const isResident = roles.includes(RESIDENT)
  const isTech = roles.includes(TECHNICIAN)
  const isAccountant = roles.includes(ACCOUNTANT)

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
            <Link className="card" to="/leases">
              <h3>Leases</h3>
              <p>Create and manage lease agreements.</p>
            </Link>
          </>
        )}
        {(isResident || isManager) && (
          <>
            <Link className="card" to="/payments">
              <h3>Payments</h3>
              <p>{isResident ? 'View your balance and pay rent.' : 'Monitor invoices and payments.'}</p>
            </Link>
            <Link className="card" to="/bookings">
              <h3>Facility Booking</h3>
              <p>{isResident ? 'Reserve shared facilities.' : 'Configure facilities and view bookings.'}</p>
            </Link>
          </>
        )}
        {isAccountant && (
          <Link className="card" to="/payments">
            <h3>Financial Reports</h3>
            <p>View financial reports and reconciliation.</p>
          </Link>
        )}
        {isResident && (
          <Link className="card" to="/leases">
            <h3>My Lease</h3>
            <p>View your lease agreement details.</p>
          </Link>
        )}
        {isAdmin && (
          <Link className="card" to="/users">
            <h3>Users & Roles</h3>
            <p>Manage user accounts, assign roles, deactivate users.</p>
          </Link>
        )}
        {(isResident || isManager || isTech) && (
          <Link className="card" to="/maintenance">
            <h3>Maintenance</h3>
            <p>{isResident ? 'Submit and track your maintenance requests.' : 'Manage maintenance requests.'}</p>
          </Link>
        )}
        {(isManager || isResident) && (
          <Link className="card" to="/communication">
            <h3>Communication</h3>
            <p>{isResident ? 'Announcements and notifications.' : 'Publish announcements and notifications.'}</p>
          </Link>
        )}
        {isManager && (
          <Link className="card" to="/security">
            <h3>Security & Visitors</h3>
            <p>Register visitors and manage access.</p>
          </Link>
        )}
      </div>
    </div>
  )
}

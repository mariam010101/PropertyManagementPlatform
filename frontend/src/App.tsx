import { Navigate, Route, Routes } from 'react-router-dom'
import ProtectedRoute from './components/ProtectedRoute'
import AdminUsersPage from './pages/AdminUsersPage'
import BookingsPage from './pages/BookingsPage'
import CommunicationPage from './pages/CommunicationPage'
import DashboardPage from './pages/DashboardPage'
import LeasesPage from './pages/LeasesPage'
import LoginPage from './pages/LoginPage'
import MaintenancePage from './pages/MaintenancePage'
import PaymentsPage from './pages/PaymentsPage'
import PropertiesPage from './pages/PropertiesPage'
import RegisterPage from './pages/RegisterPage'
import ResidentsPage from './pages/ResidentsPage'
import SecurityPage from './pages/SecurityPage'

const ADMIN = ['Administrator']
const MANAGER = ['Administrator', 'PropertyManager']
const STAFF = ['Resident', 'PropertyManager', 'Technician', 'Administrator']
const FINANCIAL = ['Accountant', 'PropertyManager', 'Administrator']
const RESIDENT = ['Resident']
const ANY = ['Resident', 'PropertyManager', 'Technician', 'Accountant', 'Administrator']

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <DashboardPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/properties"
        element={
          <ProtectedRoute roles={MANAGER}>
            <PropertiesPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/residents"
        element={
          <ProtectedRoute roles={MANAGER}>
            <ResidentsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/users"
        element={
          <ProtectedRoute roles={ADMIN}>
            <AdminUsersPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/maintenance"
        element={
          <ProtectedRoute roles={STAFF}>
            <MaintenancePage />
          </ProtectedRoute>
        }
      />
      {/* post-MVP modules (BR-005..010) */}
      <Route
        path="/payments"
        element={
          <ProtectedRoute roles={[...FINANCIAL, ...RESIDENT]}>
            <PaymentsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/leases"
        element={
          <ProtectedRoute roles={ANY}>
            <LeasesPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/communication"
        element={
          <ProtectedRoute roles={ANY}>
            <CommunicationPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/bookings"
        element={
          <ProtectedRoute roles={[...MANAGER, ...RESIDENT]}>
            <BookingsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/security"
        element={
          <ProtectedRoute roles={MANAGER}>
            <SecurityPage />
          </ProtectedRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

import { Navigate, Route, Routes } from 'react-router-dom'
import ProtectedRoute from './components/ProtectedRoute'
import DashboardPage from './pages/DashboardPage'
import LoginPage from './pages/LoginPage'
import MaintenancePage from './pages/MaintenancePage'
import PropertiesPage from './pages/PropertiesPage'
import RegisterPage from './pages/RegisterPage'
import ResidentsPage from './pages/ResidentsPage'

const MANAGER = ['Administrator', 'PropertyManager']
const STAFF = ['Resident', 'PropertyManager', 'Technician', 'Administrator']

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
        path="/maintenance"
        element={
          <ProtectedRoute roles={STAFF}>
            <MaintenancePage />
          </ProtectedRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

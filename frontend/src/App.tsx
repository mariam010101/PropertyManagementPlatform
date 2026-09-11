import { Navigate, Route, Routes } from 'react-router-dom'
import AppLayout from './components/AppLayout'
import ProtectedRoute from './components/ProtectedRoute'
import ChangePasswordPage from './pages/ChangePasswordPage'
import ForbiddenPage from './pages/ForbiddenPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import ResetPasswordPage from './pages/ResetPasswordPage'
import { APP_ROUTES } from './navigation'

/**
 * Routing is generated from the single navigation manifest, so the sidebar and the
 * route guards can never disagree about who may reach a page.
 */
export default function App() {
  return (
    <Routes>
      {/* Authentication pages render outside the application shell. */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        {APP_ROUTES.map(({ path, roles, Component, index }) =>
          index ? (
            <Route
              key={path}
              index
              element={
                <ProtectedRoute roles={roles}>
                  <Component />
                </ProtectedRoute>
              }
            />
          ) : (
            <Route
              key={path}
              path={path}
              element={
                <ProtectedRoute roles={roles}>
                  <Component />
                </ProtectedRoute>
              }
            />
          ),
        )}

        <Route path="/forbidden" element={<ForbiddenPage />} />
        {/* Reached from the user menu, so intentionally absent from the nav manifest. */}
        <Route
          path="/account/password"
          element={
            <ProtectedRoute roles={['Resident', 'PropertyManager', 'Technician', 'Accountant', 'Administrator']}>
              <ChangePasswordPage />
            </ProtectedRoute>
          }
        />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

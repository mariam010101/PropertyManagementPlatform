import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'

export default function ProtectedRoute({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const { user, loading } = useAuth()

  if (loading) {
    return <div className="center">Loading…</div>
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  // Explain the restriction instead of silently bouncing to the dashboard (GAP-018).
  if (roles && !user.roles.some((r) => roles.includes(r))) {
    return <Navigate to="/forbidden" replace />
  }

  return <>{children}</>
}

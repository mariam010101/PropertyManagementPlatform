import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, clearTokens, getRefreshToken, setTokens, type RegisterRequest } from '../api/client'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
}

interface AuthContextValue {
  user: User | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (payload: RegisterRequest) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    ;(async () => {
      try {
        const me = await api.me()
        setUser({ id: me.userId, email: '', firstName: '', lastName: '', roles: me.roles })
      } catch {
        setUser(null)
      } finally {
        setLoading(false)
      }
    })()
  }, [])

  const login = async (email: string, password: string) => {
    const res = await api.login(email, password)
    setTokens(res.accessToken, res.refreshToken)
    setUser({ id: res.userId, email: res.email, firstName: res.firstName, lastName: res.lastName, roles: res.roles })
  }

  const register = async (payload: RegisterRequest) => {
    const res = await api.register(payload)
    // In dev, the confirmation token is returned so email verification can be
    // completed immediately; a real deployment would email it.
    if (res.emailConfirmationToken) {
      await api.confirmEmail(res.userId, res.emailConfirmationToken)
    }
    setTokens(res.accessToken, res.refreshToken)
    setUser({ id: res.userId, email: res.email, firstName: res.firstName, lastName: res.lastName, roles: res.roles })
  }

  const logout = async () => {
    const refresh = getRefreshToken()
    if (refresh) {
      await api.logout(refresh).catch(() => undefined)
    }
    clearTokens()
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}

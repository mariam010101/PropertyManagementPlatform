import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { expect } from 'vitest'
import App from '../App'
import { api } from '../api/client'
import { AuthProvider } from '../auth/AuthContext'

const manager = {
  userId: 'm1',
  email: 'manager@pmp.com',
  firstName: 'M',
  lastName: 'P',
  roles: ['PropertyManager'],
}

const resident = {
  userId: 'r1',
  email: 'resident@pmp.com',
  firstName: 'R',
  lastName: 'P',
  roles: ['Resident'],
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  localStorage.clear()
  vi.spyOn(api, 'me').mockResolvedValue(manager)
  vi.spyOn(api, 'getProperties').mockResolvedValue([])
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('MVP route guards', () => {
  it('redirects an unauthenticated user away from a protected MVP route', async () => {
    renderAt('/properties')

    expect(await screen.findByText('Sign in to your account')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Properties' })).not.toBeInTheDocument()
  })

  it('lets an authenticated PropertyManager open an allowed MVP route', async () => {
    localStorage.setItem('pmp_access_token', 'token')

    renderAt('/properties')

    expect(await screen.findByRole('heading', { name: 'Properties' })).toBeInTheDocument()
  })

  it('prevents an unauthorized Resident from opening the manager-only properties route', async () => {
    localStorage.setItem('pmp_access_token', 'token')
    vi.mocked(api.me).mockResolvedValue(resident)

    renderAt('/properties')

    expect(await screen.findByText(/don.t have access to this page/)).toBeInTheDocument()
  })

  it('renders a representative MVP data page with data from the API', async () => {
    localStorage.setItem('pmp_access_token', 'token')
    vi.mocked(api.getProperties).mockResolvedValue([
      {
        id: 'p1',
        name: 'Skyline Tower',
        address: '1 Main St',
        city: 'Yerevan',
        managerUserId: 'm1',
        buildingCount: 1,
        unitCount: 4,
      },
    ])

    renderAt('/properties')

    expect(await screen.findByRole('heading', { name: 'Properties' })).toBeInTheDocument()
    expect(await screen.findByText('Skyline Tower')).toBeInTheDocument()
  })
})

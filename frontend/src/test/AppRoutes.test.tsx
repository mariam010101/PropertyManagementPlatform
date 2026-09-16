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

const admin = {
  userId: 'a1',
  email: 'admin@pmp.com',
  firstName: 'A',
  lastName: 'D',
  roles: ['Administrator'],
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
  vi.spyOn(api, 'getResidents').mockResolvedValue([])
  vi.spyOn(api, 'getMaintenance').mockResolvedValue([])
  vi.spyOn(api, 'getUsers').mockResolvedValue([])
  vi.spyOn(api, 'getStaff').mockResolvedValue([])
  vi.spyOn(api, 'getUnreadCount').mockResolvedValue({ count: 0 })
  vi.spyOn(api, 'getFinancialReport').mockResolvedValue({
    totalCollected: 0,
    totalOutstanding: 0,
    completedPayments: 0,
    failedPayments: 0,
    openInvoices: 0,
    paidInvoices: 0,
    overdueInvoices: 0,
    overdueTotal: 0,
  })
  vi.spyOn(api, 'getOutstanding').mockResolvedValue([])
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

  it('renders the login page for unauthenticated visitors', async () => {
    renderAt('/login')

    expect(await screen.findByText('Sign in to your account')).toBeInTheDocument()
  })

  it('renders the register page for unauthenticated visitors', async () => {
    renderAt('/register')

    expect(await screen.findByText('Create a resident account')).toBeInTheDocument()
  })

  it('lets an authenticated PropertyManager open an allowed MVP route', async () => {
    localStorage.setItem('pmp_access_token', 'token')

    renderAt('/properties')

    expect(await screen.findByRole('heading', { name: 'Properties' })).toBeInTheDocument()
  })

  it('renders the dashboard for an authenticated user', async () => {
    localStorage.setItem('pmp_access_token', 'token')

    renderAt('/')

    expect(await screen.findByText(/Welcome back, M/)).toBeInTheDocument()
  })

  it('renders residents data from the API for a PropertyManager', async () => {
    localStorage.setItem('pmp_access_token', 'token')
    vi.mocked(api.getResidents).mockResolvedValue([
      {
        id: 'res1',
        userId: 'u1',
        firstName: 'Rita',
        lastName: 'P',
        email: 'rita@pmp.com',
        isActive: true,
        currentUnitNumber: 'A-101',
      },
    ])

    renderAt('/residents')

    expect(await screen.findByRole('heading', { name: 'Residents' })).toBeInTheDocument()
    expect(await screen.findByText('Rita P')).toBeInTheDocument()
  })

  it('prevents an unauthorized Resident from opening the residents route', async () => {
    localStorage.setItem('pmp_access_token', 'token')
    vi.mocked(api.me).mockResolvedValue(resident)

    renderAt('/residents')

    expect(await screen.findByText(/don.t have access to this page/)).toBeInTheDocument()
  })

  it('renders maintenance data from the API for a PropertyManager', async () => {
    localStorage.setItem('pmp_access_token', 'token')
    vi.mocked(api.getMaintenance).mockResolvedValue([
      {
        id: 'm1',
        title: 'Leaking kitchen tap',
        description: 'Water drip under the sink',
        priority: 'High',
        status: 'Submitted',
        requestedByResidentId: 'r1',
        unitId: 'u1',
        unitNumber: 'A-101',
        createdAt: '2026-01-01T00:00:00Z',
        attachmentCount: 0,
        attachments: [],
        history: [],
      },
    ])

    renderAt('/maintenance')

    expect(await screen.findByRole('heading', { name: 'Maintenance' })).toBeInTheDocument()
    expect(await screen.findByText('Leaking kitchen tap')).toBeInTheDocument()
  })

  it('renders admin users data from the API for an Administrator', async () => {
    localStorage.setItem('pmp_access_token', 'token')
    vi.mocked(api.me).mockResolvedValue(admin)
    vi.mocked(api.getUsers).mockResolvedValue([
      {
        id: 'u2',
        firstName: 'Anna',
        lastName: 'Manager',
        email: 'manager@pmp.com',
        isActive: true,
        roles: ['PropertyManager'],
        createdAt: '2026-01-01T00:00:00Z',
      },
    ])

    renderAt('/users')

    expect(await screen.findByRole('heading', { name: 'Users & Roles' })).toBeInTheDocument()
    expect(await screen.findByText(/manager@pmp.com/)).toBeInTheDocument()
  })

  it('prevents a PropertyManager from opening the admin users route', async () => {
    localStorage.setItem('pmp_access_token', 'token')

    renderAt('/users')

    expect(await screen.findByText(/don.t have access to this page/)).toBeInTheDocument()
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

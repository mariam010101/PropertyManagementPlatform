import type { ComponentType } from 'react'
import type { IconName } from './components/Icon'
import AdminUsersPage from './pages/AdminUsersPage'
import BookingsPage from './pages/BookingsPage'
import CommunicationPage from './pages/CommunicationPage'
import DashboardPage from './pages/DashboardPage'
import InvoicesPage from './pages/InvoicesPage'
import LeasesPage from './pages/LeasesPage'
import MaintenancePage from './pages/MaintenancePage'
import PaymentsPage from './pages/PaymentsPage'
import PropertiesPage from './pages/PropertiesPage'
import ResidentsPage from './pages/ResidentsPage'
import SecurityPage from './pages/SecurityPage'

/** Platform roles, as issued by the API. */
export const ROLE = {
  administrator: 'Administrator',
  manager: 'PropertyManager',
  resident: 'Resident',
  technician: 'Technician',
  accountant: 'Accountant',
} as const

/**
 * The single source of truth for who may see what. Both the sidebar and the route
 * guards read these groups, so navigation and access control cannot drift apart.
 */
export const ROLES = {
  any: Object.values(ROLE),
  administrator: [ROLE.administrator],
  manager: [ROLE.administrator, ROLE.manager],
  staff: [ROLE.resident, ROLE.manager, ROLE.technician, ROLE.administrator],
  financial: [ROLE.accountant, ROLE.manager, ROLE.administrator],
  payments: [ROLE.accountant, ROLE.manager, ROLE.administrator, ROLE.resident],
  residentOrManager: [ROLE.administrator, ROLE.manager, ROLE.resident],
} satisfies Record<string, string[]>

export interface AppRoute {
  /** Route path; also the navigation target. */
  path: string
  label: string
  icon: IconName
  /** Roles allowed to reach this route. */
  roles: string[]
  Component: ComponentType
  /** True for the index route rendered at `/`. */
  index?: boolean
}

/** Order here is the order shown in the sidebar. */
export const APP_ROUTES: AppRoute[] = [
  { path: '/', label: 'Dashboard', icon: 'dashboard', roles: ROLES.any, Component: DashboardPage, index: true },
  { path: '/properties', label: 'Properties', icon: 'building', roles: ROLES.manager, Component: PropertiesPage },
  { path: '/residents', label: 'Residents', icon: 'users', roles: ROLES.manager, Component: ResidentsPage },
  { path: '/leases', label: 'Leases', icon: 'document', roles: ROLES.any, Component: LeasesPage },
  { path: '/payments', label: 'Payments', icon: 'card', roles: ROLES.payments, Component: PaymentsPage },
  { path: '/invoices', label: 'Invoices', icon: 'document', roles: ROLES.payments, Component: InvoicesPage },
  { path: '/maintenance', label: 'Maintenance', icon: 'checklist', roles: ROLES.staff, Component: MaintenancePage },
  { path: '/bookings', label: 'Facility booking', icon: 'calendar', roles: ROLES.residentOrManager, Component: BookingsPage },
  { path: '/communication', label: 'Communication', icon: 'message', roles: ROLES.any, Component: CommunicationPage },
  { path: '/security', label: 'Security & visitors', icon: 'lock', roles: ROLES.manager, Component: SecurityPage },
  { path: '/users', label: 'Users & roles', icon: 'shield', roles: ROLES.administrator, Component: AdminUsersPage },
]

/** Routes reachable by the signed-in user, in sidebar order. */
export function navigationFor(userRoles: string[]): AppRoute[] {
  return APP_ROUTES.filter((route) => route.roles.some((role) => userRoles.includes(role)))
}

/** The manifest entry matching a pathname, if any. */
export function routeFor(pathname: string): AppRoute | undefined {
  if (pathname === '/') return APP_ROUTES.find((route) => route.index)
  return APP_ROUTES.find((route) => route.path !== '/' && pathname.startsWith(route.path))
}

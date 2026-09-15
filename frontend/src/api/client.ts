// Lightweight typed API client for the PMP backend.
// Assumes the Vite dev server proxies /api to the ASP.NET Core API.

export class ApiError extends Error {
  status: number

  constructor(message: string, status = 0) {
    super(message)
    this.status = status
  }
}

const ACCESS_KEY = 'pmp_access_token'
const REFRESH_KEY = 'pmp_refresh_token'

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_KEY)
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY)
}

export function setTokens(access: string, refresh: string): void {
  localStorage.setItem(ACCESS_KEY, access)
  localStorage.setItem(REFRESH_KEY, refresh)
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_KEY)
  localStorage.removeItem(REFRESH_KEY)
}

interface RequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = getAccessToken()
  // FormData uploads must let the browser set the multipart boundary, so the
  // JSON content type is only applied to ordinary JSON payloads.
  const isForm = options.body instanceof FormData
  const headers: Record<string, string> = {
    ...(isForm ? {} : { 'Content-Type': 'application/json' }),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers as Record<string, string> | undefined),
  }

  const response = await fetch(`/api${path}`, {
    ...options,
    headers,
    body:
      options.body === undefined
        ? undefined
        : isForm
          ? (options.body as FormData)
          : JSON.stringify(options.body),
  })

  if (response.status === 401) {
    // Only attempt refresh/redirect for authenticated calls. Anonymous 401s
    // (e.g., an expired session check while already on /login) must NOT hard
    // reload the page — that would loop forever.
    if (!token) {
      throw new ApiError('Unauthorized', 401)
    }
    const refreshed = await tryRefresh()
    if (refreshed) {
      return request<T>(path, options)
    }
    clearTokens()
    window.location.href = '/login'
    throw new ApiError('Unauthorized', 401)
  }

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as { error?: string } | null
    throw new ApiError(body?.error ?? `Request failed (${response.status})`, response.status)
  }

  // Some endpoints succeed with an empty body (change-password, logout, reset-password);
  // treat that as undefined instead of throwing a JSON parse error.
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? (JSON.parse(text) as T) : (undefined as T))
}

async function tryRefresh(): Promise<boolean> {
  const refresh = getRefreshToken()
  if (!refresh) return false

  const response = await fetch('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: refresh }),
  })
  if (!response.ok) return false

  const data = (await response.json()) as AuthResponse
  setTokens(data.accessToken, data.refreshToken)
  return true
}

// ---- domain types ----

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresInSeconds: number
  userId: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
  emailConfirmationToken?: string | null
}

/** The caller's own identity, returned by GET /auth/me (used by the app shell). */
export interface MeResponse {
  userId: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
}

/** Reset token, populated only because email delivery is not configured yet (GAP-005). */
export interface ForgotPasswordResponse {
  resetToken?: string | null
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phoneNumber?: string
}

export interface PropertyDto {
  id: string
  name: string
  address: string
  city?: string
  description?: string
  managerUserId: string
  buildingCount: number
  unitCount: number
}

export interface BuildingDto {
  id: string
  propertyId: string
  name: string
  address?: string
  floors?: number
  unitCount: number
}

export interface UnitDto {
  id: string
  buildingId: string
  propertyId: string
  unitNumber: string
  unitType: string
  bedrooms?: number
  bathrooms?: number
  areaSqM?: number
  operationalStatus: string
  notes?: string
  isOccupied: boolean
}

export interface ResidentProfileDto {
  id: string
  userId: string
  firstName: string
  lastName: string
  email: string
  phoneNumber?: string
  isActive: boolean
  currentUnitId?: string
  currentUnitNumber?: string
  currentMoveInDate?: string
}

export interface UserAdminDto {
  id: string
  firstName: string
  lastName: string
  email: string
  isActive: boolean
  roles: string[]
  createdAt: string
}

export type MaintenanceStatus = 'Submitted' | 'Assigned' | 'InProgress' | 'Completed' | 'Confirmed' | 'Closed' | 'Cancelled'
export type MaintenancePriority = 'Low' | 'Medium' | 'High' | 'Urgent'

export interface MaintenanceRequestDto {
  id: string
  title: string
  description: string
  priority: MaintenancePriority
  status: MaintenanceStatus
  requestedByResidentId: string
  unitId: string
  unitNumber?: string
  assignedToUserId?: string
  assignedToName?: string
  assignedAt?: string
  completedAt?: string
  confirmedAt?: string
  closedAt?: string
  cancellationReason?: string
  createdAt: string
  attachmentCount: number
  attachments: { id: string; fileName: string; contentType: string; uploadedAt: string }[]
  history: {
    id: string
    fromStatus?: string
    toStatus: string
    comment?: string
    changedByUserId: string
    changedAt: string
  }[]
}

// ---- post-MVP module types (BR-005..011) ----

export interface NotificationDto {
  id: string
  title: string
  body: string
  eventType: string
  channel: string
  deliveryStatus: string
  isRead: boolean
  readAt?: string
  createdAt: string
}

export interface AnnouncementDto {
  id: string
  propertyId: string
  propertyName: string
  title: string
  body: string
  publishedByUserId: string
  publishedAt: string
}

export type LeaseStatus = 'Active' | 'Expired' | 'Terminated' | 'Cancelled'

export interface LeaseDto {
  id: string
  residentUserId: string
  residentName: string
  unitId: string
  unitNumber: string
  propertyId: string
  propertyName: string
  startDate: string
  endDate: string
  monthlyRent: number
  status: LeaseStatus
  currentVersion: number
  terminatedAt?: string
  documentCount: number
}

export interface LeaseDocumentDto {
  id: string
  leaseAgreementId: string
  fileName: string
  contentType: string
  uploadedByUserId: string
  version: number
  uploadedAt: string
}

export interface LeaseHistoryDto {
  version: number
  changeType: string
  description: string
  changedByUserId: string
  changedAt: string
}

/** Lease plus its documents and version history, returned by GET /leases/{id}. */
export interface LeaseDetailDto extends LeaseDto {
  documents: LeaseDocumentDto[]
  history: LeaseHistoryDto[]
}

export type InvoiceStatus = 'Open' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Cancelled'

export interface InvoiceDto {
  id: string
  leaseAgreementId: string
  residentUserId: string
  residentName: string
  unitId: string
  unitNumber: string
  propertyId: string
  propertyName: string
  periodStart: string
  periodEnd: string
  dueDate: string
  amount: number
  paidAmount: number
  outstandingBalance: number
  currency: string
  purpose: string
  status: InvoiceStatus
  paidAt?: string
  createdAt: string
  cancelledAt?: string
}

export interface PaymentTransactionDto {
  id: string
  transactionReference: string
  invoiceId: string
  residentUserId: string
  amount: number
  currency: string
  status: string
  method: string
  paidAt?: string
  confirmationNumber?: string
  failureReason?: string
  createdAt: string
}

export type PaymentAlertType = 'NewRequest' | 'DueSoon' | 'Overdue' | 'FailedPayment'
export type PaymentAlertSeverity = 'info' | 'warning' | 'critical'

/** One piece of payment attention shown by the dashboard notification bell. */
export interface PaymentAlertDto {
  type: PaymentAlertType
  severity: PaymentAlertSeverity
  title: string
  message: string
  amount: number
  currency: string
  dueDate?: string
  requestId?: string
}

/** The caller's own payment state, computed on the backend. */
export interface PaymentDashboardDto {
  amountCurrentlyDue: number
  overdueAmount: number
  upcomingAmount: number
  outstandingRequestCount: number
  overdueRequestCount: number
  nextDueDate?: string
  hasOutstanding: boolean
  currency: string
  requests: InvoiceDto[]
  history: PaymentTransactionDto[]
  alerts: PaymentAlertDto[]
}

/** A resident with money owed — the manager/administrator view. */
export interface ResidentOutstandingDto {
  residentUserId: string
  residentName: string
  email: string
  unitNumber: string
  propertyName: string
  totalOutstanding: number
  overdueAmount: number
  openRequestCount: number
  oldestDueDate?: string
  hasOverdue: boolean
  currency: string
}

export interface BalanceDto {
  totalOutstanding: number
  openInvoiceCount: number
  invoices: InvoiceDto[]
}

export interface FinancialReportDto {
  totalCollected: number
  totalOutstanding: number
  completedPayments: number
  failedPayments: number
  openInvoices: number
  paidInvoices: number
  overdueInvoices: number
  overdueTotal: number
}

export type PaymentInvoiceStatus = 'Issued' | 'Voided'

/** The receipt generated when a resident payment completes (BR-005, AC-01..AC-08). */
export interface PaymentInvoiceDto {
  id: string
  invoiceNumber: string
  paymentTransactionId: string
  transactionReference: string
  invoiceId: string
  residentUserId: string
  residentName: string
  unitId: string
  unitNumber: string
  propertyId: string
  propertyName: string
  amount: number
  currency: string
  purpose: string
  method: string
  status: PaymentInvoiceStatus
  paymentDate: string
  confirmationNumber?: string
  issuedAt: string
}

export interface FacilityDto {
  id: string
  propertyId: string
  propertyName: string
  name: string
  description: string
  isActive: boolean
  openMinutes: number
  closeMinutes: number
  slotMinutes: number
  cancellationWindowHours: number
}

export type BookingStatus = 'Reserved' | 'Cancelled' | 'Completed'

export interface FacilityBookingDto {
  id: string
  facilityId: string
  facilityName: string
  bookedByUserId: string
  bookedByName: string
  startAt: string
  endAt: string
  status: BookingStatus
  cancelledAt?: string
  cancellationReason?: string
  bookingReference: string
}

/** A facility's opening hours and reserved slots for one calendar day. */
export interface AvailabilityDto {
  facilityId: string
  date: string
  openMinutes: number
  closeMinutes: number
  slotMinutes: number
  bookings: FacilityBookingDto[]
}

export interface VisitorDto {
  id: string
  propertyId: string
  propertyName: string
  firstName: string
  lastName: string
  fullName: string
  phoneNumber?: string
  hostUserId?: string
  unitNumber?: string
  status: string
  registeredByUserId: string
  checkInAt?: string
  checkOutAt?: string
}

export interface AccessGrantDto {
  id: string
  propertyId: string
  propertyName: string
  targetType: string
  targetId: string
  targetName: string
  subjectType: string
  subjectUserId?: string
  subjectName: string
  visitorId?: string
  grantedByUserId: string
  grantedAt: string
  expiresAt?: string
  revokedAt?: string
  isActive: boolean
}

// ---- API ----

export const api = {
  register: (payload: RegisterRequest) =>
    request<AuthResponse>('/auth/register', { method: 'POST', body: payload }),

  confirmEmail: (userId: string, token: string) =>
    request<void>(`/auth/confirm-email?userId=${encodeURIComponent(userId)}&token=${encodeURIComponent(token)}`, { method: 'POST' }),

  login: (email: string, password: string) =>
    request<AuthResponse>('/auth/login', { method: 'POST', body: { email, password } }),

  logout: (refreshToken: string) =>
    request<void>('/auth/logout', { method: 'POST', body: { refreshToken } }),

  me: () => request<MeResponse>('/auth/me'),

  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>('/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } }),

  forgotPassword: (email: string) =>
    request<ForgotPasswordResponse>('/auth/forgot-password', { method: 'POST', body: { email } }),

  resetPassword: (email: string, token: string, newPassword: string) =>
    request<void>('/auth/reset-password', { method: 'POST', body: { email, token, newPassword } }),

  getStaff: (role?: string) =>
    request<{ id: string; firstName: string; lastName: string; email: string; role: string }[]>(
      `/auth/staff${role ? `?role=${encodeURIComponent(role)}` : ''}`,
    ),

  // admin user & role management (FR-RBAC-001..006)
  getUsers: (search?: string) =>
    request<UserAdminDto[]>(`/auth/users${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  setUserRoles: (userId: string, roles: string[]) =>
    request<void>(`/auth/users/${userId}/roles`, { method: 'PUT', body: { roles } }),
  deactivateUser: (userId: string) =>
    request<void>(`/auth/users/${userId}/deactivate`, { method: 'POST' }),
  activateUser: (userId: string) =>
    request<void>(`/auth/users/${userId}/activate`, { method: 'POST' }),

  // properties
  getProperties: () => request<PropertyDto[]>('/properties'),
  createProperty: (body: { name: string; address: string; city?: string; description?: string; managerUserId: string }) =>
    request<PropertyDto>('/properties', { method: 'POST', body }),
  getBuildings: (propertyId: string) => request<BuildingDto[]>(`/properties/${propertyId}/buildings`),
  createBuilding: (propertyId: string, body: { name: string; address?: string; floors?: number }) =>
    request<BuildingDto>(`/properties/${propertyId}/buildings`, { method: 'POST', body }),
  getUnits: (buildingId: string) => request<UnitDto[]>(`/buildings/${buildingId}/units`),
  createUnit: (buildingId: string, body: { unitNumber: string; unitType: string; bedrooms?: number; bathrooms?: number; areaSqM?: number; operationalStatus: string; notes?: string }) =>
    request<UnitDto>(`/buildings/${buildingId}/units`, { method: 'POST', body }),
  getProperty: (propertyId: string) => request<PropertyDto>(`/properties/${propertyId}`),
  updateProperty: (
    propertyId: string,
    body: { name: string; address: string; city?: string; description?: string; managerUserId: string },
  ) => request<PropertyDto>(`/properties/${propertyId}`, { method: 'PUT', body }),
  updateBuilding: (propertyId: string, buildingId: string, body: { name: string; address?: string; floors?: number }) =>
    request<BuildingDto>(`/properties/${propertyId}/buildings/${buildingId}`, { method: 'PUT', body }),
  getUnit: (unitId: string) => request<UnitDto>(`/units/${unitId}`),
  updateUnit: (
    unitId: string,
    body: {
      unitNumber: string
      unitType: string
      bedrooms?: number
      bathrooms?: number
      areaSqM?: number
      operationalStatus: string
      notes?: string
    },
  ) => request<UnitDto>(`/units/${unitId}`, { method: 'PUT', body }),

  // residents
  getResidents: (search?: string) =>
    request<ResidentProfileDto[]>(`/residents${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  getMyProfile: () => request<ResidentProfileDto>('/residents/me'),
  getResident: (residentId: string) => request<ResidentProfileDto>(`/residents/${residentId}`),
  updateResident: (residentId: string, body: { firstName: string; lastName: string; phoneNumber?: string }) =>
    request<ResidentProfileDto>(`/residents/${residentId}`, { method: 'PUT', body }),
  assignUnit: (residentId: string, unitId: string) =>
    request<ResidentProfileDto>(`/residents/${residentId}/assign-unit`, { method: 'POST', body: { unitId } }),
  moveOut: (residentId: string, moveOutDate?: string) =>
    request<ResidentProfileDto>(`/residents/${residentId}/move-out`, { method: 'POST', body: { moveOutDate } }),
  deactivateResident: (residentId: string) =>
    request<void>(`/residents/${residentId}/deactivate`, { method: 'POST' }),

  // maintenance
  getMaintenance: (filters?: { status?: MaintenanceStatus; priority?: MaintenancePriority; unitId?: string }) => {
    const query = new URLSearchParams()
    if (filters?.status) query.set('status', filters.status)
    if (filters?.priority) query.set('priority', filters.priority)
    if (filters?.unitId) query.set('unitId', filters.unitId)
    const suffix = query.toString()
    return request<MaintenanceRequestDto[]>(`/maintenance${suffix ? `?${suffix}` : ''}`)
  },
  getMaintenanceRequest: (id: string) => request<MaintenanceRequestDto>(`/maintenance/${id}`),
  submitMaintenance: (body: { title: string; description: string; unitId: string; priority: MaintenancePriority }) =>
    request<MaintenanceRequestDto>('/maintenance', { method: 'POST', body }),
  assignMaintenance: (id: string, technicianUserId: string, comment?: string) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/assign`, { method: 'POST', body: { technicianUserId, comment } }),
  updateMaintenanceStatus: (id: string, status: MaintenanceStatus, comment?: string) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/status`, { method: 'POST', body: { status, comment } }),
  setMaintenancePriority: (id: string, priority: MaintenancePriority) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/priority`, { method: 'POST', body: { priority } }),
  confirmMaintenance: (id: string, comment?: string) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/confirm`, { method: 'POST', body: { comment } }),
  uploadMaintenanceAttachment: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<void>(`/maintenance/${id}/attachments`, { method: 'POST', body: form })
  },

  // communication & notifications (BR-007)
  getNotifications: (unreadOnly = false) =>
    request<NotificationDto[]>(`/communication/notifications?unreadOnly=${unreadOnly}`),
  getUnreadCount: () => request<{ count: number }>('/communication/notifications/unread-count'),
  markNotificationRead: (id: string) =>
    request<void>(`/communication/notifications/${id}/read`, { method: 'POST' }),
  markAllNotificationsRead: () => request<void>('/communication/notifications/read-all', { method: 'POST' }),
  getMyAnnouncements: () => request<AnnouncementDto[]>('/communication/announcements/mine'),
  getAnnouncements: (propertyId?: string) =>
    request<AnnouncementDto[]>(`/communication/announcements${propertyId ? `?propertyId=${propertyId}` : ''}`),
  publishAnnouncement: (body: { propertyId: string; title: string; body: string }) =>
    request<AnnouncementDto>('/communication/announcements', { method: 'POST', body }),

  // leases (BR-006)
  getLeases: (status?: LeaseStatus) =>
    request<LeaseDto[]>(`/leases${status ? `?status=${encodeURIComponent(status)}` : ''}`),
  getLease: (id: string) => request<LeaseDetailDto>(`/leases/${id}`),
  createLease: (body: { residentUserId: string; unitId: string; startDate: string; endDate: string; monthlyRent: number }) =>
    request<LeaseDto>('/leases', { method: 'POST', body }),
  updateLease: (id: string, body: { startDate: string; endDate: string; monthlyRent: number }) =>
    request<LeaseDetailDto>(`/leases/${id}`, { method: 'PUT', body }),
  terminateLease: (id: string, comment?: string) =>
    request<void>(`/leases/${id}/terminate`, { method: 'POST', body: { comment } }),
  getLeaseDocuments: (id: string) => request<LeaseDocumentDto[]>(`/leases/${id}/documents`),
  uploadLeaseDocument: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<LeaseDocumentDto>(`/leases/${id}/documents`, { method: 'POST', body: form })
  },

  // payments (BR-005) + accountant reports (BR-011)
  getBalance: () => request<BalanceDto>('/payments/balance'),
  getPaymentDashboard: () => request<PaymentDashboardDto>('/payments/dashboard'),
  getInvoices: (status?: InvoiceStatus) =>
    request<InvoiceDto[]>(`/payments/invoices${status ? `?status=${encodeURIComponent(status)}` : ''}`),
  getOutstanding: () => request<ResidentOutstandingDto[]>('/payments/outstanding'),
  createInvoice: (body: { leaseAgreementId: string; periodStart: string; periodEnd: string; dueDate: string; amount: number; currency?: string; purpose?: string }) =>
    request<InvoiceDto>('/payments/invoices', { method: 'POST', body }),
  generateRentRequest: (leaseAgreementId: string) =>
    request<InvoiceDto>('/payments/requests/rent', { method: 'POST', body: { leaseAgreementId } }),
  payInvoice: (invoiceId: string, amount: number) =>
    request<PaymentTransactionDto>(`/payments/invoices/${invoiceId}/pay`, { method: 'POST', body: { amount, method: 'Card' } }),
  cancelInvoice: (invoiceId: string, reason?: string) =>
    request<InvoiceDto>(`/payments/invoices/${invoiceId}/cancel`, { method: 'POST', body: { reason } }),
  getPaymentHistory: () => request<PaymentTransactionDto[]>('/payments/history'),
  getFinancialReport: () => request<FinancialReportDto>('/payments/report'),
  getReconciliation: () =>
    request<{ expectedFromInvoices: number; collected: number; difference: number; isBalanced: boolean }>(
      '/payments/reconciliation',
    ),

  // payment invoices (BR-005): receipts generated for successful payments
  getMyInvoices: () => request<PaymentInvoiceDto[]>('/invoices/my'),
  getPaymentInvoices: (filters?: {
    status?: PaymentInvoiceStatus
    residentUserId?: string
    propertyId?: string
    from?: string
    to?: string
  }) => {
    const query = new URLSearchParams()
    if (filters?.status) query.set('status', filters.status)
    if (filters?.residentUserId) query.set('residentUserId', filters.residentUserId)
    if (filters?.propertyId) query.set('propertyId', filters.propertyId)
    if (filters?.from) query.set('from', filters.from)
    if (filters?.to) query.set('to', filters.to)
    const suffix = query.toString()
    return request<PaymentInvoiceDto[]>(`/invoices${suffix ? `?${suffix}` : ''}`)
  },
  getPaymentInvoice: (id: string) => request<PaymentInvoiceDto>(`/invoices/${id}`),

  // facility booking (BR-009)
  getFacilities: (propertyId?: string) =>
    request<FacilityDto[]>(`/bookings/facilities${propertyId ? `?propertyId=${propertyId}` : ''}`),
  getFacility: (facilityId: string) => request<FacilityDto>(`/bookings/facilities/${facilityId}`),
  getAvailability: (facilityId: string, date: string) =>
    request<AvailabilityDto>(`/bookings/facilities/${facilityId}/availability?date=${encodeURIComponent(date)}`),
  createFacility: (body: {
    propertyId: string
    name: string
    description?: string
    openMinutes: number
    closeMinutes: number
    slotMinutes: number
    cancellationWindowHours?: number
  }) => request<FacilityDto>('/bookings/facilities', { method: 'POST', body }),
  updateFacility: (
    facilityId: string,
    body: {
      name?: string
      description?: string
      isActive?: boolean
      openMinutes?: number
      closeMinutes?: number
      slotMinutes?: number
      cancellationWindowHours?: number
    },
  ) => request<FacilityDto>(`/bookings/facilities/${facilityId}`, { method: 'PUT', body }),
  bookFacility: (facilityId: string, startAt: string, endAt: string) =>
    request<FacilityBookingDto>('/bookings', { method: 'POST', body: { facilityId, startAt, endAt } }),
  getBookings: (filters?: { facilityId?: string; mineOnly?: boolean }) => {
    const query = new URLSearchParams()
    if (filters?.facilityId) query.set('facilityId', filters.facilityId)
    if (filters?.mineOnly) query.set('mineOnly', 'true')
    const suffix = query.toString()
    return request<FacilityBookingDto[]>(`/bookings${suffix ? `?${suffix}` : ''}`)
  },
  cancelBooking: (bookingId: string, reason?: string) =>
    request<void>(`/bookings/${bookingId}/cancel`, { method: 'POST', body: { reason } }),

  // physical security & visitors (BR-010)
  getVisitors: () => request<VisitorDto[]>('/security/visitors'),
  registerVisitor: (body: { firstName: string; lastName: string; propertyId: string; phoneNumber?: string; unitNumber?: string }) =>
    request<VisitorDto>('/security/visitors', { method: 'POST', body }),
  checkInVisitor: (id: string) => request<VisitorDto>(`/security/visitors/${id}/check-in`, { method: 'POST', body: {} }),
  checkOutVisitor: (id: string) => request<VisitorDto>(`/security/visitors/${id}/check-out`, { method: 'POST' }),
  getAccessLog: () => request<AccessGrantDto[]>('/security/access'),
  grantAccess: (body: { propertyId: string; targetType: string; targetId: string; subjectType: string; subjectUserId?: string; visitorId?: string; expiresAt?: string }) =>
    request<AccessGrantDto>('/security/access', { method: 'POST', body }),
  revokeAccess: (id: string) => request<void>(`/security/access/${id}/revoke`, { method: 'POST' }),
}

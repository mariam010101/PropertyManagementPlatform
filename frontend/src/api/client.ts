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
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers as Record<string, string> | undefined),
  }

  const response = await fetch(`/api${path}`, {
    ...options,
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })

  if (response.status === 401) {
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

  return (await response.json()) as T
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

  me: () => request<{ userId: string; roles: string[] }>('/auth/me'),

  getStaff: (role?: string) =>
    request<{ id: string; firstName: string; lastName: string; email: string; role: string }[]>(
      `/auth/staff${role ? `?role=${encodeURIComponent(role)}` : ''}`,
    ),

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

  // residents
  getResidents: (search?: string) =>
    request<ResidentProfileDto[]>(`/residents${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  getMyProfile: () => request<ResidentProfileDto>('/residents/me'),
  assignUnit: (residentId: string, unitId: string) =>
    request<ResidentProfileDto>(`/residents/${residentId}/assign-unit`, { method: 'POST', body: { unitId } }),
  deactivateResident: (residentId: string) =>
    request<void>(`/residents/${residentId}/deactivate`, { method: 'POST' }),

  // maintenance
  getMaintenance: () => request<MaintenanceRequestDto[]>('/maintenance'),
  submitMaintenance: (body: { title: string; description: string; unitId: string; priority: MaintenancePriority }) =>
    request<MaintenanceRequestDto>('/maintenance', { method: 'POST', body }),
  assignMaintenance: (id: string, technicianUserId: string) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/assign`, { method: 'POST', body: { technicianUserId } }),
  updateMaintenanceStatus: (id: string, status: MaintenanceStatus, comment?: string) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/status`, { method: 'POST', body: { status, comment } }),
  confirmMaintenance: (id: string) =>
    request<MaintenanceRequestDto>(`/maintenance/${id}/confirm`, { method: 'POST', body: {} }),
}

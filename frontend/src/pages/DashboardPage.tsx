import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  api,
  type FinancialReportDto,
  type MaintenanceRequestDto,
  type PaymentDashboardDto,
  type PropertyDto,
  type ResidentOutstandingDto,
} from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const TECHNICIAN = 'Technician'
const ACCOUNTANT = 'Accountant'

const money = (amount: number, currency = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD' }).format(amount)

const OPEN_STATUSES = ['Submitted', 'Assigned', 'InProgress']

interface AttentionItem {
  key: string
  title: string
  detail: string
  to: string
  tone: 'critical' | 'warning' | 'info'
}

interface QuickAction {
  label: string
  to: string
}

/**
 * Role-appropriate landing page. Every figure is composed on the client from the
 * existing role-scoped endpoints, so no new API surface or requirements are invented;
 * each role only calls what its policies allow.
 */
export default function DashboardPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isAdmin = roles.includes(ADMIN)
  const isManager = roles.includes(MANAGER)
  const isManagerOrAdmin = isManager || isAdmin
  const isResident = roles.includes(RESIDENT)
  const isTechnician = roles.includes(TECHNICIAN)
  const isAccountant = roles.includes(ACCOUNTANT)
  const isFinancial = isManagerOrAdmin || isAccountant

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [payment, setPayment] = useState<PaymentDashboardDto | null>(null)
  const [report, setReport] = useState<FinancialReportDto | null>(null)
  const [recon, setRecon] = useState<{ difference: number; isBalanced: boolean } | null>(null)
  const [outstanding, setOutstanding] = useState<ResidentOutstandingDto[]>([])
  const [maintenance, setMaintenance] = useState<MaintenanceRequestDto[]>([])
  const [properties, setProperties] = useState<PropertyDto[]>([])
  const [residentCount, setResidentCount] = useState<number | null>(null)
  const [unread, setUnread] = useState<number | null>(null)

  const load = async () => {
    const tasks: Promise<void>[] = []
    const track = (promise: Promise<unknown>) => {
      tasks.push(promise.then(() => undefined).catch((e) => setError((e as Error).message)))
    }

    if (isResident) track(api.getPaymentDashboard().then(setPayment))
    if (isFinancial) {
      track(api.getFinancialReport().then(setReport))
      track(api.getOutstanding().then(setOutstanding))
    }
    if (isAccountant) track(api.getReconciliation().then(setRecon))
    if (isResident || isManagerOrAdmin || isTechnician) track(api.getMaintenance().then(setMaintenance))
    if (isManagerOrAdmin) {
      track(api.getProperties().then(setProperties))
      track(api.getResidents().then((residents) => setResidentCount(residents.length)))
    }
    track(api.getUnreadCount().then((count) => setUnread(count.count)))

    await Promise.all(tasks)
    setLoading(false)
  }

  useEffect(() => {
    void load()
    const refreshOnFocus = () => void load()
    window.addEventListener('focus', refreshOnFocus)
    return () => window.removeEventListener('focus', refreshOnFocus)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const openMaintenance = maintenance.filter((request) => OPEN_STATUSES.includes(request.status))
  const urgentMaintenance = openMaintenance.filter(
    (request) => request.priority === 'High' || request.priority === 'Urgent',
  )
  const assignedToMe = openMaintenance.filter((request) => request.assignedToUserId === user?.id)
  const overdueResidents = outstanding.filter((resident) => resident.hasOverdue)
  const unitTotal = properties.reduce((total, property) => total + property.unitCount, 0)

  const stats: { label: string; value: string; hint: string; tone?: 'danger' | 'warn' | 'info' }[] = []
  if (isResident) {
    stats.push(
      {
        label: 'Amount due',
        value: payment ? money(payment.amountCurrentlyDue, payment.currency) : '—',
        hint: payment ? `${payment.outstandingRequestCount} open request(s)` : 'Loading…',
        tone: payment && payment.amountCurrentlyDue > 0 ? 'warn' : undefined,
      },
      {
        label: 'Overdue',
        value: payment ? money(payment.overdueAmount, payment.currency) : '—',
        hint: payment ? `${payment.overdueRequestCount} request(s)` : 'Loading…',
        tone: payment && payment.overdueAmount > 0 ? 'danger' : undefined,
      },
      { label: 'Open requests', value: `${openMaintenance.length}`, hint: 'Maintenance you raised' },
    )
  }
  if (isManagerOrAdmin) {
    stats.push(
      { label: 'Outstanding', value: report ? money(report.totalOutstanding) : '—', hint: 'Across your scope' },
      {
        label: 'Overdue',
        value: report ? money(report.overdueTotal) : '—',
        hint: report ? `${report.overdueInvoices} request(s)` : 'Loading…',
        tone: report && report.overdueTotal > 0 ? 'danger' : undefined,
      },
      { label: 'Open requests', value: `${openMaintenance.length}`, hint: `${urgentMaintenance.length} high/urgent` },
      { label: 'Residents', value: residentCount === null ? '—' : `${residentCount}`, hint: 'Active profiles' },
      { label: 'Units', value: properties.length === 0 ? '—' : `${unitTotal}`, hint: `${properties.length} properties` },
    )
  }
  if (isAccountant) {
    stats.push(
      { label: 'Collected', value: report ? money(report.totalCollected) : '—', hint: `${report?.completedPayments ?? 0} payments` },
      { label: 'Outstanding', value: report ? money(report.totalOutstanding) : '—', hint: `${report?.openInvoices ?? 0} open` },
      {
        label: 'Overdue',
        value: report ? money(report.overdueTotal) : '—',
        hint: `${report?.overdueInvoices ?? 0} request(s)`,
        tone: report && report.overdueTotal > 0 ? 'danger' : undefined,
      },
      {
        label: 'Reconciliation',
        value: recon ? money(recon.difference) : '—',
        hint: recon ? (recon.isBalanced ? 'Balanced' : 'Out of balance') : 'Loading…',
        tone: recon && !recon.isBalanced ? 'danger' : undefined,
      },
    )
  }
  if (isTechnician) {
    stats.push(
      { label: 'Assigned to me', value: `${assignedToMe.length}`, hint: 'Open requests' },
      {
        label: 'High/urgent',
        value: `${assignedToMe.filter((r) => r.priority === 'High' || r.priority === 'Urgent').length}`,
        hint: 'Needs attention first',
        tone: assignedToMe.some((r) => r.priority === 'High' || r.priority === 'Urgent') ? 'warn' : undefined,
      },
      { label: 'All open', value: `${openMaintenance.length}`, hint: 'In your queue' },
    )
  }
  stats.push({
    label: 'Unread',
    value: unread === null ? '—' : `${unread}`,
    hint: 'Notifications',
    tone: unread !== null && unread > 0 ? 'info' : undefined,
  })

  const attention: AttentionItem[] = []
  if (isResident && payment) {
    for (const alert of payment.alerts) {
      attention.push({
        key: `${alert.type}-${alert.requestId ?? alert.message}`,
        title: alert.title,
        detail: alert.message,
        to: '/payments',
        tone: alert.severity === 'critical' ? 'critical' : alert.severity === 'warning' ? 'warning' : 'info',
      })
    }
  }
  if (isManagerOrAdmin) {
    for (const resident of overdueResidents.slice(0, 5)) {
      attention.push({
        key: `resident-${resident.residentUserId}`,
        title: `${resident.residentName || resident.email} is overdue`,
        detail: `${money(resident.overdueAmount, resident.currency)} overdue · unit ${resident.unitNumber || '—'} · ${resident.propertyName}`,
        to: '/payments',
        tone: 'critical',
      })
    }
    for (const request of urgentMaintenance.slice(0, 5)) {
      attention.push({
        key: `mnt-${request.id}`,
        title: `${request.priority} maintenance: ${request.title}`,
        detail: `Unit ${request.unitNumber || '—'} · ${request.status}`,
        to: '/maintenance',
        tone: 'warning',
      })
    }
  }
  if (isTechnician) {
    const mine = [...assignedToMe].sort((a, b) => (a.priority === b.priority ? 0 : a.priority === 'Urgent' ? -1 : 1))
    for (const request of mine.slice(0, 6)) {
      attention.push({
        key: `mnt-${request.id}`,
        title: `${request.priority} — ${request.title}`,
        detail: `Unit ${request.unitNumber || '—'} · ${request.status}`,
        to: '/maintenance',
        tone: request.priority === 'Urgent' || request.priority === 'High' ? 'warning' : 'info',
      })
    }
  }
  if (isAccountant && recon && !recon.isBalanced) {
    attention.push({
      key: 'recon',
      title: 'Reconciliation is out of balance',
      detail: `Difference of ${money(recon.difference)} between invoices and collected payments.`,
      to: '/payments',
      tone: 'critical',
    })
  }

  const actions: QuickAction[] = []
  if (isResident) {
    actions.push(
      { label: 'Pay rent', to: '/payments' },
      { label: 'Submit a request', to: '/maintenance' },
      { label: 'Book a facility', to: '/bookings' },
    )
  }
  if (isManagerOrAdmin) {
    actions.push(
      { label: 'Outstanding balances', to: '/payments' },
      { label: 'Maintenance', to: '/maintenance' },
      { label: 'Residents', to: '/residents' },
    )
    if (isAdmin) actions.push({ label: 'Users & roles', to: '/users' })
  }
  if (isAccountant) actions.push({ label: 'Financial report', to: '/payments' })
  if (isTechnician) actions.push({ label: 'My requests', to: '/maintenance' })

  return (
    <div className="container">
      <header className="page-head">
        <div>
          <h2>Welcome back{user?.firstName ? `, ${user.firstName}` : ''}</h2>
          <p className="muted">
            {isResident
              ? 'What needs your attention and what you owe.'
              : 'What needs your attention across your scope.'}
          </p>
        </div>
        {loading && <span className="muted">Refreshing…</span>}
      </header>

      {error && <div className="error">{error}</div>}

      <div className="stats">
        {stats.map((stat) => (
          <div
            key={stat.label}
            className={`stat${
              stat.tone === 'danger' ? ' stat--danger' : stat.tone === 'warn' ? ' stat--warn' : stat.tone === 'info' ? ' stat--due' : ''
            }`}
          >
            <span className="stat__label">{stat.label}</span>
            <span className="stat__value">{stat.value}</span>
            <span className="stat__hint">{stat.hint}</span>
          </div>
        ))}
      </div>

      <section className="dashboard-section" aria-label="Needs attention">
        <h3>Needs your attention</h3>
        {attention.length === 0 ? (
          <div className="panel muted">
            {loading ? 'Checking your accounts…' : 'Nothing needs your attention right now.'}
          </div>
        ) : (
          <ul className="attention">
            {attention.map((item) => (
              <li key={item.key} className="attention__item">
                <span className={`attention__dot attention__dot--${item.tone}`} aria-hidden="true" />
                <div className="attention__body">
                  <span className="attention__title">{item.title}</span>
                  <span className="attention__meta">{item.detail}</span>
                </div>
                <Link className="btn" to={item.to}>
                  Open
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      {actions.length > 0 && (
        <section className="dashboard-section" aria-label="Quick actions">
          <h3>Quick actions</h3>
          <div className="quick-actions">
            {actions.map((action) => (
              <Link key={action.to + action.label} className="btn" to={action.to}>
                {action.label}
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}

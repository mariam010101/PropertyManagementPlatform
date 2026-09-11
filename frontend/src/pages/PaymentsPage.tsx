import { useEffect, useState } from 'react'
import {
  api,
  type FinancialReportDto,
  type InvoiceDto,
  type InvoiceStatus,
  type LeaseDto,
  type PaymentDashboardDto,
  type PaymentTransactionDto,
  type ResidentOutstandingDto,
} from '../api/client'
import { useAuth } from '../auth/AuthContext'
import PaymentNotificationBell from '../components/PaymentNotificationBell'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const ACCOUNTANT = 'Accountant'

const money = (amount: number, currency = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD' }).format(amount)

const date = (value?: string) =>
  value ? new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) : '—'

const humanStatus = (status: InvoiceStatus) =>
  status === 'PartiallyPaid' ? 'Part paid' : status

const statusClass = (status: InvoiceStatus) =>
  status === 'Paid'
    ? 'ok'
    : status === 'Overdue'
      ? 'danger'
      : status === 'PartiallyPaid'
        ? 'warn'
        : status === 'Cancelled'
          ? 'muted'
          : 'info'

const transactionClass = (status: string) =>
  status === 'Completed' ? 'ok' : status === 'Failed' ? 'danger' : status === 'Refunded' ? 'muted' : 'info'

export default function PaymentsPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isResident = roles.includes(RESIDENT)
  const isManager = roles.includes(MANAGER)
  const isAdmin = roles.includes(ADMIN)
  const isAccountant = roles.includes(ACCOUNTANT)
  const isFinancial = isAdmin || isManager || isAccountant
  const canManageRequests = isAdmin || isManager

  const [dashboard, setDashboard] = useState<PaymentDashboardDto | null>(null)
  const [invoices, setInvoices] = useState<InvoiceDto[]>([])
  const [outstanding, setOutstanding] = useState<ResidentOutstandingDto[]>([])
  const [leases, setLeases] = useState<LeaseDto[]>([])
  const [selectedLease, setSelectedLease] = useState('')
  const [report, setReport] = useState<FinancialReportDto | null>(null)
  const [recon, setRecon] = useState<{ expectedFromInvoices: number; collected: number; difference: number; isBalanced: boolean } | null>(null)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [busy, setBusy] = useState(false)
  const [highlightId, setHighlightId] = useState<string | null>(null)

  const load = async () => {
    try {
      if (isResident) {
        setDashboard(await api.getPaymentDashboard())
      }
      if (isFinancial) {
        setInvoices(await api.getInvoices())
        setOutstanding(await api.getOutstanding())
        setReport(await api.getFinancialReport())
        setRecon(await api.getReconciliation())
      }
      if (canManageRequests) {
        const all = await api.getLeases()
        setLeases(all.filter((lease) => lease.status === 'Active'))
      }
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // The bell only highlights briefly so the dashboard stays calm.
  useEffect(() => {
    if (!highlightId) return
    const timer = window.setTimeout(() => setHighlightId(null), 2600)
    return () => window.clearTimeout(timer)
  }, [highlightId])

  const focusRequest = (requestId: string) => {
    setHighlightId(requestId)
    window.setTimeout(() => {
      document.getElementById(`request-${requestId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    }, 0)
  }

  const run = async (action: () => Promise<string>) => {
    setBusy(true)
    setError('')
    setSuccess('')
    try {
      setSuccess(await action())
      await load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const pay = (invoice: InvoiceDto) =>
    run(async () => {
      const transaction = await api.payInvoice(invoice.id, invoice.outstandingBalance)
      return `Payment of ${money(transaction.amount, invoice.currency)} received. Confirmation ${transaction.confirmationNumber}.`
    }).catch(() => undefined)

  const cancel = (invoice: InvoiceDto) =>
    run(async () => {
      await api.cancelInvoice(invoice.id, 'Cancelled from the payments dashboard.')
      return 'Payment request cancelled. The record is retained for history.'
    }).catch(() => undefined)

  const raiseRent = () =>
    run(async () => {
      const invoice = await api.generateRentRequest(selectedLease)
      return `Rent request for ${money(invoice.amount, invoice.currency)} raised, due ${date(invoice.dueDate)}.`
    }).catch(() => undefined)

  return (
    <div className="container">
      <header className="page-head">
        <div>
          <h2>Payments</h2>
          <p className="muted">
            {isResident ? 'What you owe, what is coming up and what you have paid.' : 'Outstanding balances, payment requests and financials.'}
          </p>
        </div>
        {isResident && dashboard && (
          <PaymentNotificationBell alerts={dashboard.alerts} onSelectRequest={focusRequest} />
        )}
      </header>

      {error && <div className="error">{error}</div>}
      {success && <div className="hint">{success}</div>}

      {isResident && dashboard && (
        <section aria-label="Payment summary">
          {dashboard.overdueAmount > 0 && (
            <div className="notice notice--danger" role="status">
              <strong>{money(dashboard.overdueAmount, dashboard.currency)} is overdue.</strong>
              <span className="muted"> Settle it to stop further reminders.</span>
            </div>
          )}

          <div className="stats">
            <div className={`stat${dashboard.amountCurrentlyDue > 0 ? ' stat--due' : ''}`}>
              <span className="stat__label">Amount currently due</span>
              <span className="stat__value">{money(dashboard.amountCurrentlyDue, dashboard.currency)}</span>
              <span className="muted">{dashboard.outstandingRequestCount} open request(s)</span>
            </div>
            <div className={`stat${dashboard.overdueAmount > 0 ? ' stat--danger' : ''}`}>
              <span className="stat__label">Overdue</span>
              <span className="stat__value">{money(dashboard.overdueAmount, dashboard.currency)}</span>
              <span className="muted">{dashboard.overdueRequestCount} request(s)</span>
            </div>
            <div className="stat stat--warn">
              <span className="stat__label">Upcoming</span>
              <span className="stat__value">{money(dashboard.upcomingAmount, dashboard.currency)}</span>
              <span className="muted">Not yet due</span>
            </div>
            <div className="stat">
              <span className="stat__label">Next due date</span>
              <span className="stat__value">{date(dashboard.nextDueDate)}</span>
              <span className="muted">{dashboard.hasOutstanding ? 'Keep this in mind' : 'Nothing scheduled'}</span>
            </div>
          </div>

          <h3>Payment requests</h3>
          {dashboard.requests.length === 0 ? (
            <div className="panel muted">You have no payment requests yet.</div>
          ) : (
            dashboard.requests.map((invoice) => {
              const paidPercent = invoice.amount > 0 ? Math.min(100, Math.round((invoice.paidAmount / invoice.amount) * 100)) : 0
              return (
                <article
                  key={invoice.id}
                  id={`request-${invoice.id}`}
                  className={`panel request${highlightId === invoice.id ? ' request--highlight' : ''}`}
                >
                  <div className="row space-between">
                    <div className="row">
                      <strong>{invoice.purpose} payment</strong>
                      <span className={`pill pill--${statusClass(invoice.status)}`}>{humanStatus(invoice.status)}</span>
                    </div>
                    <span className="request__amount">{money(invoice.outstandingBalance, invoice.currency)}</span>
                  </div>

                  <div className="muted request__meta">
                    Due {date(invoice.dueDate)} · {invoice.propertyName || 'Property'} · Unit {invoice.unitNumber || '—'}
                  </div>

                  {invoice.paidAmount > 0 && invoice.outstandingBalance > 0 && (
                    <div className="request__progress">
                      <div className="request__progress-bar" style={{ width: `${paidPercent}%` }} />
                    </div>
                  )}

                  <div className="row space-between request__foot">
                    <span className="muted">
                      {money(invoice.amount, invoice.currency)} total
                      {invoice.paidAmount > 0 ? ` · ${money(invoice.paidAmount, invoice.currency)} paid` : ''}
                    </span>
                    {invoice.outstandingBalance > 0 && (
                      <button type="button" disabled={busy} onClick={() => pay(invoice)}>
                        Pay {money(invoice.outstandingBalance, invoice.currency)}
                      </button>
                    )}
                  </div>
                </article>
              )
            })
          )}

          <h3>Payment history</h3>
          {dashboard.history.length === 0 ? (
            <p className="muted">No payments yet.</p>
          ) : (
            <div className="ledger">
              {dashboard.history.map((transaction: PaymentTransactionDto) => (
                <div key={transaction.id} className="ledger__row">
                  <span className="muted ledger__ref">{transaction.transactionReference}</span>
                  <span>{money(transaction.amount, transaction.currency)}</span>
                  <span className={`pill pill--${transactionClass(transaction.status)}`}>{transaction.status}</span>
                  <span className="muted">{date(transaction.paidAt ?? transaction.createdAt)}</span>
                  <span className="muted ledger__note">{transaction.confirmationNumber ?? transaction.failureReason ?? ''}</span>
                </div>
              ))}
            </div>
          )}
        </section>
      )}

      {isFinancial && (
        <section aria-label="Financial overview">
          {report && (
            <div className="stats">
              <div className="stat">
                <span className="stat__label">Collected</span>
                <span className="stat__value">{money(report.totalCollected)}</span>
                <span className="muted">{report.completedPayments} completed · {report.failedPayments} failed</span>
              </div>
              <div className="stat stat--warn">
                <span className="stat__label">Outstanding</span>
                <span className="stat__value">{money(report.totalOutstanding)}</span>
                <span className="muted">{report.openInvoices} open · {report.paidInvoices} paid</span>
              </div>
              <div className={`stat${report.overdueTotal > 0 ? ' stat--danger' : ''}`}>
                <span className="stat__label">Overdue</span>
                <span className="stat__value">{money(report.overdueTotal)}</span>
                <span className="muted">{report.overdueInvoices} request(s)</span>
              </div>
              {recon && (
                <div className="stat">
                  <span className="stat__label">Reconciliation</span>
                  <span className="stat__value">{money(recon.difference)}</span>
                  <span className={recon.isBalanced ? 'ok-text' : 'danger-text'}>
                    {recon.isBalanced ? 'Balanced' : 'Out of balance'}
                  </span>
                </div>
              )}
            </div>
          )}

          <h3>Residents with an outstanding balance</h3>
          {outstanding.length === 0 ? (
            <div className="panel muted">No outstanding balances in your scope.</div>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th scope="col">Resident</th>
                    <th scope="col">Unit</th>
                    <th scope="col">Outstanding</th>
                    <th scope="col">Overdue</th>
                    <th scope="col">Requests</th>
                    <th scope="col">Oldest due</th>
                  </tr>
                </thead>
                <tbody>
                  {outstanding.map((resident) => (
                    <tr key={resident.residentUserId}>
                      <td>
                        <strong>{resident.residentName || resident.email}</strong>
                        <span className="muted table__sub">{resident.propertyName}</span>
                      </td>
                      <td>{resident.unitNumber || '—'}</td>
                      <td>{money(resident.totalOutstanding, resident.currency)}</td>
                      <td className={resident.overdueAmount > 0 ? 'danger-text' : ''}>
                        {money(resident.overdueAmount, resident.currency)}
                      </td>
                      <td>{resident.openRequestCount}</td>
                      <td>{date(resident.oldestDueDate)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {canManageRequests && (
            <>
              <h3>Raise a rent request</h3>
              <div className="panel">
                <p className="muted">
                  The amount is taken from the lease's monthly rent — it is never typed in by hand.
                </p>
                <div className="row">
                  <label className="field">
                    <span>Active lease</span>
                    <select value={selectedLease} onChange={(event) => setSelectedLease(event.target.value)}>
                      <option value="">Select a lease…</option>
                      {leases.map((lease) => (
                        <option key={lease.id} value={lease.id}>
                          {lease.residentName || 'Resident'} · Unit {lease.unitNumber} · {money(lease.monthlyRent)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button type="button" disabled={busy || !selectedLease} onClick={raiseRent}>
                    Raise {leases.find((l) => l.id === selectedLease) ? money(leases.find((l) => l.id === selectedLease)!.monthlyRent) : ''} request
                  </button>
                </div>
              </div>
            </>
          )}

          <h3>All payment requests</h3>
          {invoices.length === 0 ? (
            <div className="panel muted">No payment requests in your scope.</div>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th scope="col">Resident</th>
                    <th scope="col">Purpose</th>
                    <th scope="col">Due</th>
                    <th scope="col">Amount</th>
                    <th scope="col">Status</th>
                    <th scope="col" />
                  </tr>
                </thead>
                <tbody>
                  {invoices.map((invoice) => (
                    <tr key={invoice.id}>
                      <td>
                        <strong>{invoice.residentName || 'Resident'}</strong>
                        <span className="muted table__sub">Unit {invoice.unitNumber || '—'}</span>
                      </td>
                      <td>{invoice.purpose}</td>
                      <td>{date(invoice.dueDate)}</td>
                      <td>
                        {money(invoice.amount, invoice.currency)}
                        {invoice.paidAmount > 0 && (
                          <span className="muted table__sub">{money(invoice.paidAmount, invoice.currency)} paid</span>
                        )}
                      </td>
                      <td>
                        <span className={`pill pill--${statusClass(invoice.status)}`}>{humanStatus(invoice.status)}</span>
                      </td>
                      <td className="table__actions">
                        {canManageRequests && invoice.status !== 'Paid' && invoice.status !== 'Cancelled' && (
                          <button type="button" className="ghost danger" disabled={busy} onClick={() => cancel(invoice)}>
                            Cancel
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </div>
  )
}

import { useEffect, useMemo, useState } from 'react'
import { api, type PaymentInvoiceDto, type PaymentInvoiceStatus } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const RESIDENT = 'Resident'
const ACCOUNTANT = 'Accountant'
const MANAGER = 'PropertyManager'
const ADMIN = 'Administrator'

const money = (amount: number, currency = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD' }).format(amount)

const date = (value?: string) =>
  value ? new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) : '—'

const statusClass = (status: PaymentInvoiceStatus) => (status === 'Issued' ? 'ok' : 'muted')

/**
 * Payment invoices (BR-005): receipts generated for successful payments.
 *
 * Residents see their own invoices; accountants/managers/admins see invoices within
 * their financial scope, with a text search and a status filter. The backend enforces
 * every access rule — this page only renders what the API returns.
 */
export default function InvoicesPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isResident = roles.includes(RESIDENT)
  const isFinancial = roles.includes(ACCOUNTANT) || roles.includes(MANAGER) || roles.includes(ADMIN)

  const [invoices, setInvoices] = useState<PaymentInvoiceDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [statusFilter, setStatusFilter] = useState<PaymentInvoiceStatus | ''>('')
  const [search, setSearch] = useState('')

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const data = isResident ? await api.getMyInvoices() : await api.getPaymentInvoices()
      setInvoices(data)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const visible = useMemo(() => {
    let list = invoices
    if (statusFilter) {
      list = list.filter((invoice) => invoice.status === statusFilter)
    }
    const query = search.trim().toLowerCase()
    if (query) {
      list = list.filter((invoice) =>
        [invoice.invoiceNumber, invoice.residentName, invoice.propertyName, invoice.unitNumber, invoice.purpose].some(
          (value) => value?.toLowerCase().includes(query),
        ),
      )
    }
    return list
  }, [invoices, statusFilter, search])

  const selected = invoices.find((invoice) => invoice.id === selectedId) ?? null

  return (
    <div className="container">
      <header className="page-head">
        <div>
          <h2>Invoices</h2>
          <p className="muted">
            {isResident
              ? 'Receipts for your successful payments.'
              : 'Invoices generated for successful resident payments within your scope.'}
          </p>
        </div>
        <button type="button" className="ghost" onClick={() => void load()} disabled={loading}>
          Refresh
        </button>
      </header>

      {error && (
        <div className="error" role="alert">
          {error}
          <button type="button" className="ghost" onClick={() => void load()}>
            Retry
          </button>
        </div>
      )}

      {loading ? (
        <div className="panel muted">Loading invoices…</div>
      ) : (
        <>
          {isFinancial && invoices.length > 0 && (
            <div className="row" aria-label="Invoice filters">
              <label className="field">
                <span>Search</span>
                <input
                  type="search"
                  placeholder="Invoice #, resident, property, unit"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
              <label className="field">
                <span>Status</span>
                <select
                  value={statusFilter}
                  onChange={(event) => setStatusFilter(event.target.value as PaymentInvoiceStatus | '')}
                >
                  <option value="">All statuses</option>
                  <option value="Issued">Issued</option>
                  <option value="Voided">Voided</option>
                </select>
              </label>
            </div>
          )}

          {visible.length === 0 ? (
            <div className="panel muted">No invoices available yet.</div>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th scope="col">Invoice</th>
                    {isFinancial && <th scope="col">Resident</th>}
                    <th scope="col">Property / unit</th>
                    <th scope="col">Amount</th>
                    <th scope="col">Payment date</th>
                    <th scope="col">Method</th>
                    <th scope="col">Status</th>
                    <th scope="col" />
                  </tr>
                </thead>
                <tbody>
                  {visible.map((invoice) => (
                    <tr key={invoice.id}>
                      <td>
                        <strong>{invoice.invoiceNumber}</strong>
                        <span className="muted table__sub">{invoice.purpose}</span>
                      </td>
                      {isFinancial && (
                        <td>
                          <strong>{invoice.residentName || 'Resident'}</strong>
                        </td>
                      )}
                      <td>
                        {invoice.propertyName || '—'}
                        <span className="muted table__sub">Unit {invoice.unitNumber || '—'}</span>
                      </td>
                      <td>{money(invoice.amount, invoice.currency)}</td>
                      <td>{date(invoice.paymentDate)}</td>
                      <td>{invoice.method}</td>
                      <td>
                        <span className={`pill pill--${statusClass(invoice.status)}`}>{invoice.status}</span>
                      </td>
                      <td className="table__actions">
                        <button
                          type="button"
                          className="ghost"
                          onClick={() => setSelectedId(selectedId === invoice.id ? null : invoice.id)}
                        >
                          {selectedId === invoice.id ? 'Close' : 'View'}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {selected && (
            <section aria-label="Invoice details" className="panel">
              <div className="row space-between">
                <div>
                  <h3>{selected.invoiceNumber}</h3>
                  <span className="muted">Issued {date(selected.issuedAt)}</span>
                </div>
                <span className={`pill pill--${statusClass(selected.status)}`}>{selected.status}</span>
              </div>

              <div className="stats">
                <div className="stat">
                  <span className="stat__label">Amount paid</span>
                  <span className="stat__value">{money(selected.amount, selected.currency)}</span>
                  <span className="muted">{selected.method} · {selected.purpose}</span>
                </div>
                <div className="stat">
                  <span className="stat__label">Payment date</span>
                  <span className="stat__value">{date(selected.paymentDate)}</span>
                  <span className="muted">{selected.confirmationNumber ?? selected.transactionReference}</span>
                </div>
                <div className="stat">
                  <span className="stat__label">Property / unit</span>
                  <span className="stat__value">{selected.unitNumber || '—'}</span>
                  <span className="muted">{selected.propertyName || '—'}</span>
                </div>
                {isFinancial && (
                  <div className="stat">
                    <span className="stat__label">Resident</span>
                    <span className="stat__value">{selected.residentName || 'Resident'}</span>
                    <span className="muted">Unit {selected.unitNumber || '—'}</span>
                  </div>
                )}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  )
}

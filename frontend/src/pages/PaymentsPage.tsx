import { useEffect, useState } from 'react'
import {
  api,
  type BalanceDto,
  type FinancialReportDto,
  type InvoiceDto,
  type PaymentTransactionDto,
} from '../api/client'
import { useAuth } from '../auth/AuthContext'

const ADMIN = 'Administrator'
const MANAGER = 'PropertyManager'
const RESIDENT = 'Resident'
const ACCOUNTANT = 'Accountant'

export default function PaymentsPage() {
  const { user } = useAuth()
  const roles = user?.roles ?? []
  const isResident = roles.includes(RESIDENT)
  const isFinancial = roles.includes(ADMIN) || roles.includes(MANAGER) || roles.includes(ACCOUNTANT)

  const [balance, setBalance] = useState<BalanceDto | null>(null)
  const [invoices, setInvoices] = useState<InvoiceDto[]>([])
  const [history, setHistory] = useState<PaymentTransactionDto[]>([])
  const [report, setReport] = useState<FinancialReportDto | null>(null)
  const [recon, setRecon] = useState<{ expectedFromInvoices: number; collected: number; difference: number; isBalanced: boolean } | null>(null)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const load = async () => {
    try {
      if (isResident) setBalance(await api.getBalance())
      if (isFinancial) {
        setInvoices(await api.getInvoices())
        setHistory(await api.getPaymentHistory())
        setReport(await api.getFinancialReport())
        setRecon(await api.getReconciliation())
      }
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const pay = async (invoiceId: string, amount: number) => {
    setError('')
    setSuccess('')
    try {
      const tx = await api.payInvoice(invoiceId, amount)
      setSuccess(`Payment received. Confirmation ${tx.confirmationNumber}`)
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const money = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <div className="container">
      <h2>Payments</h2>
      {error && <div className="error">{error}</div>}
      {success && <div className="hint">{success}</div>}

      {isResident && balance && (
        <>
          <div className="panel">
            <h3>Outstanding balance</h3>
            <p className={balance.totalOutstanding > 0 ? 'error' : 'hint'}>
              {money(balance.totalOutstanding)} across {balance.openInvoiceCount} open invoice(s)
            </p>
          </div>
          <h3>My invoices</h3>
          {balance.invoices.map((inv) => (
            <div key={inv.id} className="panel">
              <div className="row space-between">
                <strong>Invoice due {new Date(inv.dueDate).toLocaleDateString()}</strong>
                <span className="badge">{inv.status}</span>
              </div>
              <div className="muted">
                Amount {money(inv.amount)} A· Paid {money(inv.paidAmount)} A· Outstanding {money(inv.outstandingBalance)}
              </div>
              {inv.outstandingBalance > 0 && (
                <button onClick={() => pay(inv.id, inv.outstandingBalance)}>Pay {money(inv.outstandingBalance)}</button>
              )}
            </div>
          ))}
          <h3>Payment history</h3>
          {history.length === 0 && <p className="muted">No payments yet.</p>}
          {history.map((h) => (
            <div key={h.id} className="row space-between">
              <span className="muted">{h.transactionReference}</span>
              <span>{money(h.amount)}</span>
              <span className="badge">{h.status}</span>
              <span className="muted">{h.confirmationNumber ?? ''}</span>
            </div>
          ))}
        </>
      )}

      {isFinancial && (
        <>
          {report && (
            <div className="panel">
              <h3>Financial report</h3>
              <p>Collected: <strong>{money(report.totalCollected)}</strong></p>
              <p>Outstanding: <strong>{money(report.totalOutstanding)}</strong></p>
              <p className="muted">
                {report.completedPayments} completed / {report.failedPayments} failed payments A· {report.paidInvoices} paid / {report.openInvoices} open invoices
              </p>
            </div>
          )}
          {recon && (
            <div className="panel">
              <h3>Reconciliation</h3>
              <p>Expected from invoices: {money(recon.expectedFromInvoices)}</p>
              <p>Collected: {money(recon.collected)}</p>
              <p>Difference: <strong>{money(recon.difference)}</strong></p>
              <p className={recon.isBalanced ? 'hint' : 'error'}>{recon.isBalanced ? 'Balanced' : 'Out of balance'}</p>
            </div>
          )}
          <h3>All invoices</h3>
          {invoices.map((inv) => (
            <div key={inv.id} className="row space-between">
              <span>{inv.residentName} A· Unit {inv.unitNumber}</span>
              <span className="muted">{inv.propertyName}</span>
              <span>{money(inv.amount)}</span>
              <span className="badge">{inv.status}</span>
              <span className="muted">Due {new Date(inv.dueDate).toLocaleDateString()}</span>
            </div>
          ))}
        </>
      )}
    </div>
  )
}

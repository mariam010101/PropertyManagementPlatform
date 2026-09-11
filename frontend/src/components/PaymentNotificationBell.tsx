import { useEffect, useRef, useState } from 'react'
import type { PaymentAlertDto, PaymentAlertSeverity } from '../api/client'

interface PaymentNotificationBellProps {
  /** Attention items computed by the backend; empty means nothing requires action. */
  alerts: PaymentAlertDto[]
  /** Navigate to the payment request the alert refers to. */
  onSelectRequest: (requestId: string) => void
}

const money = (amount: number, currency: string) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: currency || 'USD' }).format(amount)

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) : ''

const severityClass = (severity: PaymentAlertSeverity) =>
  severity === 'critical' ? 'danger' : severity === 'warning' ? 'warn' : 'info'

/**
 * Prominent payment notification bell. It is a real button (keyboard reachable,
 * aria-labelled) that opens a short, scannable list of the payments needing attention —
 * what it is, how much, when it is due and, where relevant, a direct link to the request.
 */
export default function PaymentNotificationBell({ alerts, onSelectRequest }: PaymentNotificationBellProps) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)

  const count = alerts.length
  const hasCritical = alerts.some((alert) => alert.severity === 'critical')

  useEffect(() => {
    if (!open) return

    const onPointerDown = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }

    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const ariaLabel =
    count === 0
      ? 'Payment notifications: nothing needs attention'
      : `Payment notifications: ${count} ${count === 1 ? 'item needs' : 'items need'} attention`

  return (
    <div className="bell" ref={rootRef}>
      <button
        type="button"
        className={`bell__button${hasCritical ? ' bell__button--critical' : ''}`}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={ariaLabel}
        onClick={() => setOpen((value) => !value)}
      >
        <svg className="bell__icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
          <path
            d="M12 3a1 1 0 0 1 1 1v.62A5.5 5.5 0 0 1 17.5 10v3.1l1.28 2.13a1 1 0 0 1-.86 1.52H6.08a1 1 0 0 1-.86-1.52L6.5 13.1V10a5.5 5.5 0 0 1 4.5-5.38V4a1 1 0 0 1 1-1Z"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinejoin="round"
          />
          <path d="M9.6 19.3a2.6 2.6 0 0 0 4.8 0" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
        </svg>
        <span className="bell__label">Payments</span>
        {count > 0 && <span className="bell__badge">{count > 9 ? '9+' : count}</span>}
      </button>

      {open && (
        <div className="bell__menu" role="menu" aria-label="Payments needing attention">
          <div className="bell__menu-head">
            <strong>Needs your attention</strong>
            <span className="muted">{count === 0 ? 'All up to date' : `${count} open`}</span>
          </div>

          {count === 0 ? (
            <p className="bell__empty">Nothing is due and no payment has failed. You are up to date.</p>
          ) : (
            <ul className="bell__list">
              {alerts.map((alert) => (
                <li
                  key={`${alert.type}-${alert.requestId ?? alert.message}`}
                  className={`bell__item bell__item--${severityClass(alert.severity)}`}
                >
                  <div className="bell__item-head">
                    <span className={`pill pill--${severityClass(alert.severity)}`}>{alert.title}</span>
                    <span className="bell__amount">{money(alert.amount, alert.currency)}</span>
                  </div>
                  <p className="bell__message">{alert.message}</p>
                  <div className="bell__item-foot">
                    {alert.dueDate && <span className="muted">Due {formatDate(alert.dueDate)}</span>}
                    {alert.requestId && (
                      <button
                        type="button"
                        className="ghost bell__view"
                        onClick={() => {
                          setOpen(false)
                          onSelectRequest(alert.requestId as string)
                        }}
                      >
                        View request
                      </button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

using System.ComponentModel.DataAnnotations;
using PMP.Modules.Payment.Entities;
using PMP.Modules.Payment.Enums;

namespace PMP.Modules.Payment.Contracts;

/// <summary>
/// A payment request/order as seen by a user: who owes it, how much, in which currency,
/// for what, when it is due, its status and when it was raised.
/// </summary>
public record InvoiceDto
{
    public Guid Id { get; init; }

    public Guid LeaseAgreementId { get; init; }

    public Guid ResidentUserId { get; init; }

    public string ResidentName { get; init; } = string.Empty;

    public Guid UnitId { get; init; }

    public string UnitNumber { get; init; } = string.Empty;

    public Guid PropertyId { get; init; }

    public string PropertyName { get; init; } = string.Empty;

    public DateTimeOffset PeriodStart { get; init; }

    public DateTimeOffset PeriodEnd { get; init; }

    public DateTimeOffset DueDate { get; init; }

    public decimal Amount { get; init; }

    public decimal PaidAmount { get; init; }

    /// <summary>Outstanding balance = Amount - PaidAmount (FR-PAY-001, BRULE-PAY-006).</summary>
    public decimal OutstandingBalance => Amount - PaidAmount;

    /// <summary>ISO 4217 currency of <see cref="Amount"/>.</summary>
    public string Currency { get; init; } = PaymentCurrency.Default;

    /// <summary>Why the payment is owed (Rent, Utilities, Deposit, …).</summary>
    public string Purpose { get; init; } = PaymentPurpose.Rent;

    /// <summary>Effective lifecycle state (past-due unsettled requests report as Overdue).</summary>
    public InvoiceStatus Status { get; init; }

    public DateTimeOffset? PaidAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }
}

public record CreateInvoiceRequest
{
    [Required]
    public Guid LeaseAgreementId { get; init; }

    public DateTimeOffset PeriodStart { get; init; }

    public DateTimeOffset PeriodEnd { get; init; }

    public DateTimeOffset DueDate { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [MaxLength(3)]
    public string? Currency { get; init; }

    [MaxLength(30)]
    public string? Purpose { get; init; }
}

/// <summary>
/// Raise a payment request for a lease where the amount is derived from the lease data
/// (<c>MonthlyRent</c>) — the amount is never hard-coded (FR-PAY, BRULE-PAY-001).
/// </summary>
public record GenerateRentRequestRequest
{
    [Required]
    public Guid LeaseAgreementId { get; init; }

    /// <summary>Billing period start; defaults to the current month.</summary>
    public DateTimeOffset? PeriodStart { get; init; }

    /// <summary>Billing period end; defaults to <see cref="PeriodStart"/> + 1 month.</summary>
    public DateTimeOffset? PeriodEnd { get; init; }

    /// <summary>Due date; defaults to the 10th of the period's month.</summary>
    public DateTimeOffset? DueDate { get; init; }
}

public record BalanceDto
{
    public decimal TotalOutstanding { get; init; }

    public int OpenInvoiceCount { get; init; }

    public IReadOnlyList<InvoiceDto> Invoices { get; init; } = [];
}

public record PayInvoiceRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    public PaymentMethod Method { get; init; } = PaymentMethod.Card;
}

public record PaymentTransactionDto
{
    public Guid Id { get; init; }

    public string TransactionReference { get; init; } = string.Empty;

    public Guid InvoiceId { get; init; }

    public Guid ResidentUserId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = PaymentCurrency.Default;

    public PaymentStatus Status { get; init; }

    public PaymentMethod Method { get; init; }

    public DateTimeOffset? PaidAt { get; init; }

    public string? ConfirmationNumber { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// One piece of payment attention shown by the dashboard notification bell.
/// </summary>
public record PaymentAlertDto
{
    public PaymentAlertType Type { get; init; }

    /// <summary>info | warning | critical — drives the bell's status treatment.</summary>
    public string Severity { get; init; } = "info";

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = PaymentCurrency.Default;

    public DateTimeOffset? DueDate { get; init; }

    /// <summary>The payment request to navigate to, when the alert is about one.</summary>
    public Guid? RequestId { get; init; }
}

/// <summary>
/// Everything a resident needs on their payment dashboard, computed by the backend:
/// how much is currently due, what is upcoming, what is overdue, and the full history.
/// </summary>
public record PaymentDashboardDto
{
    /// <summary>Total outstanding across all unsettled requests (FR-PAY-001).</summary>
    public decimal AmountCurrentlyDue { get; init; }

    /// <summary>Outstanding portion of requests that are past due.</summary>
    public decimal OverdueAmount { get; init; }

    /// <summary>Outstanding portion of requests that are not past due.</summary>
    public decimal UpcomingAmount { get; init; }

    public int OutstandingRequestCount { get; init; }

    public int OverdueRequestCount { get; init; }

    public DateTimeOffset? NextDueDate { get; init; }

    public bool HasOutstanding => AmountCurrentlyDue > 0;

    /// <summary>Currency of the amounts (the demo deployment is single-currency).</summary>
    public string Currency { get; init; } = PaymentCurrency.Default;

    public IReadOnlyList<InvoiceDto> Requests { get; init; } = [];

    public IReadOnlyList<PaymentTransactionDto> History { get; init; } = [];

    /// <summary>Attention items for the notification bell; empty when nothing needs action.</summary>
    public IReadOnlyList<PaymentAlertDto> Alerts { get; init; } = [];
}

/// <summary>
/// A resident with money owed — the manager/administrator view (FR-PAY-005).
/// </summary>
public record ResidentOutstandingDto
{
    public Guid ResidentUserId { get; init; }

    public string ResidentName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string UnitNumber { get; init; } = string.Empty;

    public string PropertyName { get; init; } = string.Empty;

    public decimal TotalOutstanding { get; init; }

    public decimal OverdueAmount { get; init; }

    public int OpenRequestCount { get; init; }

    public DateTimeOffset? OldestDueDate { get; init; }

    public bool HasOverdue { get; init; }

    public string Currency { get; init; } = PaymentCurrency.Default;
}

public record CancelPaymentRequestRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}

public record FinancialReportDto
{
    public decimal TotalCollected { get; init; }

    public decimal TotalOutstanding { get; init; }

    public int CompletedPayments { get; init; }

    public int FailedPayments { get; init; }

    public int OpenInvoices { get; init; }

    public int PaidInvoices { get; init; }

    public int OverdueInvoices { get; init; }

    public decimal OverdueTotal { get; init; }
}

public record ReconciliationDto
{
    public decimal ExpectedFromInvoices { get; init; }

    public decimal Collected { get; init; }

    public decimal Difference { get; init; }

    public int CompletedTransactions { get; init; }

    public int RefundedTransactions { get; init; }

    public int FailedTransactions { get; init; }

    public bool IsBalanced => Math.Abs(Difference) < 0.01m;
}

using System.ComponentModel.DataAnnotations;
using PMP.Modules.Payment.Enums;

namespace PMP.Modules.Payment.Contracts;

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

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? PaidAt { get; init; }
}

public record CreateInvoiceRequest
{
    [Required]
    public Guid LeaseAgreementId { get; init; }

    public DateTimeOffset PeriodStart { get; init; }

    public DateTimeOffset PeriodEnd { get; init; }

    public DateTimeOffset DueDate { get; init; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; init; }
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

    public PaymentStatus Status { get; init; }

    public PaymentMethod Method { get; init; }

    public DateTimeOffset? PaidAt { get; init; }

    public string? ConfirmationNumber { get; init; }
}

public record FinancialReportDto
{
    public decimal TotalCollected { get; init; }

    public decimal TotalOutstanding { get; init; }

    public int CompletedPayments { get; init; }

    public int FailedPayments { get; init; }

    public int OpenInvoices { get; init; }

    public int PaidInvoices { get; init; }

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

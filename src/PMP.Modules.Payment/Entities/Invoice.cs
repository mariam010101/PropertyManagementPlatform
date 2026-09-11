using PMP.Modules.Payment.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Payment.Entities;

/// <summary>
/// A payment request/order a resident is required to pay (BR-005 / FR-PAY-001..007).
///
/// <para>Raised from a lease (BRULE-PAY-001) and identified by the resident it is owed
/// by, the amount, the currency, the purpose, the due date, the status and the creation
/// date (<see cref="BaseEntity.CreatedAt"/>). The outstanding balance is the amount minus
/// the sum of completed payments (FR-PAY-001, BRULE-PAY-006).</para>
///
/// <para>Requests are append-only: settled and cancelled requests are retained so the
/// resident can always see what was expected and what happened (BRULE-PAY-003).</para>
/// </summary>
public class Invoice : BaseEntity
{
    public Guid LeaseAgreementId { get; set; }

    public Guid ResidentUserId { get; set; }

    /// <summary>Denormalized unit/property ids for reporting + manager scoping.</summary>
    public Guid UnitId { get; set; }

    public Guid PropertyId { get; set; }

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public DateTimeOffset DueDate { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency the amount is expressed in. Defaults to USD.</summary>
    public string Currency { get; set; } = PaymentCurrency.Default;

    /// <summary>
    /// Why the payment is owed — one of <see cref="PaymentPurpose"/> (Rent, Utilities, …).
    /// </summary>
    public string Purpose { get; set; } = PaymentPurpose.Rent;

    public decimal PaidAmount { get; set; }

    /// <summary>Lifecycle state — see <see cref="InvoiceStatus"/>.</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? DueDateReminderSentAt { get; set; }

    /// <summary>Set once when the request first became overdue, to avoid reminder spam.</summary>
    public DateTimeOffset? OverdueNotifiedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();

    /// <summary>Outstanding balance = amount − paid (never negative).</summary>
    public decimal OutstandingBalance => Math.Max(0m, Amount - PaidAmount);

    /// <summary>
    /// Effective status for display/decisioning: an unsettled request whose due date has
    /// passed is overdue even before the daily sweep persists that transition.
    /// </summary>
    public InvoiceStatus GetEffectiveStatus(DateTimeOffset now) =>
        Status is InvoiceStatus.Open or InvoiceStatus.PartiallyPaid && DueDate < now
            ? InvoiceStatus.Overdue
            : Status;
}

/// <summary>Small helper for the default currency used by the demo deployment.</summary>
public static class PaymentCurrency
{
    public const string Default = "USD";
}

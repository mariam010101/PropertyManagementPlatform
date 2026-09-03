using PMP.Modules.Payment.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Payment.Entities;

/// <summary>
/// An immutable payment transaction (BRULE-PAY-002/003/004). Each transaction has a
/// unique reference and a recorded status; processed records are never deleted.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    /// <summary>Unique transaction identifier (BRULE-PAY-002).</summary>
    public string TransactionReference { get; set; } = string.Empty;

    public Guid InvoiceId { get; set; }

    public Guid ResidentUserId { get; set; }

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public PaymentMethod Method { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Confirmation reference returned to the resident (BRULE-PAY-005).</summary>
    public string? ConfirmationNumber { get; set; }

    public string? FailureReason { get; set; }
}

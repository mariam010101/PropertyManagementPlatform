using PMP.Modules.Payment.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Payment.Entities;

/// <summary>
/// A rent invoice generated from a lease (BRULE-PAY-001). Outstanding balance is
/// amount minus the sum of completed payments (FR-PAY-001, BRULE-PAY-006).
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

    public decimal PaidAmount { get; set; }

    /// <summary>Open / PartiallyPaid / Paid / Overdue / Voided.</summary>
    public string Status { get; set; } = "Open";

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? DueDateReminderSentAt { get; set; }

    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
}

using PMP.Shared.Domain;

namespace PMP.Modules.Lease.Entities;

/// <summary>
/// A versioned history record for a lease (FR-LEASE-004, BRULE-LEASE-005). Each
/// modification is captured so prior terms remain auditable.
/// </summary>
public class LeaseHistoryEntry : BaseEntity
{
    public Guid LeaseAgreementId { get; set; }

    public int Version { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid ChangedByUserId { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public LeaseAgreement? LeaseAgreement { get; set; }
}

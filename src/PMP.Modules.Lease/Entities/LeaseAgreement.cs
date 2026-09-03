using PMP.Modules.Lease.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Lease.Entities;

/// <summary>
/// A lease binds one resident to one residential unit with a start/end date
/// (BRULE-LEASE-002/003). Modifications create new versions (BRULE-LEASE-005);
/// expired leases are retained for history (BRULE-LEASE-006, FR-LEASE-007).
/// </summary>
public class LeaseAgreement : BaseEntity
{
    /// <summary>Auth user id of the resident/tenant.</summary>
    public Guid ResidentUserId { get; set; }

    /// <summary>Residential unit this lease is bound to (BRULE-LEASE-002).</summary>
    public Guid UnitId { get; set; }

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }

    public decimal MonthlyRent { get; set; }

    public LeaseStatus Status { get; set; } = LeaseStatus.Active;

    /// <summary>Current version number (increments on each modification).</summary>
    public int CurrentVersion { get; set; } = 1;

    public string? TerminationComment { get; set; }

    public DateTimeOffset? TerminatedAt { get; set; }

    public ICollection<LeaseDocument> Documents { get; set; } = new List<LeaseDocument>();

    public ICollection<LeaseHistoryEntry> History { get; set; } = new List<LeaseHistoryEntry>();
}

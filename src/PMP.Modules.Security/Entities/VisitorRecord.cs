using PMP.Modules.Security.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Security.Entities;

/// <summary>
/// A visitor registration with check-in/check-out times (FR-SEC-001..003). Records are
/// retained so visitor entries/exits form an audit log (FR-SEC-005/006).
/// </summary>
public class VisitorRecord : BaseEntity
{
    public Guid PropertyId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary>Resident (user) the visitor is visiting, if known.</summary>
    public Guid? HostUserId { get; set; }

    public string? UnitNumber { get; set; }

    public VisitorStatus Status { get; set; } = VisitorStatus.Registered;

    /// <summary>Security/staff user who registered the visitor (FR-SEC-001).</summary>
    public Guid RegisteredByUserId { get; set; }

    public DateTimeOffset? CheckInAt { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    public string? Notes { get; set; }
}

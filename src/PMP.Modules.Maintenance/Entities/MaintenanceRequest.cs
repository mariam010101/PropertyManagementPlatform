using PMP.Modules.Maintenance.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Maintenance.Entities;

/// <summary>
/// A maintenance request (FR-MNT-001..008). Exactly one current status
/// (BRULE-MNT-002).
/// </summary>
public class MaintenanceRequest : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Submitted;

    /// <summary>Resident profile that submitted the request (resident schema).</summary>
    public Guid RequestedByResidentId { get; set; }

    /// <summary>Residential unit the request relates to (property schema).</summary>
    public Guid UnitId { get; set; }

    /// <summary>Assigned technician (auth.users id), if any.</summary>
    public Guid? AssignedToUserId { get; set; }

    public DateTimeOffset? AssignedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public ICollection<MaintenanceAttachment> Attachments { get; set; } = new List<MaintenanceAttachment>();

    public ICollection<MaintenanceHistoryEntry> History { get; set; } = new List<MaintenanceHistoryEntry>();
}

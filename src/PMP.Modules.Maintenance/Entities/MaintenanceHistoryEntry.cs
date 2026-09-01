using PMP.Modules.Maintenance.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Maintenance.Entities;

/// <summary>
/// A status-change record maintaining the full request history (FR-MNT-006).
/// </summary>
public class MaintenanceHistoryEntry : BaseEntity
{
    public Guid MaintenanceRequestId { get; set; }

    public MaintenanceStatus? FromStatus { get; set; }

    public MaintenanceStatus ToStatus { get; set; }

    public string? Comment { get; set; }

    public Guid ChangedByUserId { get; set; }

    public MaintenanceRequest? Request { get; set; }
}

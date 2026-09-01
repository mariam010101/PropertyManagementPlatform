using PMP.Shared.Domain;

namespace PMP.Modules.Maintenance.Entities;

/// <summary>An image/attachment on a maintenance request (FR-MNT-002).</summary>
public class MaintenanceAttachment : BaseEntity
{
    public Guid MaintenanceRequestId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }

    public MaintenanceRequest? Request { get; set; }
}

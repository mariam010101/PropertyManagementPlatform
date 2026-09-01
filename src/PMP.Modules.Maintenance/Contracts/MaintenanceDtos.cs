using System.ComponentModel.DataAnnotations;
using PMP.Modules.Maintenance.Enums;

namespace PMP.Modules.Maintenance.Contracts;

public record MaintenanceRequestDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public MaintenancePriority Priority { get; init; }

    public MaintenanceStatus Status { get; init; }

    public Guid RequestedByResidentId { get; init; }

    public Guid UnitId { get; init; }

    public string? UnitNumber { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public string? AssignedToName { get; init; }

    public DateTimeOffset? AssignedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public string? CancellationReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public int AttachmentCount { get; init; }

    public IReadOnlyList<MaintenanceAttachmentDto> Attachments { get; init; } = Array.Empty<MaintenanceAttachmentDto>();

    public IReadOnlyList<MaintenanceHistoryDto> History { get; init; } = Array.Empty<MaintenanceHistoryDto>();
}

public record MaintenanceAttachmentDto
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public DateTimeOffset UploadedAt { get; init; }
}

public record MaintenanceHistoryDto
{
    public Guid Id { get; init; }

    public MaintenanceStatus? FromStatus { get; init; }

    public MaintenanceStatus ToStatus { get; init; }

    public string? Comment { get; init; }

    public Guid ChangedByUserId { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}

public record CreateMaintenanceRequestRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    public Guid UnitId { get; init; }

    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
}

public record AssignMaintenanceRequest
{
    [Required]
    public Guid TechnicianUserId { get; init; }

    [MaxLength(500)]
    public string? Comment { get; init; }
}

public record UpdateMaintenanceStatusRequest
{
    [Required]
    public MaintenanceStatus Status { get; init; }

    [MaxLength(1000)]
    public string? Comment { get; init; }
}

public record SetMaintenancePriorityRequest
{
    [Required]
    public MaintenancePriority Priority { get; init; }
}

public record ConfirmCompletionRequest
{
    [MaxLength(1000)]
    public string? Comment { get; init; }
}

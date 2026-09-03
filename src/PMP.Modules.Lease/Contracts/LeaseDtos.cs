using System.ComponentModel.DataAnnotations;
using PMP.Modules.Lease.Enums;

namespace PMP.Modules.Lease.Contracts;

public record LeaseDto
{
    public Guid Id { get; init; }

    public Guid ResidentUserId { get; init; }

    public string ResidentName { get; init; } = string.Empty;

    public Guid UnitId { get; init; }

    public string UnitNumber { get; init; } = string.Empty;

    public Guid PropertyId { get; init; }

    public string PropertyName { get; init; } = string.Empty;

    public DateTimeOffset StartDate { get; init; }

    public DateTimeOffset EndDate { get; init; }

    public decimal MonthlyRent { get; init; }

    public LeaseStatus Status { get; init; }

    public int CurrentVersion { get; init; }

    public DateTimeOffset? TerminatedAt { get; init; }

    public int DocumentCount { get; init; }
}

public record CreateLeaseRequest
{
    [Required]
    public Guid ResidentUserId { get; init; }

    [Required]
    public Guid UnitId { get; init; }

    public DateTimeOffset StartDate { get; init; }

    public DateTimeOffset EndDate { get; init; }

    [Range(0, double.MaxValue)]
    public decimal MonthlyRent { get; init; }
}

public record UpdateLeaseRequest
{
    public DateTimeOffset StartDate { get; init; }

    public DateTimeOffset EndDate { get; init; }

    [Range(0, double.MaxValue)]
    public decimal MonthlyRent { get; init; }
}

public record TerminateLeaseRequest
{
    [MaxLength(500)]
    public string? Comment { get; init; }
}

public record LeaseDocumentDto
{
    public Guid Id { get; init; }

    public Guid LeaseAgreementId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public Guid UploadedByUserId { get; init; }

    public int Version { get; init; }

    public DateTimeOffset UploadedAt { get; init; }
}

public record LeaseHistoryDto
{
    public int Version { get; init; }

    public string ChangeType { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public Guid ChangedByUserId { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}

public record LeaseDetailDto : LeaseDto
{
    public IReadOnlyList<LeaseDocumentDto> Documents { get; init; } = [];

    public IReadOnlyList<LeaseHistoryDto> History { get; init; } = [];
}

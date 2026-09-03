using System.ComponentModel.DataAnnotations;
using PMP.Modules.Security.Enums;

namespace PMP.Modules.Security.Contracts;

public record VisitorDto
{
    public Guid Id { get; init; }

    public Guid PropertyId { get; init; }

    public string PropertyName { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string? PhoneNumber { get; init; }

    public Guid? HostUserId { get; init; }

    public string? UnitNumber { get; init; }

    public VisitorStatus Status { get; init; }

    public Guid RegisteredByUserId { get; init; }

    public DateTimeOffset? CheckInAt { get; init; }

    public DateTimeOffset? CheckOutAt { get; init; }

    public string? Notes { get; init; }
}

public record RegisterVisitorRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Phone, MaxLength(32)]
    public string? PhoneNumber { get; init; }

    [Required]
    public Guid PropertyId { get; init; }

    public Guid? HostUserId { get; init; }

    [MaxLength(30)]
    public string? UnitNumber { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record CheckInVisitorRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record AccessGrantDto
{
    public Guid Id { get; init; }

    public Guid PropertyId { get; init; }

    public string PropertyName { get; init; } = string.Empty;

    public AccessTargetType TargetType { get; init; }

    public Guid TargetId { get; init; }

    public string TargetName { get; init; } = string.Empty;

    public AccessSubjectType SubjectType { get; init; }

    public Guid? SubjectUserId { get; init; }

    public string SubjectName { get; init; } = string.Empty;

    public Guid? VisitorId { get; init; }

    public Guid GrantedByUserId { get; init; }

    public DateTimeOffset GrantedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public bool IsActive { get; init; }
}

public record GrantAccessRequest
{
    [Required]
    public Guid PropertyId { get; init; }

    public AccessTargetType TargetType { get; init; }

    [Required]
    public Guid TargetId { get; init; }

    public AccessSubjectType SubjectType { get; init; }

    /// <summary>Required when SubjectType == User.</summary>
    public Guid? SubjectUserId { get; init; }

    /// <summary>Required when SubjectType == Visitor.</summary>
    public Guid? VisitorId { get; init; }

    /// <summary>Optional expiration for temporary access (FR-SEC-007).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

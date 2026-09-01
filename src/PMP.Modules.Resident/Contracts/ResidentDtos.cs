using System.ComponentModel.DataAnnotations;

namespace PMP.Modules.Resident.Contracts;

public record ResidentProfileDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Current residential unit, if any.</summary>
    public Guid? CurrentUnitId { get; init; }

    public string? CurrentUnitNumber { get; init; }

    public DateTimeOffset? CurrentMoveInDate { get; init; }
}

public record UpdateResidentProfileRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Phone, MaxLength(32)]
    public string? PhoneNumber { get; init; }
}

public record AssignUnitRequest
{
    [Required]
    public Guid UnitId { get; init; }

    public DateTimeOffset? MoveInDate { get; init; }
}

public record MoveOutRequest
{
    public DateTimeOffset? MoveOutDate { get; init; }
}

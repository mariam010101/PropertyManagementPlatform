using System.ComponentModel.DataAnnotations;
using PMP.Modules.Property.Enums;

namespace PMP.Modules.Property.Contracts;

public record PropertyDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string? City { get; init; }

    public string? Description { get; init; }

    public Guid ManagerUserId { get; init; }

    public int BuildingCount { get; init; }

    public int UnitCount { get; init; }
}

public record CreatePropertyRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    public Guid ManagerUserId { get; init; }
}

public record UpdatePropertyRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    public Guid ManagerUserId { get; init; }
}

public record BuildingDto
{
    public Guid Id { get; init; }

    public Guid PropertyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Address { get; init; }

    public int? Floors { get; init; }

    public int UnitCount { get; init; }
}

public record CreateBuildingRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; init; }

    public int? Floors { get; init; }
}

public record UpdateBuildingRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; init; }

    public int? Floors { get; init; }
}

public record UnitDto
{
    public Guid Id { get; init; }

    public Guid BuildingId { get; init; }

    public Guid PropertyId { get; init; }

    public string UnitNumber { get; init; } = string.Empty;

    public string UnitType { get; init; } = string.Empty;

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public double? AreaSqM { get; init; }

    public UnitOperationalStatus OperationalStatus { get; init; }

    public string? Notes { get; init; }

    /// <summary>Derived occupancy (FR-PROP-004): true when the unit has active residents.</summary>
    public bool IsOccupied { get; init; }
}

public record CreateUnitRequest
{
    [Required, MaxLength(30)]
    public string UnitNumber { get; init; } = string.Empty;

    [MaxLength(50)]
    public string UnitType { get; init; } = string.Empty;

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public double? AreaSqM { get; init; }

    public UnitOperationalStatus OperationalStatus { get; init; } = UnitOperationalStatus.Active;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record UpdateUnitRequest
{
    [Required, MaxLength(30)]
    public string UnitNumber { get; init; } = string.Empty;

    [MaxLength(50)]
    public string UnitType { get; init; } = string.Empty;

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public double? AreaSqM { get; init; }

    public UnitOperationalStatus OperationalStatus { get; init; } = UnitOperationalStatus.Active;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

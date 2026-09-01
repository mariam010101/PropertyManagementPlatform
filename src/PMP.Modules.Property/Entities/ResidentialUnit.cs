using PMP.Modules.Property.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Property.Entities;

/// <summary>
/// A rentable residential unit. Every unit belongs to one building
/// (BRULE-PROP-002).
/// </summary>
public class ResidentialUnit : BaseEntity
{
    public Guid BuildingId { get; set; }

    public string UnitNumber { get; set; } = string.Empty;

    /// <summary>e.g. Studio, OneBedroom, TwoBedroom, Penthouse.</summary>
    public string UnitType { get; set; } = string.Empty;

    public int? Bedrooms { get; set; }

    public int? Bathrooms { get; set; }

    public double? AreaSqM { get; set; }

    /// <summary>Operational status (FR-PROP-006); occupancy is derived, not stored.</summary>
    public UnitOperationalStatus OperationalStatus { get; set; } = UnitOperationalStatus.Active;

    public string? Notes { get; set; }

    public Building? Building { get; set; }
}

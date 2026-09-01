using PMP.Shared.Domain;

namespace PMP.Modules.Resident.Entities;

/// <summary>
/// Effective-dated resident-to-unit association (FR-RES-004/005). One row per
/// resident-unit period. A unit may have multiple active residents
/// (BRULE-RES-002).
/// </summary>
public class ResidentUnit : BaseEntity
{
    public Guid ResidentProfileId { get; set; }

    /// <summary>Residential unit id (property.residential_units).</summary>
    public Guid UnitId { get; set; }

    public DateTimeOffset MoveInDate { get; set; }

    /// <summary>Null while the resident is currently occupying the unit.</summary>
    public DateTimeOffset? MoveOutDate { get; set; }

    public ResidentProfile? ResidentProfile { get; set; }

    public bool IsCurrent => MoveOutDate == null;
}

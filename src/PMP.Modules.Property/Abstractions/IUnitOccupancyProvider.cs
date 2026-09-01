namespace PMP.Modules.Property.Abstractions;

/// <summary>
/// Provides derived occupancy for residential units. Implemented outside the
/// Property module (the Resident module owns resident-unit associations) to
/// keep the dependency direction correct in the modular monolith.
/// </summary>
public interface IUnitOccupancyProvider
{
    /// <summary>
    /// Returns a map of unit id to a boolean indicating whether the unit has at
    /// least one active resident.
    /// </summary>
    Task<Dictionary<Guid, bool>> GetOccupancyAsync(IEnumerable<Guid> unitIds);
}

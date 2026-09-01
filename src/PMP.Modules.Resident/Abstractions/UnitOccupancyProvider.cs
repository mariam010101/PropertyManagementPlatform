using Microsoft.EntityFrameworkCore;
using PMP.Modules.Property.Abstractions;
using PMP.Modules.Resident.Data;

namespace PMP.Modules.Resident.Abstractions;

/// <summary>
/// Implements the Property module's occupancy contract using resident-unit
/// associations (FR-PROP-004: occupancy is derived, not stored).
/// </summary>
public class UnitOccupancyProvider : IUnitOccupancyProvider
{
    private readonly ResidentDbContext _db;

    public UnitOccupancyProvider(ResidentDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<Guid, bool>> GetOccupancyAsync(IEnumerable<Guid> unitIds)
    {
        var ids = unitIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        var occupied = await _db.ResidentUnits
            .AsNoTracking()
            .Where(ru => ids.Contains(ru.UnitId) &&
                         ru.MoveOutDate == null &&
                         !ru.IsDeleted &&
                         ru.ResidentProfile != null &&
                         ru.ResidentProfile.IsActive &&
                         !ru.ResidentProfile.IsDeleted)
            .Select(ru => ru.UnitId)
            .Distinct()
            .ToListAsync();

        return ids.ToDictionary(id => id, id => occupied.Contains(id));
    }
}

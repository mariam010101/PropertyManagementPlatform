using Microsoft.EntityFrameworkCore;
using PMP.Modules.Property.Abstractions;
using PMP.Modules.Property.Contracts;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Shared.Common;

namespace PMP.Modules.Property.Services;

public interface IPropertyService
{
    Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(Guid actorId, IReadOnlyList<string> roles);

    Task<Result<PropertyDto>> GetPropertyAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId);

    Task<Result<PropertyDto>> CreatePropertyAsync(Guid actorId, CreatePropertyRequest request);

    Task<Result<PropertyDto>> UpdatePropertyAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId, UpdatePropertyRequest request);

    Task<IReadOnlyList<BuildingDto>> GetBuildingsAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId);

    Task<Result<BuildingDto>> CreateBuildingAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId, CreateBuildingRequest request);

    Task<Result<BuildingDto>> UpdateBuildingAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId, Guid buildingId, UpdateBuildingRequest request);

    Task<IReadOnlyList<UnitDto>> GetUnitsAsync(Guid actorId, IReadOnlyList<string> roles, Guid buildingId);

    Task<Result<UnitDto>> GetUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid unitId);

    Task<Result<UnitDto>> CreateUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid buildingId, CreateUnitRequest request);

    Task<Result<UnitDto>> UpdateUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid unitId, UpdateUnitRequest request);
}

public class PropertyService : IPropertyService
{
    private readonly PropertyDbContext _db;
    private readonly IUnitOccupancyProvider _occupancy;

    public PropertyService(PropertyDbContext db, IUnitOccupancyProvider occupancy)
    {
        _db = db;
        _occupancy = occupancy;
    }

    public async Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        return await Scoped(actorId, roles)
            .Select(p => new PropertyDto
            {
                Id = p.Id,
                Name = p.Name,
                Address = p.Address,
                City = p.City,
                Description = p.Description,
                ManagerUserId = p.ManagerUserId,
                BuildingCount = p.Buildings.Count,
                UnitCount = p.Buildings.Sum(b => b.Units.Count),
            })
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Result<PropertyDto>> GetPropertyAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId)
    {
        var property = await Scoped(actorId, roles)
            .Where(p => p.Id == propertyId)
            .Select(p => new PropertyDto
            {
                Id = p.Id,
                Name = p.Name,
                Address = p.Address,
                City = p.City,
                Description = p.Description,
                ManagerUserId = p.ManagerUserId,
                BuildingCount = p.Buildings.Count,
                UnitCount = p.Buildings.Sum(b => b.Units.Count),
            })
            .SingleOrDefaultAsync();

        return property is null
            ? Result.Fail<PropertyDto>("Property not found or you do not have access to it.")
            : Result.Ok(property);
    }

    public async Task<Result<PropertyDto>> CreatePropertyAsync(Guid actorId, CreatePropertyRequest request)
    {
        var entity = new ManagedProperty
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            Description = request.Description,
            ManagerUserId = request.ManagerUserId,
        };

        _db.Properties.Add(entity);
        await _db.SaveChangesAsync();

        return Result.Ok(new PropertyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Address = entity.Address,
            City = entity.City,
            Description = entity.Description,
            ManagerUserId = entity.ManagerUserId,
        });
    }

    public async Task<Result<PropertyDto>> UpdatePropertyAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId, UpdatePropertyRequest request)
    {
        var property = await Scoped(actorId, roles).SingleOrDefaultAsync(p => p.Id == propertyId);
        if (property is null)
        {
            return Result.Fail<PropertyDto>("Property not found or you do not have access to it.");
        }

        property.Name = request.Name;
        property.Address = request.Address;
        property.City = request.City;
        property.Description = request.Description;
        property.ManagerUserId = request.ManagerUserId;
        property.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Result.Ok(new PropertyDto
        {
            Id = property.Id,
            Name = property.Name,
            Address = property.Address,
            City = property.City,
            Description = property.Description,
            ManagerUserId = property.ManagerUserId,
        });
    }

    public async Task<IReadOnlyList<BuildingDto>> GetBuildingsAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId)
    {
        if (!await CanAccessPropertyAsync(actorId, roles, propertyId))
        {
            return Array.Empty<BuildingDto>();
        }

        return await _db.Buildings
            .Where(b => b.PropertyId == propertyId)
            .Select(b => new BuildingDto
            {
                Id = b.Id,
                PropertyId = b.PropertyId,
                Name = b.Name,
                Address = b.Address,
                Floors = b.Floors,
                UnitCount = b.Units.Count,
            })
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<Result<BuildingDto>> CreateBuildingAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId, CreateBuildingRequest request)
    {
        if (!await CanAccessPropertyAsync(actorId, roles, propertyId))
        {
            return Result.Fail<BuildingDto>("Property not found or you do not have access to it.");
        }

        var building = new Building
        {
            PropertyId = propertyId,
            Name = request.Name,
            Address = request.Address,
            Floors = request.Floors,
        };

        _db.Buildings.Add(building);
        await _db.SaveChangesAsync();

        return Result.Ok(new BuildingDto
        {
            Id = building.Id,
            PropertyId = building.PropertyId,
            Name = building.Name,
            Address = building.Address,
            Floors = building.Floors,
        });
    }

    public async Task<Result<BuildingDto>> UpdateBuildingAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId, Guid buildingId, UpdateBuildingRequest request)
    {
        if (!await CanAccessPropertyAsync(actorId, roles, propertyId))
        {
            return Result.Fail<BuildingDto>("Property not found or you do not have access to it.");
        }

        var building = await _db.Buildings.SingleOrDefaultAsync(b => b.Id == buildingId && b.PropertyId == propertyId);
        if (building is null)
        {
            return Result.Fail<BuildingDto>("Building not found.");
        }

        building.Name = request.Name;
        building.Address = request.Address;
        building.Floors = request.Floors;
        building.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Result.Ok(new BuildingDto
        {
            Id = building.Id,
            PropertyId = building.PropertyId,
            Name = building.Name,
            Address = building.Address,
            Floors = building.Floors,
        });
    }

    public async Task<IReadOnlyList<UnitDto>> GetUnitsAsync(Guid actorId, IReadOnlyList<string> roles, Guid buildingId)
    {
        var building = await _db.Buildings
            .Where(b => b.Id == buildingId)
            .Select(b => new { b.Id, b.PropertyId })
            .SingleOrDefaultAsync();

        if (building is null || !await CanAccessPropertyAsync(actorId, roles, building.PropertyId))
        {
            return Array.Empty<UnitDto>();
        }

        var units = await _db.ResidentialUnits
            .Where(u => u.BuildingId == buildingId)
            .OrderBy(u => u.UnitNumber)
            .ToListAsync();

        return await ToDtosAsync(units, building.PropertyId);
    }

    public async Task<Result<UnitDto>> GetUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid unitId)
    {
        var unit = await _db.ResidentialUnits
            .Include(u => u.Building)
            .SingleOrDefaultAsync(u => u.Id == unitId);

        if (unit is null || !await CanAccessPropertyAsync(actorId, roles, unit.Building!.PropertyId))
        {
            return Result.Fail<UnitDto>("Unit not found or you do not have access to it.");
        }

        var dtos = await ToDtosAsync([unit], unit.Building.PropertyId);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<UnitDto>> CreateUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid buildingId, CreateUnitRequest request)
    {
        var building = await _db.Buildings
            .Where(b => b.Id == buildingId)
            .Select(b => new { b.Id, b.PropertyId })
            .SingleOrDefaultAsync();

        if (building is null || !await CanAccessPropertyAsync(actorId, roles, building.PropertyId))
        {
            return Result.Fail<UnitDto>("Building not found or you do not have access to it.");
        }

        var duplicate = await _db.ResidentialUnits
            .AnyAsync(u => u.BuildingId == buildingId && u.UnitNumber == request.UnitNumber);
        if (duplicate)
        {
            return Result.Fail<UnitDto>("A unit with this number already exists in the building.");
        }

        var unit = new ResidentialUnit
        {
            BuildingId = buildingId,
            UnitNumber = request.UnitNumber,
            UnitType = request.UnitType,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            AreaSqM = request.AreaSqM,
            OperationalStatus = request.OperationalStatus,
            Notes = request.Notes,
        };

        _db.ResidentialUnits.Add(unit);
        await _db.SaveChangesAsync();

        return Result.Ok(new UnitDto
        {
            Id = unit.Id,
            BuildingId = unit.BuildingId,
            PropertyId = building.PropertyId,
            UnitNumber = unit.UnitNumber,
            UnitType = unit.UnitType,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            AreaSqM = unit.AreaSqM,
            OperationalStatus = unit.OperationalStatus,
            Notes = unit.Notes,
            IsOccupied = false,
        });
    }

    public async Task<Result<UnitDto>> UpdateUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid unitId, UpdateUnitRequest request)
    {
        var unit = await _db.ResidentialUnits
            .Include(u => u.Building)
            .SingleOrDefaultAsync(u => u.Id == unitId);

        if (unit is null || !await CanAccessPropertyAsync(actorId, roles, unit.Building!.PropertyId))
        {
            return Result.Fail<UnitDto>("Unit not found or you do not have access to it.");
        }

        unit.UnitNumber = request.UnitNumber;
        unit.UnitType = request.UnitType;
        unit.Bedrooms = request.Bedrooms;
        unit.Bathrooms = request.Bathrooms;
        unit.AreaSqM = request.AreaSqM;
        unit.OperationalStatus = request.OperationalStatus;
        unit.Notes = request.Notes;
        unit.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        var dtos = await ToDtosAsync([unit], unit.Building.PropertyId);
        return Result.Ok(dtos[0]);
    }

    // ---- helpers ----

    private IQueryable<ManagedProperty> Scoped(Guid actorId, IReadOnlyList<string> roles)
    {
        return roles.Contains(AppRoles.Administrator)
            ? _db.Properties.AsNoTracking()
            : _db.Properties.AsNoTracking().Where(p => p.ManagerUserId == actorId);
    }

    private async Task<bool> CanAccessPropertyAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId)
    {
        var property = await _db.Properties
            .Where(p => p.Id == propertyId)
            .Select(p => new { p.ManagerUserId })
            .SingleOrDefaultAsync();

        return property is not null &&
               (roles.Contains(AppRoles.Administrator) || property.ManagerUserId == actorId);
    }

    private async Task<IReadOnlyList<UnitDto>> ToDtosAsync(IEnumerable<ResidentialUnit> units, Guid propertyId)
    {
        var list = units.ToList();
        var occupancy = await _occupancy.GetOccupancyAsync(list.Select(u => u.Id));

        return list.Select(u => new UnitDto
        {
            Id = u.Id,
            BuildingId = u.BuildingId,
            PropertyId = propertyId,
            UnitNumber = u.UnitNumber,
            UnitType = u.UnitType,
            Bedrooms = u.Bedrooms,
            Bathrooms = u.Bathrooms,
            AreaSqM = u.AreaSqM,
            OperationalStatus = u.OperationalStatus,
            Notes = u.Notes,
            IsOccupied = occupancy.TryGetValue(u.Id, out var occupied) && occupied,
        }).ToList();
    }
}

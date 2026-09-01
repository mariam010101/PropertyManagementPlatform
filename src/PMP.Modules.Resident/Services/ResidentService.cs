using Microsoft.EntityFrameworkCore;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Contracts;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Modules.Resident.Services;

public interface IResidentService
{
    Task<Result> CreateProfileForUserAsync(Guid userId, string email, string firstName, string lastName, string? phoneNumber);

    Task<IReadOnlyList<ResidentProfileDto>> GetResidentsAsync(Guid actorId, IReadOnlyList<string> roles, string? search, bool? activeOnly);

    Task<Result<ResidentProfileDto>> GetResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId);

    Task<Result<ResidentProfileDto>> GetMyProfileAsync(Guid userId);

    Task<Result<ResidentProfileDto>> UpdateResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId, UpdateResidentProfileRequest request);

    Task<Result<ResidentProfileDto>> AssignUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId, AssignUnitRequest request);

    Task<Result> MoveOutAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId, MoveOutRequest request);

    Task<Result> DeactivateResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId);
}

public class ResidentService : IResidentService
{
    private readonly ResidentDbContext _db;
    private readonly PropertyDbContext _propertyDb;

    public ResidentService(ResidentDbContext db, PropertyDbContext propertyDb)
    {
        _db = db;
        _propertyDb = propertyDb;
    }

    public async Task<Result> CreateProfileForUserAsync(Guid userId, string email, string firstName, string lastName, string? phoneNumber)
    {
        var exists = await _db.ResidentProfiles.AnyAsync(r => r.UserId == userId);
        if (exists)
        {
            return Result.Ok();
        }

        var profile = new ResidentProfile
        {
            UserId = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            IsActive = true,
        };

        _db.ResidentProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ResidentProfileDto>> GetResidentsAsync(Guid actorId, IReadOnlyList<string> roles, string? search, bool? activeOnly)
    {
        var managedUnitIds = await ManagedUnitIdsAsync(actorId, roles);

        var query = _db.ResidentProfiles
            .AsNoTracking()
            .Where(r => !r.IsDeleted);

        if (!roles.Contains(AppRoles.Administrator))
        {
            // Property managers only see residents linked to units they manage.
            query = query.Where(r => _db.ResidentUnits.Any(ru =>
                ru.ResidentProfileId == r.Id &&
                ru.MoveOutDate == null &&
                !ru.IsDeleted &&
                managedUnitIds.Contains(ru.UnitId)));
        }

        if (activeOnly == true)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r =>
                r.FirstName.ToLower().Contains(term) ||
                r.LastName.ToLower().Contains(term) ||
                r.Email.ToLower().Contains(term));
        }

        var residents = await query
            .OrderBy(r => r.LastName).ThenBy(r => r.FirstName)
            .ToListAsync();

        return await ToDtosAsync(residents);
    }

    public async Task<Result<ResidentProfileDto>> GetResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId)
    {
        var resident = await _db.ResidentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == residentId && !r.IsDeleted);

        if (resident is null || !await CanAccessResidentAsync(actorId, roles, residentId))
        {
            return Result.Fail<ResidentProfileDto>("Resident not found or you do not have access to it.");
        }

        var dtos = await ToDtosAsync([resident]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<ResidentProfileDto>> GetMyProfileAsync(Guid userId)
    {
        var resident = await _db.ResidentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && !r.IsDeleted);

        if (resident is null)
        {
            return Result.Fail<ResidentProfileDto>("Resident profile not found.");
        }

        var dtos = await ToDtosAsync([resident]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<ResidentProfileDto>> UpdateResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId, UpdateResidentProfileRequest request)
    {
        var resident = await _db.ResidentProfiles.SingleOrDefaultAsync(r => r.Id == residentId && !r.IsDeleted);
        if (resident is null || !await CanAccessResidentAsync(actorId, roles, residentId))
        {
            return Result.Fail<ResidentProfileDto>("Resident not found or you do not have access to it.");
        }

        resident.FirstName = request.FirstName;
        resident.LastName = request.LastName;
        resident.PhoneNumber = request.PhoneNumber;
        resident.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        var dtos = await ToDtosAsync([resident]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<ResidentProfileDto>> AssignUnitAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId, AssignUnitRequest request)
    {
        var resident = await _db.ResidentProfiles.SingleOrDefaultAsync(r => r.Id == residentId && !r.IsDeleted);
        if (resident is null || !await CanAccessResidentAsync(actorId, roles, residentId))
        {
            return Result.Fail<ResidentProfileDto>("Resident not found or you do not have access to it.");
        }

        var unit = await _propertyDb.ResidentialUnits
            .Include(u => u.Building)
            .SingleOrDefaultAsync(u => u.Id == request.UnitId);

        if (unit is null)
        {
            return Result.Fail<ResidentProfileDto>("Unit not found.");
        }

        if (unit.OperationalStatus != UnitOperationalStatus.Active)
        {
            return Result.Fail<ResidentProfileDto>("Unit is not available for assignment.");
        }

        if (!roles.Contains(AppRoles.Administrator) && unit.Building!.Property!.ManagerUserId != actorId)
        {
            return Result.Fail<ResidentProfileDto>("You do not have access to this unit.");
        }

        var moveIn = request.MoveInDate ?? DateTimeOffset.UtcNow;

        // End any current association (unit transfer) and create a new one.
        var current = await _db.ResidentUnits
            .Where(ru => ru.ResidentProfileId == residentId && ru.MoveOutDate == null && !ru.IsDeleted)
            .ToListAsync();

        foreach (var association in current)
        {
            association.MoveOutDate = moveIn;
            association.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _db.ResidentUnits.Add(new ResidentUnit
        {
            ResidentProfileId = residentId,
            UnitId = unit.Id,
            MoveInDate = moveIn,
        });

        await _db.SaveChangesAsync();

        var dtos = await ToDtosAsync([resident]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result> MoveOutAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId, MoveOutRequest request)
    {
        var resident = await _db.ResidentProfiles.SingleOrDefaultAsync(r => r.Id == residentId && !r.IsDeleted);
        if (resident is null || !await CanAccessResidentAsync(actorId, roles, residentId))
        {
            return Result.Fail("Resident not found or you do not have access to it.");
        }

        var current = await _db.ResidentUnits
            .Where(ru => ru.ResidentProfileId == residentId && ru.MoveOutDate == null && !ru.IsDeleted)
            .ToListAsync();

        if (current.Count == 0)
        {
            return Result.Fail("Resident has no active unit association.");
        }

        var moveOut = request.MoveOutDate ?? DateTimeOffset.UtcNow;
        foreach (var association in current)
        {
            association.MoveOutDate = moveOut;
            association.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeactivateResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId)
    {
        var resident = await _db.ResidentProfiles.SingleOrDefaultAsync(r => r.Id == residentId && !r.IsDeleted);
        if (resident is null || !await CanAccessResidentAsync(actorId, roles, residentId))
        {
            return Result.Fail("Resident not found or you do not have access to it.");
        }

        resident.IsActive = false;
        resident.UpdatedAt = DateTimeOffset.UtcNow;

        // End active occupancy while preserving history (FR-RES-006).
        var current = await _db.ResidentUnits
            .Where(ru => ru.ResidentProfileId == residentId && ru.MoveOutDate == null && !ru.IsDeleted)
            .ToListAsync();
        foreach (var association in current)
        {
            association.MoveOutDate = DateTimeOffset.UtcNow;
            association.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    // ---- helpers ----

    private async Task<List<Guid>> ManagedUnitIdsAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (roles.Contains(AppRoles.Administrator))
        {
            return await _propertyDb.ResidentialUnits.AsNoTracking().Select(u => u.Id).ToListAsync();
        }

        return await _propertyDb.ResidentialUnits
            .AsNoTracking()
            .Where(u => u.Building!.Property!.ManagerUserId == actorId)
            .Select(u => u.Id)
            .ToListAsync();
    }

    private async Task<bool> CanAccessResidentAsync(Guid actorId, IReadOnlyList<string> roles, Guid residentId)
    {
        if (roles.Contains(AppRoles.Administrator))
        {
            return true;
        }

        if (!roles.Contains(AppRoles.PropertyManager))
        {
            return false;
        }

        var managedUnitIds = await ManagedUnitIdsAsync(actorId, roles);
        return await _db.ResidentUnits
            .AnyAsync(ru => ru.ResidentProfileId == residentId &&
                            ru.MoveOutDate == null &&
                            !ru.IsDeleted &&
                            managedUnitIds.Contains(ru.UnitId));
    }

    private async Task<IReadOnlyList<ResidentProfileDto>> ToDtosAsync(IEnumerable<ResidentProfile> residents)
    {
        var list = residents.ToList();

        // Current unit id per resident (active association, latest move-in).
        var currentAssociations = await _db.ResidentUnits
            .AsNoTracking()
            .Where(ru => list.Select(r => r.Id).Contains(ru.ResidentProfileId) &&
                         ru.MoveOutDate == null &&
                         !ru.IsDeleted)
            .GroupBy(ru => ru.ResidentProfileId)
            .Select(g => g.OrderByDescending(ru => ru.MoveInDate).First())
            .ToListAsync();

        var unitIds = currentAssociations.Select(a => a.UnitId).Distinct().ToList();
        var unitNumbers = await _propertyDb.ResidentialUnits
            .AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UnitNumber);

        return list.Select(r =>
        {
            var current = currentAssociations.FirstOrDefault(a => a.ResidentProfileId == r.Id);
            return new ResidentProfileDto
            {
                Id = r.Id,
                UserId = r.UserId,
                FirstName = r.FirstName,
                LastName = r.LastName,
                Email = r.Email,
                PhoneNumber = r.PhoneNumber,
                IsActive = r.IsActive,
                CurrentUnitId = current?.UnitId,
                CurrentUnitNumber = current is not null && unitNumbers.TryGetValue(current.UnitId, out var n) ? n : null,
                CurrentMoveInDate = current?.MoveInDate,
            };
        }).ToList();
    }
}

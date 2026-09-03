using Microsoft.EntityFrameworkCore;
using PMP.Modules.Auth.Data;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Booking.Data;
using PMP.Modules.Property.Data;
using PMP.Modules.Security.Contracts;
using PMP.Modules.Security.Data;
using PMP.Modules.Security.Entities;
using PMP.Modules.Security.Enums;
using PMP.Shared.Common;

namespace PMP.Modules.Security.Services;

public interface ISecurityService
{
    Task<Result<VisitorDto>> RegisterVisitorAsync(Guid actorId, IReadOnlyList<string> roles, RegisterVisitorRequest request);

    Task<Result<VisitorDto>> CheckInVisitorAsync(Guid actorId, IReadOnlyList<string> roles, Guid visitorId, CheckInVisitorRequest request);

    Task<Result<VisitorDto>> CheckOutVisitorAsync(Guid actorId, IReadOnlyList<string> roles, Guid visitorId);

    Task<IReadOnlyList<VisitorDto>> GetVisitorsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId, bool activeOnly);

    Task<Result<AccessGrantDto>> GrantAccessAsync(Guid actorId, IReadOnlyList<string> roles, GrantAccessRequest request);

    Task<Result> RevokeAccessAsync(Guid actorId, IReadOnlyList<string> roles, Guid accessGrantId);

    Task<IReadOnlyList<AccessGrantDto>> GetAccessLogAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId, bool activeOnly);
}

/// <summary>
/// Physical security & visitor management (BR-010, FR-SEC-001..007). Staff roles
/// (PropertyManager/Administrator) act as authorized security personnel: they register
/// visitors, check them in/out, and grant/revoke building/unit/facility access. All
/// visitor entries/exits and access grants/revocations are retained as audit logs.
/// </summary>
public class SecurityService : ISecurityService
{
    private readonly SecurityDbContext _db;
    private readonly AuthDbContext _authDb;
    private readonly PropertyDbContext _propertyDb;
    private readonly BookingDbContext _bookingDb;

    public SecurityService(
        SecurityDbContext db,
        AuthDbContext authDb,
        PropertyDbContext propertyDb,
        BookingDbContext bookingDb)
    {
        _db = db;
        _authDb = authDb;
        _propertyDb = propertyDb;
        _bookingDb = bookingDb;
    }

    public async Task<Result<VisitorDto>> RegisterVisitorAsync(Guid actorId, IReadOnlyList<string> roles, RegisterVisitorRequest request)
    {
        if (!IsSecurityRole(roles))
        {
            return Result.Fail<VisitorDto>("Only authorized security staff can register visitors.");
        }

        var property = await _propertyDb.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PropertyId);
        if (property is null || !await CanAccessPropertyAsync(actorId, roles, request.PropertyId))
        {
            return Result.Fail<VisitorDto>("Property not found or you do not have access to it.");
        }

        var visitor = new VisitorRecord
        {
            PropertyId = request.PropertyId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            HostUserId = request.HostUserId,
            UnitNumber = request.UnitNumber,
            Status = VisitorStatus.Registered,
            RegisteredByUserId = actorId,
            Notes = request.Notes,
        };

        _db.Visitors.Add(visitor);
        await _db.SaveChangesAsync();

        var dtos = await ToVisitorDtosAsync([visitor]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<VisitorDto>> CheckInVisitorAsync(Guid actorId, IReadOnlyList<string> roles, Guid visitorId, CheckInVisitorRequest request)
    {
        if (!IsSecurityRole(roles))
        {
            return Result.Fail<VisitorDto>("Only authorized security staff can check in visitors.");
        }

        var visitor = await _db.Visitors.SingleOrDefaultAsync(v => v.Id == visitorId);
        if (visitor is null || !await CanAccessPropertyAsync(actorId, roles, visitor.PropertyId))
        {
            return Result.Fail<VisitorDto>("Visitor not found or you do not have access to it.");
        }

        if (visitor.Status != VisitorStatus.Registered)
        {
            return Result.Fail<VisitorDto>("Only registered visitors can be checked in.");
        }

        visitor.Status = VisitorStatus.CheckedIn;
        visitor.CheckInAt = DateTimeOffset.UtcNow;
        visitor.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        var dtos = await ToVisitorDtosAsync([visitor]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<VisitorDto>> CheckOutVisitorAsync(Guid actorId, IReadOnlyList<string> roles, Guid visitorId)
    {
        if (!IsSecurityRole(roles))
        {
            return Result.Fail<VisitorDto>("Only authorized security staff can check out visitors.");
        }

        var visitor = await _db.Visitors.SingleOrDefaultAsync(v => v.Id == visitorId);
        if (visitor is null || !await CanAccessPropertyAsync(actorId, roles, visitor.PropertyId))
        {
            return Result.Fail<VisitorDto>("Visitor not found or you do not have access to it.");
        }

        if (visitor.Status != VisitorStatus.CheckedIn)
        {
            return Result.Fail<VisitorDto>("Only checked-in visitors can be checked out.");
        }

        visitor.Status = VisitorStatus.CheckedOut;
        visitor.CheckOutAt = DateTimeOffset.UtcNow;
        visitor.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        var dtos = await ToVisitorDtosAsync([visitor]);
        return Result.Ok(dtos[0]);
    }

    public async Task<IReadOnlyList<VisitorDto>> GetVisitorsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId, bool activeOnly)
    {
        var query = _db.Visitors.AsNoTracking();

        if (!roles.Contains(AppRoles.Administrator))
        {
            var scopedIds = await ScopedPropertyIdsAsync(actorId, roles);
            query = query.Where(v => scopedIds.Contains(v.PropertyId));
        }

        if (propertyId.HasValue)
        {
            query = query.Where(v => v.PropertyId == propertyId.Value);
        }

        if (activeOnly)
        {
            query = query.Where(v => v.Status == VisitorStatus.Registered || v.Status == VisitorStatus.CheckedIn);
        }

        var rows = await query.ToListAsync();
        var ordered = rows.OrderByDescending(v => v.CreatedAt).ToList();
        return await ToVisitorDtosAsync(ordered);
    }

    public async Task<Result<AccessGrantDto>> GrantAccessAsync(Guid actorId, IReadOnlyList<string> roles, GrantAccessRequest request)
    {
        if (!IsSecurityRole(roles))
        {
            return Result.Fail<AccessGrantDto>("Only authorized security staff can grant access.");
        }

        var property = await _propertyDb.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PropertyId);
        if (property is null || !await CanAccessPropertyAsync(actorId, roles, request.PropertyId))
        {
            return Result.Fail<AccessGrantDto>("Property not found or you do not have access to it.");
        }

        if (request.SubjectType == AccessSubjectType.User && !request.SubjectUserId.HasValue)
        {
            return Result.Fail<AccessGrantDto>("SubjectUserId is required for user access.");
        }

        if (request.SubjectType == AccessSubjectType.Visitor)
        {
            if (!request.VisitorId.HasValue)
            {
                return Result.Fail<AccessGrantDto>("VisitorId is required for visitor access.");
            }

            var visitorExists = await _db.Visitors.AnyAsync(v => v.Id == request.VisitorId.Value);
            if (!visitorExists)
            {
                return Result.Fail<AccessGrantDto>("Visitor not found.");
            }
        }

        var grant = new AccessGrant
        {
            PropertyId = request.PropertyId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            SubjectType = request.SubjectType,
            SubjectUserId = request.SubjectUserId,
            VisitorId = request.VisitorId,
            GrantedByUserId = actorId,
            ExpiresAt = request.ExpiresAt,
        };

        if (grant.ExpiresAt.HasValue && grant.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result.Fail<AccessGrantDto>("Expiration must be in the future.");
        }

        _db.AccessGrants.Add(grant);
        await _db.SaveChangesAsync();

        var dtos = await ToAccessGrantDtosAsync([grant]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result> RevokeAccessAsync(Guid actorId, IReadOnlyList<string> roles, Guid accessGrantId)
    {
        if (!IsSecurityRole(roles))
        {
            return Result.Fail("Only authorized security staff can revoke access.");
        }

        var grant = await _db.AccessGrants.SingleOrDefaultAsync(g => g.Id == accessGrantId);
        if (grant is null || !await CanAccessPropertyAsync(actorId, roles, grant.PropertyId))
        {
            return Result.Fail("Access grant not found or you do not have access to it.");
        }

        if (grant.RevokedAt.HasValue)
        {
            return Result.Fail("Access already revoked.");
        }

        grant.RevokedAt = DateTimeOffset.UtcNow;
        grant.RevokedByUserId = actorId;
        grant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<IReadOnlyList<AccessGrantDto>> GetAccessLogAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId, bool activeOnly)
    {
        var query = _db.AccessGrants.AsNoTracking();

        if (!roles.Contains(AppRoles.Administrator))
        {
            var scopedIds = await ScopedPropertyIdsAsync(actorId, roles);
            query = query.Where(g => scopedIds.Contains(g.PropertyId));
        }

        if (propertyId.HasValue)
        {
            query = query.Where(g => g.PropertyId == propertyId.Value);
        }

        var rows = await query.ToListAsync();

        if (activeOnly)
        {
            rows = rows.Where(g => g.IsActive).ToList();
        }

        var ordered = rows.OrderByDescending(g => g.GrantedAt).ToList();
        return await ToAccessGrantDtosAsync(ordered);
    }

    // ---- helpers ----

    private static bool IsSecurityRole(IReadOnlyList<string> roles) =>
        roles.Contains(AppRoles.Administrator) || roles.Contains(AppRoles.PropertyManager);

    private async Task<List<Guid>> ScopedPropertyIdsAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (roles.Contains(AppRoles.Administrator))
        {
            return await _propertyDb.Properties.AsNoTracking().Select(p => p.Id).ToListAsync();
        }

        return await _propertyDb.Properties.AsNoTracking()
            .Where(p => p.ManagerUserId == actorId)
            .Select(p => p.Id)
            .ToListAsync();
    }

    private async Task<bool> CanAccessPropertyAsync(Guid actorId, IReadOnlyList<string> roles, Guid propertyId)
    {
        if (roles.Contains(AppRoles.Administrator))
        {
            return true;
        }

        return await _propertyDb.Properties.AsNoTracking()
            .AnyAsync(p => p.Id == propertyId && p.ManagerUserId == actorId);
    }

    private async Task<string> UserNameAsync(Guid userId)
    {
        var user = await _authDb.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId);
        return user is null ? string.Empty : $"{user.FirstName} {user.LastName}".Trim();
    }

    private async Task<IReadOnlyList<VisitorDto>> ToVisitorDtosAsync(IEnumerable<VisitorRecord> visitors)
    {
        var list = visitors.ToList();
        var propertyIds = list.Select(v => v.PropertyId).Distinct().ToList();
        var names = await _propertyDb.Properties.AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return list.Select(v => new VisitorDto
        {
            Id = v.Id,
            PropertyId = v.PropertyId,
            PropertyName = names.GetValueOrDefault(v.PropertyId) ?? string.Empty,
            FirstName = v.FirstName,
            LastName = v.LastName,
            PhoneNumber = v.PhoneNumber,
            HostUserId = v.HostUserId,
            UnitNumber = v.UnitNumber,
            Status = v.Status,
            RegisteredByUserId = v.RegisteredByUserId,
            CheckInAt = v.CheckInAt,
            CheckOutAt = v.CheckOutAt,
            Notes = v.Notes,
        }).ToList();
    }

    private async Task<IReadOnlyList<AccessGrantDto>> ToAccessGrantDtosAsync(IEnumerable<AccessGrant> grants)
    {
        var list = grants.ToList();
        var result = new List<AccessGrantDto>();
        foreach (var grant in list)
        {
            var propertyName = await _propertyDb.Properties.AsNoTracking()
                .Where(p => p.Id == grant.PropertyId)
                .Select(p => p.Name)
                .SingleOrDefaultAsync() ?? string.Empty;

            var targetName = await ResolveTargetNameAsync(grant.TargetType, grant.TargetId);
            var subjectName = grant.SubjectType == AccessSubjectType.User
                ? await UserNameAsync(grant.SubjectUserId ?? Guid.Empty)
                : await VisitorFullNameAsync(grant.VisitorId);

            result.Add(new AccessGrantDto
            {
                Id = grant.Id,
                PropertyId = grant.PropertyId,
                PropertyName = propertyName,
                TargetType = grant.TargetType,
                TargetId = grant.TargetId,
                TargetName = targetName,
                SubjectType = grant.SubjectType,
                SubjectUserId = grant.SubjectUserId,
                SubjectName = subjectName,
                VisitorId = grant.VisitorId,
                GrantedByUserId = grant.GrantedByUserId,
                GrantedAt = grant.GrantedAt,
                ExpiresAt = grant.ExpiresAt,
                RevokedAt = grant.RevokedAt,
                IsActive = grant.IsActive,
            });
        }

        return result;
    }

    private async Task<string> ResolveTargetNameAsync(AccessTargetType targetType, Guid targetId)
    {
        return targetType switch
        {
            AccessTargetType.Building => await _propertyDb.Buildings.AsNoTracking()
                .Where(b => b.Id == targetId).Select(b => b.Name).SingleOrDefaultAsync() ?? string.Empty,
            AccessTargetType.Unit => await _propertyDb.ResidentialUnits.AsNoTracking()
                .Where(u => u.Id == targetId).Select(u => u.UnitNumber).SingleOrDefaultAsync() ?? string.Empty,
            AccessTargetType.Facility => await _bookingDb.Facilities.AsNoTracking()
                .Where(f => f.Id == targetId).Select(f => f.Name).SingleOrDefaultAsync() ?? string.Empty,
            _ => string.Empty,
        };
    }

    private async Task<string> VisitorFullNameAsync(Guid? visitorId)
    {
        if (!visitorId.HasValue)
        {
            return string.Empty;
        }

        var visitor = await _db.Visitors.AsNoTracking().SingleOrDefaultAsync(v => v.Id == visitorId.Value);
        return visitor is null ? string.Empty : $"{visitor.FirstName} {visitor.LastName}".Trim();
    }
}

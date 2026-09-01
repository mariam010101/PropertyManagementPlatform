using Microsoft.EntityFrameworkCore;
using PMP.Modules.Maintenance.Abstractions;
using PMP.Modules.Maintenance.Contracts;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Maintenance.Entities;
using PMP.Modules.Maintenance.Enums;
using PMP.Modules.Property.Data;
using PMP.Modules.Resident.Data;
using PMP.Shared.Common;

namespace PMP.Modules.Maintenance.Services;

public interface IMaintenanceService
{
    Task<Result<MaintenanceRequestDto>> SubmitAsync(Guid actorId, IReadOnlyList<string> roles, CreateMaintenanceRequestRequest request);

    Task<IReadOnlyList<MaintenanceRequestDto>> GetRequestsAsync(Guid actorId, IReadOnlyList<string> roles, MaintenanceStatus? status, MaintenancePriority? priority, Guid? unitId);

    Task<Result<MaintenanceRequestDto>> GetRequestAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId);

    Task<Result<MaintenanceRequestDto>> AssignAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, AssignMaintenanceRequest request);

    Task<Result<MaintenanceRequestDto>> UpdateStatusAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, UpdateMaintenanceStatusRequest request);

    Task<Result<MaintenanceRequestDto>> SetPriorityAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, SetMaintenancePriorityRequest request);

    Task<Result<MaintenanceRequestDto>> ConfirmAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, ConfirmCompletionRequest request);

    Task<Result> AddAttachmentAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, string fileName, string contentType, string storagePath);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly MaintenanceDbContext _db;
    private readonly ResidentDbContext _residentDb;
    private readonly PropertyDbContext _propertyDb;
    private readonly INotificationService _notifications;

    public MaintenanceService(
        MaintenanceDbContext db,
        ResidentDbContext residentDb,
        PropertyDbContext propertyDb,
        INotificationService notifications)
    {
        _db = db;
        _residentDb = residentDb;
        _propertyDb = propertyDb;
        _notifications = notifications;
    }

    private static readonly IReadOnlyDictionary<MaintenanceStatus, MaintenanceStatus[]> AllowedTransitions =
        new Dictionary<MaintenanceStatus, MaintenanceStatus[]>
        {
            [MaintenanceStatus.Submitted] = [MaintenanceStatus.Assigned, MaintenanceStatus.Cancelled],
            [MaintenanceStatus.Assigned] = [MaintenanceStatus.InProgress, MaintenanceStatus.Cancelled],
            [MaintenanceStatus.InProgress] = [MaintenanceStatus.Completed, MaintenanceStatus.Cancelled],
            [MaintenanceStatus.Completed] = [MaintenanceStatus.Confirmed, MaintenanceStatus.InProgress, MaintenanceStatus.Cancelled],
            [MaintenanceStatus.Confirmed] = [MaintenanceStatus.Closed],
            [MaintenanceStatus.Closed] = [],
            [MaintenanceStatus.Cancelled] = [],
        };

    public async Task<Result<MaintenanceRequestDto>> SubmitAsync(Guid actorId, IReadOnlyList<string> roles, CreateMaintenanceRequestRequest request)
    {
        var profile = await _residentDb.ResidentProfiles
            .SingleOrDefaultAsync(r => r.UserId == actorId && !r.IsDeleted && r.IsActive);
        if (profile is null)
        {
            return Result.Fail<MaintenanceRequestDto>("Only active residents can submit maintenance requests.");
        }

        // NFR-MNT-003: residents may only submit for units associated with their account.
        var isTheirUnit = await _residentDb.ResidentUnits
            .AnyAsync(ru => ru.ResidentProfileId == profile.Id &&
                            ru.UnitId == request.UnitId &&
                            ru.MoveOutDate == null &&
                            !ru.IsDeleted);
        if (!isTheirUnit)
        {
            return Result.Fail<MaintenanceRequestDto>("You can only submit requests for your current residential unit.");
        }

        var requestEntity = new MaintenanceRequest
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = MaintenanceStatus.Submitted,
            RequestedByResidentId = profile.Id,
            UnitId = request.UnitId,
        };

        _db.MaintenanceRequests.Add(requestEntity);
        _db.MaintenanceHistory.Add(new MaintenanceHistoryEntry
        {
            MaintenanceRequestId = requestEntity.Id,
            FromStatus = null,
            ToStatus = MaintenanceStatus.Submitted,
            Comment = "Request submitted by resident.",
            ChangedByUserId = actorId,
        });
        await _db.SaveChangesAsync();

        var dto = await ToDtoAsync(requestEntity.Id, roles);
        return Result.Ok(dto);
    }

    public async Task<IReadOnlyList<MaintenanceRequestDto>> GetRequestsAsync(Guid actorId, IReadOnlyList<string> roles, MaintenanceStatus? status, MaintenancePriority? priority, Guid? unitId)
    {
        var query = _db.MaintenanceRequests.AsNoTracking().Where(r => !r.IsDeleted);

        if (roles.Contains(AppRoles.Resident) && !IsStaff(roles))
        {
            var profile = await _residentDb.ResidentProfiles
                .Where(r => r.UserId == actorId && !r.IsDeleted)
                .Select(r => (Guid?)r.Id)
                .SingleOrDefaultAsync();
            if (profile is null)
            {
                return Array.Empty<MaintenanceRequestDto>();
            }

            query = query.Where(r => r.RequestedByResidentId == profile.Value);
        }
        else if (roles.Contains(AppRoles.Technician) && !roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            query = query.Where(r => r.AssignedToUserId == actorId);
        }
        else
        {
            var unitIds = await ManagedUnitIdsAsync(actorId, roles);
            query = query.Where(r => unitIds.Contains(r.UnitId));
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(r => r.Priority == priority.Value);
        }

        if (unitId.HasValue)
        {
            query = query.Where(r => r.UnitId == unitId.Value);
        }

        var ids = await query.OrderByDescending(r => r.CreatedAt).Select(r => r.Id).ToListAsync();
        var result = new List<MaintenanceRequestDto>();
        foreach (var id in ids)
        {
            result.Add(await ToDtoAsync(id, roles));
        }

        return result;
    }

    public async Task<Result<MaintenanceRequestDto>> GetRequestAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId)
    {
        if (!await CanAccessRequestAsync(actorId, roles, requestId))
        {
            return Result.Fail<MaintenanceRequestDto>("Request not found or you do not have access to it.");
        }

        return Result.Ok(await ToDtoAsync(requestId, roles));
    }

    public async Task<Result<MaintenanceRequestDto>> AssignAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, AssignMaintenanceRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<MaintenanceRequestDto>("Only a property manager or administrator can assign requests.");
        }

        var requestEntity = await _db.MaintenanceRequests.SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
        if (requestEntity is null || !await CanAccessRequestAsync(actorId, roles, requestId))
        {
            return Result.Fail<MaintenanceRequestDto>("Request not found or you do not have access to it.");
        }

        if (requestEntity.Status is MaintenanceStatus.Closed or MaintenanceStatus.Cancelled)
        {
            return Result.Fail<MaintenanceRequestDto>("Closed or cancelled requests cannot be assigned.");
        }

        var from = requestEntity.Status;
        requestEntity.AssignedToUserId = request.TechnicianUserId;
        requestEntity.AssignedAt = DateTimeOffset.UtcNow;
        if (from == MaintenanceStatus.Submitted)
        {
            requestEntity.Status = MaintenanceStatus.Assigned;
        }

        _db.MaintenanceHistory.Add(new MaintenanceHistoryEntry
        {
            MaintenanceRequestId = requestEntity.Id,
            FromStatus = from,
            ToStatus = requestEntity.Status,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? "Request assigned to technician." : request.Comment,
            ChangedByUserId = actorId,
        });
        await _db.SaveChangesAsync();

        await NotifyResidentAsync(requestEntity, "Maintenance request assigned", "Your maintenance request has been assigned to a technician.");
        return Result.Ok(await ToDtoAsync(requestEntity.Id, roles));
    }

    public async Task<Result<MaintenanceRequestDto>> UpdateStatusAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, UpdateMaintenanceStatusRequest request)
    {
        var requestEntity = await _db.MaintenanceRequests.SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
        if (requestEntity is null || !await CanAccessRequestAsync(actorId, roles, requestId))
        {
            return Result.Fail<MaintenanceRequestDto>("Request not found or you do not have access to it.");
        }

        var from = requestEntity.Status;
        if (from == request.Status)
        {
            return Result.Fail<MaintenanceRequestDto>("Request is already in this status.");
        }

        if (!AllowedTransitions.TryGetValue(from, out var allowed) || !allowed.Contains(request.Status))
        {
            return Result.Fail<MaintenanceRequestDto>($"Transition from {from} to {request.Status} is not allowed.");
        }

        if (!await CanPerformAsync(actorId, roles, requestEntity, request.Status))
        {
            return Result.Fail<MaintenanceRequestDto>("You are not allowed to perform this transition.");
        }

        ApplyStatus(requestEntity, request.Status);
        requestEntity.UpdatedAt = DateTimeOffset.UtcNow;

        _db.MaintenanceHistory.Add(new MaintenanceHistoryEntry
        {
            MaintenanceRequestId = requestEntity.Id,
            FromStatus = from,
            ToStatus = request.Status,
            Comment = request.Comment,
            ChangedByUserId = actorId,
        });
        await _db.SaveChangesAsync();

        await NotifyResidentAsync(requestEntity, $"Request {request.Status}", $"Your maintenance request status changed to {request.Status}.");
        return Result.Ok(await ToDtoAsync(requestEntity.Id, roles));
    }

    public async Task<Result<MaintenanceRequestDto>> SetPriorityAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, SetMaintenancePriorityRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<MaintenanceRequestDto>("Only a property manager or administrator can set priority.");
        }

        var requestEntity = await _db.MaintenanceRequests.SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
        if (requestEntity is null || !await CanAccessRequestAsync(actorId, roles, requestId))
        {
            return Result.Fail<MaintenanceRequestDto>("Request not found or you do not have access to it.");
        }

        requestEntity.Priority = request.Priority;
        requestEntity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Result.Ok(await ToDtoAsync(requestEntity.Id, roles));
    }

    public async Task<Result<MaintenanceRequestDto>> ConfirmAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, ConfirmCompletionRequest request)
    {
        var requestEntity = await _db.MaintenanceRequests.SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
        if (requestEntity is null || !await CanAccessRequestAsync(actorId, roles, requestId))
        {
            return Result.Fail<MaintenanceRequestDto>("Request not found or you do not have access to it.");
        }

        if (requestEntity.Status != MaintenanceStatus.Completed)
        {
            return Result.Fail<MaintenanceRequestDto>("Only completed requests can be confirmed.");
        }

        var from = requestEntity.Status;
        requestEntity.Status = MaintenanceStatus.Confirmed;
        requestEntity.ConfirmedAt = DateTimeOffset.UtcNow;
        requestEntity.UpdatedAt = DateTimeOffset.UtcNow;

        _db.MaintenanceHistory.Add(new MaintenanceHistoryEntry
        {
            MaintenanceRequestId = requestEntity.Id,
            FromStatus = from,
            ToStatus = MaintenanceStatus.Confirmed,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? "Completion confirmed by resident." : request.Comment,
            ChangedByUserId = actorId,
        });

        // Confirmation closes the request.
        requestEntity.Status = MaintenanceStatus.Closed;
        requestEntity.ClosedAt = DateTimeOffset.UtcNow;
        _db.MaintenanceHistory.Add(new MaintenanceHistoryEntry
        {
            MaintenanceRequestId = requestEntity.Id,
            FromStatus = MaintenanceStatus.Confirmed,
            ToStatus = MaintenanceStatus.Closed,
            Comment = "Request closed after resident confirmation.",
            ChangedByUserId = actorId,
        });

        await _db.SaveChangesAsync();
        return Result.Ok(await ToDtoAsync(requestEntity.Id, roles));
    }

    public async Task<Result> AddAttachmentAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId, string fileName, string contentType, string storagePath)
    {
        var requestEntity = await _db.MaintenanceRequests.SingleOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);
        if (requestEntity is null)
        {
            return Result.Fail("Request not found.");
        }

        if (requestEntity.Status is MaintenanceStatus.Closed or MaintenanceStatus.Cancelled)
        {
            return Result.Fail("Closed or cancelled requests cannot be modified.");
        }

        var requester = await _residentDb.ResidentProfiles
            .Where(r => r.Id == requestEntity.RequestedByResidentId && !r.IsDeleted)
            .Select(r => (Guid?)r.UserId)
            .SingleOrDefaultAsync();

        var isRequester = requester == actorId;
        var isAssigned = requestEntity.AssignedToUserId == actorId;
        var isManager = roles.Contains(AppRoles.PropertyManager) || roles.Contains(AppRoles.Administrator);

        if (!isRequester && !isAssigned && !isManager)
        {
            return Result.Fail("You are not allowed to attach files to this request.");
        }

        _db.MaintenanceAttachments.Add(new MaintenanceAttachment
        {
            MaintenanceRequestId = requestEntity.Id,
            FileName = fileName,
            ContentType = contentType,
            StoragePath = storagePath,
            UploadedByUserId = actorId,
        });
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    // ---- state machine helpers ----

    private static void ApplyStatus(MaintenanceRequest request, MaintenanceStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        request.Status = status;
        switch (status)
        {
            case MaintenanceStatus.Completed:
                request.CompletedAt = now;
                break;
            case MaintenanceStatus.Closed:
                request.ClosedAt = now;
                break;
            case MaintenanceStatus.Cancelled:
                request.CancelledAt = now;
                break;
            case MaintenanceStatus.InProgress when request.Status != MaintenanceStatus.InProgress:
                // Reopening a completed request; keep timestamps meaningful.
                break;
        }
    }

    private async Task<bool> CanPerformAsync(Guid actorId, IReadOnlyList<string> roles, MaintenanceRequest request, MaintenanceStatus to)
    {
        var requester = await _residentDb.ResidentProfiles
            .Where(r => r.Id == request.RequestedByResidentId && !r.IsDeleted)
            .Select(r => (Guid?)r.UserId)
            .SingleOrDefaultAsync();

        bool isRequester = requester == actorId;
        bool isAssignedTech = request.AssignedToUserId == actorId && roles.Contains(AppRoles.Technician);
        bool isManager = roles.Contains(AppRoles.PropertyManager) || roles.Contains(AppRoles.Administrator);

        return to switch
        {
            MaintenanceStatus.InProgress when request.Status == MaintenanceStatus.Completed =>
                // Reopen within the 7-day window by the resident.
                isRequester && request.CompletedAt is not null &&
                DateTimeOffset.UtcNow - request.CompletedAt.Value <= TimeSpan.FromDays(7),
            MaintenanceStatus.InProgress => isAssignedTech || isManager,
            MaintenanceStatus.Completed => isAssignedTech || isManager,
            MaintenanceStatus.Cancelled => isRequester || isManager,
            MaintenanceStatus.Confirmed => isRequester,
            MaintenanceStatus.Closed => isRequester || isManager,
            _ => false,
        };
    }

    private async Task NotifyResidentAsync(MaintenanceRequest request, string title, string message)
    {
        var userId = await _residentDb.ResidentProfiles
            .Where(r => r.Id == request.RequestedByResidentId && !r.IsDeleted)
            .Select(r => (Guid?)r.UserId)
            .SingleOrDefaultAsync();

        if (userId.HasValue)
        {
            await _notifications.NotifyAsync(userId.Value, title, message);
        }
    }

    private async Task<bool> CanAccessRequestAsync(Guid actorId, IReadOnlyList<string> roles, Guid requestId)
    {
        var request = await _db.MaintenanceRequests
            .Where(r => r.Id == requestId && !r.IsDeleted)
            .Select(r => new { r.RequestedByResidentId, r.AssignedToUserId })
            .SingleOrDefaultAsync();

        if (request is null)
        {
            return false;
        }

        if (roles.Contains(AppRoles.Administrator))
        {
            return true;
        }

        if (roles.Contains(AppRoles.PropertyManager))
        {
            var unitId = await _db.MaintenanceRequests.Where(r => r.Id == requestId).Select(r => r.UnitId).SingleAsync();
            var unitIds = await ManagedUnitIdsAsync(actorId, roles);
            return unitIds.Contains(unitId);
        }

        if (roles.Contains(AppRoles.Technician) && request.AssignedToUserId == actorId)
        {
            return true;
        }

        var requester = await _residentDb.ResidentProfiles
            .Where(r => r.Id == request.RequestedByResidentId && !r.IsDeleted)
            .Select(r => (Guid?)r.UserId)
            .SingleOrDefaultAsync();

        return requester == actorId;
    }

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

    private static bool IsStaff(IReadOnlyList<string> roles)
    {
        return roles.Contains(AppRoles.PropertyManager) || roles.Contains(AppRoles.Administrator) || roles.Contains(AppRoles.Technician);
    }

    private async Task<MaintenanceRequestDto> ToDtoAsync(Guid requestId, IReadOnlyList<string> roles)
    {
        var request = await _db.MaintenanceRequests
            .AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.Priority,
                r.Status,
                r.RequestedByResidentId,
                r.UnitId,
                r.AssignedToUserId,
                r.AssignedAt,
                r.CompletedAt,
                r.ConfirmedAt,
                r.ClosedAt,
                r.CancellationReason,
                r.CreatedAt,
            })
            .SingleAsync();

        var unitNumber = await _propertyDb.ResidentialUnits
            .AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Select(u => u.UnitNumber)
            .SingleOrDefaultAsync();

        var attachments = await _db.MaintenanceAttachments
            .AsNoTracking()
            .Where(a => a.MaintenanceRequestId == requestId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new MaintenanceAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                UploadedAt = a.CreatedAt,
            })
            .ToListAsync();

        var history = await _db.MaintenanceHistory
            .AsNoTracking()
            .Where(h => h.MaintenanceRequestId == requestId)
            .OrderBy(h => h.CreatedAt)
            .Select(h => new MaintenanceHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                Comment = h.Comment,
                ChangedByUserId = h.ChangedByUserId,
                ChangedAt = h.CreatedAt,
            })
            .ToListAsync();

        return new MaintenanceRequestDto
        {
            Id = request.Id,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = request.Status,
            RequestedByResidentId = request.RequestedByResidentId,
            UnitId = request.UnitId,
            UnitNumber = unitNumber,
            AssignedToUserId = request.AssignedToUserId,
            AssignedAt = request.AssignedAt,
            CompletedAt = request.CompletedAt,
            ConfirmedAt = request.ConfirmedAt,
            ClosedAt = request.ClosedAt,
            CancellationReason = request.CancellationReason,
            CreatedAt = request.CreatedAt,
            AttachmentCount = attachments.Count,
            Attachments = attachments,
            History = history,
        };
    }
}

using Microsoft.EntityFrameworkCore;
using PMP.Modules.Communication.Contracts;
using PMP.Modules.Communication.Services;
using PMP.Modules.Lease.Contracts;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Entities;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Shared.Common;

namespace PMP.Modules.Lease.Services;

public interface ILeaseService
{
    Task<Result<LeaseDetailDto>> CreateLeaseAsync(Guid actorId, IReadOnlyList<string> roles, CreateLeaseRequest request);

    Task<IReadOnlyList<LeaseDto>> GetLeasesAsync(Guid actorId, IReadOnlyList<string> roles, LeaseStatus? status);

    Task<Result<LeaseDetailDto>> GetLeaseAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId);

    Task<Result<LeaseDetailDto>> UpdateLeaseAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId, UpdateLeaseRequest request);

    Task<Result> TerminateLeaseAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId, TerminateLeaseRequest request);

    Task<Result<LeaseDocumentDto>> UploadDocumentAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId, string fileName, string contentType, string storagePath);

    Task<Result<IReadOnlyList<LeaseDocumentDto>>> GetDocumentsAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId);

    /// <summary>System sweep: expire past-due leases and notify about upcoming expiries.</summary>
    Task<int> RunExpiryLifecycleAsync();
}

/// <summary>
/// Lease & document management (BR-006, BRULE-LEASE-001..006). Each modification
/// creates a versioned history entry (FR-LEASE-004); expired leases are retained and
/// archived for reference (FR-LEASE-007).
/// </summary>
public class LeaseService : ILeaseService
{
    private const int ExpiryNoticeDays = 30;

    private readonly LeaseDbContext _db;
    private readonly PropertyDbContext _propertyDb;
    private readonly ResidentDbContext _residentDb;
    private readonly ICommunicationService _communication;

    public LeaseService(
        LeaseDbContext db,
        PropertyDbContext propertyDb,
        ResidentDbContext residentDb,
        ICommunicationService communication)
    {
        _db = db;
        _propertyDb = propertyDb;
        _residentDb = residentDb;
        _communication = communication;
    }

    public async Task<Result<LeaseDetailDto>> CreateLeaseAsync(Guid actorId, IReadOnlyList<string> roles, CreateLeaseRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<LeaseDetailDto>("Only a property manager or administrator can create leases.");
        }

        if (request.EndDate <= request.StartDate)
        {
            return Result.Fail<LeaseDetailDto>("End date must be after start date.");
        }

        var residentProfile = await _residentDb.ResidentProfiles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == request.ResidentUserId && !r.IsDeleted);
        if (residentProfile is null)
        {
            return Result.Fail<LeaseDetailDto>("Resident not found.");
        }

        var unit = await _propertyDb.ResidentialUnits
            .AsNoTracking()
            .Include(u => u.Building)
            .ThenInclude(b => b!.Property)
            .SingleOrDefaultAsync(u => u.Id == request.UnitId);
        if (unit is null)
        {
            return Result.Fail<LeaseDetailDto>("Unit not found.");
        }

        if (!roles.Contains(AppRoles.Administrator) && unit.Building!.Property!.ManagerUserId != actorId)
        {
            return Result.Fail<LeaseDetailDto>("You do not have access to this unit's property.");
        }

        if (unit.OperationalStatus != UnitOperationalStatus.Active)
        {
            return Result.Fail<LeaseDetailDto>("Unit is not active; a lease cannot be created.");
        }

        // One active lease per unit at a time (BRULE-LEASE-002).
        var overlapping = await _db.LeaseAgreements.AnyAsync(l =>
            l.UnitId == request.UnitId &&
            l.Status == LeaseStatus.Active &&
            l.StartDate < request.EndDate &&
            l.EndDate > request.StartDate);
        if (overlapping)
        {
            return Result.Fail<LeaseDetailDto>("The unit already has an active lease for this period.");
        }

        var lease = new LeaseAgreement
        {
            ResidentUserId = request.ResidentUserId,
            UnitId = request.UnitId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MonthlyRent = request.MonthlyRent,
            Status = LeaseStatus.Active,
            CurrentVersion = 1,
        };

        _db.LeaseAgreements.Add(lease);
        _db.LeaseHistory.Add(new LeaseHistoryEntry
        {
            LeaseAgreementId = lease.Id,
            Version = 1,
            ChangeType = "Created",
            Description = $"Lease created for unit {unit.UnitNumber} (rent {request.MonthlyRent}).",
            ChangedByUserId = actorId,
        });
        await _db.SaveChangesAsync();

        return Result.Ok(await BuildDetailAsync(lease.Id, actorId, roles));
    }

    public async Task<IReadOnlyList<LeaseDto>> GetLeasesAsync(Guid actorId, IReadOnlyList<string> roles, LeaseStatus? status)
    {
        var leaseIds = await ScopedLeaseIdsAsync(actorId, roles);

        var query = _db.LeaseAgreements.AsNoTracking()
            .Where(l => leaseIds.Contains(l.Id));

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        var rows = await query.ToListAsync();
        return await ToDtosAsync(rows);
    }

    public async Task<Result<LeaseDetailDto>> GetLeaseAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId)
    {
        var lease = await _db.LeaseAgreements.AsNoTracking().SingleOrDefaultAsync(l => l.Id == leaseId);
        if (lease is null)
        {
            return Result.Fail<LeaseDetailDto>("Lease not found.");
        }

        if (!await CanAccessLeaseAsync(actorId, roles, lease))
        {
            return Result.Fail<LeaseDetailDto>("You do not have access to this lease.");
        }

        return Result.Ok(await BuildDetailAsync(leaseId, actorId, roles));
    }

    public async Task<Result<LeaseDetailDto>> UpdateLeaseAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId, UpdateLeaseRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<LeaseDetailDto>("Only a property manager or administrator can modify leases.");
        }

        var lease = await _db.LeaseAgreements.SingleOrDefaultAsync(l => l.Id == leaseId);
        if (lease is null || !await CanAccessLeaseAsync(actorId, roles, lease))
        {
            return Result.Fail<LeaseDetailDto>("Lease not found or you do not have access to it.");
        }

        if (lease.Status is LeaseStatus.Terminated or LeaseStatus.Cancelled)
        {
            return Result.Fail<LeaseDetailDto>("Terminated leases cannot be modified.");
        }

        if (request.EndDate <= request.StartDate)
        {
            return Result.Fail<LeaseDetailDto>("End date must be after start date.");
        }

        lease.CurrentVersion += 1;
        lease.StartDate = request.StartDate;
        lease.EndDate = request.EndDate;
        lease.MonthlyRent = request.MonthlyRent;
        lease.UpdatedAt = DateTimeOffset.UtcNow;

        _db.LeaseHistory.Add(new LeaseHistoryEntry
        {
            LeaseAgreementId = lease.Id,
            Version = lease.CurrentVersion,
            ChangeType = "Modified",
            Description = $"Lease terms updated (v{lease.CurrentVersion}).",
            ChangedByUserId = actorId,
        });
        await _db.SaveChangesAsync();

        return Result.Ok(await BuildDetailAsync(lease.Id, actorId, roles));
    }

    public async Task<Result> TerminateLeaseAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId, TerminateLeaseRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail("Only a property manager or administrator can terminate leases.");
        }

        var lease = await _db.LeaseAgreements.SingleOrDefaultAsync(l => l.Id == leaseId);
        if (lease is null || !await CanAccessLeaseAsync(actorId, roles, lease))
        {
            return Result.Fail("Lease not found or you do not have access to it.");
        }

        if (lease.Status != LeaseStatus.Active)
        {
            return Result.Fail("Only an active lease can be terminated.");
        }

        lease.Status = LeaseStatus.Terminated;
        lease.TerminationComment = request.Comment;
        lease.TerminatedAt = DateTimeOffset.UtcNow;
        lease.UpdatedAt = DateTimeOffset.UtcNow;
        lease.CurrentVersion += 1;

        _db.LeaseHistory.Add(new LeaseHistoryEntry
        {
            LeaseAgreementId = lease.Id,
            Version = lease.CurrentVersion,
            ChangeType = "Terminated",
            Description = string.IsNullOrWhiteSpace(request.Comment) ? "Lease terminated." : request.Comment,
            ChangedByUserId = actorId,
        });
        await _db.SaveChangesAsync();

        await NotifyUserAsync(lease.ResidentUserId, "Lease terminated", "Your lease agreement has been terminated.");
        return Result.Ok();
    }

    public async Task<Result<LeaseDocumentDto>> UploadDocumentAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId, string fileName, string contentType, string storagePath)
    {
        var lease = await _db.LeaseAgreements.AsNoTracking().SingleOrDefaultAsync(l => l.Id == leaseId);
        if (lease is null || !await CanAccessLeaseAsync(actorId, roles, lease))
        {
            return Result.Fail<LeaseDocumentDto>("Lease not found or you do not have access to it.");
        }

        var document = new LeaseDocument
        {
            LeaseAgreementId = leaseId,
            FileName = fileName,
            ContentType = contentType,
            StoragePath = storagePath,
            UploadedByUserId = actorId,
            Version = lease.CurrentVersion,
        };

        _db.LeaseDocuments.Add(document);
        await _db.SaveChangesAsync();

        return Result.Ok(new LeaseDocumentDto
        {
            Id = document.Id,
            LeaseAgreementId = document.LeaseAgreementId,
            FileName = document.FileName,
            ContentType = document.ContentType,
            UploadedByUserId = document.UploadedByUserId,
            Version = document.Version,
            UploadedAt = document.CreatedAt,
        });
    }

    public async Task<Result<IReadOnlyList<LeaseDocumentDto>>> GetDocumentsAsync(Guid actorId, IReadOnlyList<string> roles, Guid leaseId)
    {
        var lease = await _db.LeaseAgreements.AsNoTracking().SingleOrDefaultAsync(l => l.Id == leaseId);
        if (lease is null || !await CanAccessLeaseAsync(actorId, roles, lease))
        {
            return Result.Fail<IReadOnlyList<LeaseDocumentDto>>("Lease not found or you do not have access to it.");
        }

        // SQLite cannot ORDER BY DateTimeOffset in SQL: materialize then order in memory.
        var docs = (await _db.LeaseDocuments.AsNoTracking()
                .Where(d => d.LeaseAgreementId == leaseId)
                .ToListAsync())
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        var result = docs.Select(d => new LeaseDocumentDto
        {
            Id = d.Id,
            LeaseAgreementId = d.LeaseAgreementId,
            FileName = d.FileName,
            ContentType = d.ContentType,
            UploadedByUserId = d.UploadedByUserId,
            Version = d.Version,
            UploadedAt = d.CreatedAt,
        }).ToList();

        return Result.Ok<IReadOnlyList<LeaseDocumentDto>>(result);
    }

    /// <summary>
    /// Background sweep (hosted service): expire past-due leases (FR-LEASE-007) and
    /// notify residents whose lease ends within the notice window (FR-LEASE-006).
    /// Returns the number of leases touched. System-initiated (no user actor).
    /// </summary>
    public async Task<int> RunExpiryLifecycleAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var horizon = now.AddDays(ExpiryNoticeDays);

        var actives = await _db.LeaseAgreements
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        var changed = new List<LeaseAgreement>();
        foreach (var lease in actives)
        {
            // Expire leases that have passed their end date (retained for history).
            if (lease.EndDate < now)
            {
                lease.Status = LeaseStatus.Expired;
                lease.UpdatedAt = now;
                _db.LeaseHistory.Add(new LeaseHistoryEntry
                {
                    LeaseAgreementId = lease.Id,
                    Version = lease.CurrentVersion,
                    ChangeType = "Expired",
                    Description = "Lease reached its end date and was expired.",
                    ChangedByUserId = Guid.Empty,
                });
                changed.Add(lease);
                continue;
            }

            // Notify once per lease when within the notice window and not yet notified.
            if (lease.EndDate <= horizon && lease.EndDate >= now)
            {
                var alreadyNotified = await _db.LeaseHistory.AnyAsync(h =>
                    h.LeaseAgreementId == lease.Id && h.ChangeType == "ExpiryNotice");
                if (!alreadyNotified)
                {
                    _db.LeaseHistory.Add(new LeaseHistoryEntry
                    {
                        LeaseAgreementId = lease.Id,
                        Version = lease.CurrentVersion,
                        ChangeType = "ExpiryNotice",
                        Description = $"Lease expires on {lease.EndDate:yyyy-MM-dd}.",
                        ChangedByUserId = Guid.Empty,
                    });
                    changed.Add(lease);
                    await NotifyUserAsync(lease.ResidentUserId, "Lease expiring soon", $"Your lease ends on {lease.EndDate:yyyy-MM-dd}.");
                }
            }
        }

        if (changed.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return changed.Count;
    }

    // ---- helpers ----

    private async Task<bool> CanAccessLeaseAsync(Guid actorId, IReadOnlyList<string> roles, LeaseAgreement lease)
    {
        if (roles.Contains(AppRoles.Administrator) || lease.ResidentUserId == actorId)
        {
            return true;
        }

        if (!roles.Contains(AppRoles.PropertyManager))
        {
            return false;
        }

        var unit = await _propertyDb.ResidentialUnits
            .AsNoTracking()
            .Include(u => u.Building)
            .ThenInclude(b => b!.Property)
            .SingleOrDefaultAsync(u => u.Id == lease.UnitId);
        return unit is not null && unit.Building!.Property!.ManagerUserId == actorId;
    }

    private async Task<List<Guid>> ScopedLeaseIdsAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (roles.Contains(AppRoles.Administrator))
        {
            return await _db.LeaseAgreements.AsNoTracking().Select(l => l.Id).ToListAsync();
        }

        if (roles.Contains(AppRoles.Resident))
        {
            return await _db.LeaseAgreements.AsNoTracking()
                .Where(l => l.ResidentUserId == actorId)
                .Select(l => l.Id)
                .ToListAsync();
        }

        if (roles.Contains(AppRoles.PropertyManager))
        {
            var managedUnitIds = await _propertyDb.ResidentialUnits
                .AsNoTracking()
                .Where(u => u.Building!.Property!.ManagerUserId == actorId)
                .Select(u => u.Id)
                .ToListAsync();
            return await _db.LeaseAgreements.AsNoTracking()
                .Where(l => managedUnitIds.Contains(l.UnitId))
                .Select(l => l.Id)
                .ToListAsync();
        }

        return new List<Guid>();
    }

    private async Task<IReadOnlyList<LeaseDto>> ToDtosAsync(IEnumerable<LeaseAgreement> leases)
    {
        var list = leases.ToList();
        var result = new List<LeaseDto>();
        foreach (var lease in list)
        {
            result.Add(await ToDtoAsync(lease));
        }

        return result;
    }

    private async Task<LeaseDto> ToDtoAsync(LeaseAgreement lease)
    {
        var unit = await _propertyDb.ResidentialUnits
            .AsNoTracking()
            .Include(u => u.Building)
            .ThenInclude(b => b!.Property)
            .SingleOrDefaultAsync(u => u.Id == lease.UnitId);

        var profile = await _residentDb.ResidentProfiles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == lease.ResidentUserId && !r.IsDeleted);

        var documentCount = await _db.LeaseDocuments.AsNoTracking()
            .CountAsync(d => d.LeaseAgreementId == lease.Id);

        return new LeaseDto
        {
            Id = lease.Id,
            ResidentUserId = lease.ResidentUserId,
            ResidentName = profile is null ? string.Empty : $"{profile.FirstName} {profile.LastName}".Trim(),
            UnitId = lease.UnitId,
            UnitNumber = unit?.UnitNumber ?? string.Empty,
            PropertyId = unit?.Building?.PropertyId ?? Guid.Empty,
            PropertyName = unit?.Building?.Property?.Name ?? string.Empty,
            StartDate = lease.StartDate,
            EndDate = lease.EndDate,
            MonthlyRent = lease.MonthlyRent,
            Status = lease.Status,
            CurrentVersion = lease.CurrentVersion,
            TerminatedAt = lease.TerminatedAt,
            DocumentCount = documentCount,
        };
    }

    private async Task<LeaseDetailDto> BuildDetailAsync(Guid leaseId, Guid actorId, IReadOnlyList<string> roles)
    {
        var lease = await _db.LeaseAgreements.AsNoTracking().SingleOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new InvalidOperationException("Lease not found.");

        var baseDto = await ToDtoAsync(lease);

        var documents = await GetDocumentsAsync(actorId, roles, leaseId);
        var history = await _db.LeaseHistory.AsNoTracking()
            .Where(h => h.LeaseAgreementId == leaseId)
            .ToListAsync();

        return new LeaseDetailDto
        {
            Id = baseDto.Id,
            ResidentUserId = baseDto.ResidentUserId,
            ResidentName = baseDto.ResidentName,
            UnitId = baseDto.UnitId,
            UnitNumber = baseDto.UnitNumber,
            PropertyId = baseDto.PropertyId,
            PropertyName = baseDto.PropertyName,
            StartDate = baseDto.StartDate,
            EndDate = baseDto.EndDate,
            MonthlyRent = baseDto.MonthlyRent,
            Status = baseDto.Status,
            CurrentVersion = baseDto.CurrentVersion,
            TerminatedAt = baseDto.TerminatedAt,
            DocumentCount = baseDto.DocumentCount,
            Documents = documents.Data ?? [],
            History = history
                .OrderByDescending(h => h.Version)
                .Select(h => new LeaseHistoryDto
                {
                    Version = h.Version,
                    ChangeType = h.ChangeType,
                    Description = h.Description,
                    ChangedByUserId = h.ChangedByUserId,
                    ChangedAt = h.ChangedAt,
                })
                .ToList(),
        };
    }

    private async Task NotifyUserAsync(Guid userId, string title, string message)
    {
        await _communication.SendUserNotificationAsync(userId, title, message, "Lease", Communication.Enums.NotificationChannel.InApp);
    }
}

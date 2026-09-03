using Microsoft.EntityFrameworkCore;
using PMP.Modules.Booking.Contracts;
using PMP.Modules.Booking.Data;
using PMP.Modules.Booking.Entities;
using PMP.Modules.Booking.Enums;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Communication.Services;
using PMP.Modules.Property.Data;
using PMP.Modules.Resident.Data;
using PMP.Shared.Common;

namespace PMP.Modules.Booking.Services;

public interface IBookingService
{
    Task<IReadOnlyList<FacilityDto>> GetFacilitiesAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId);

    Task<Result<FacilityDto>> GetFacilityAsync(Guid actorId, IReadOnlyList<string> roles, Guid facilityId);

    Task<Result<FacilityDto>> CreateFacilityAsync(Guid actorId, IReadOnlyList<string> roles, CreateFacilityRequest request);

    Task<Result<FacilityDto>> UpdateFacilityAsync(Guid actorId, IReadOnlyList<string> roles, Guid facilityId, UpdateFacilityRequest request);

    Task<Result<AvailabilityDto>> GetAvailabilityAsync(Guid actorId, IReadOnlyList<string> roles, Guid facilityId, DateTimeOffset date);

    Task<IReadOnlyList<FacilityBookingDto>> GetBookingsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? facilityId, bool mineOnly);

    Task<Result<FacilityBookingDto>> BookAsync(Guid actorId, IReadOnlyList<string> roles, CreateBookingRequest request);

    Task<Result> CancelBookingAsync(Guid actorId, IReadOnlyList<string> roles, Guid bookingId, CancelBookingRequest request);
}

/// <summary>
/// Facility booking (BR-009). Residents can only book facilities in the property they
/// occupy; overlaps are rejected (FR-BOOK-003); cancellations respect the facility's
/// cancellation window (FR-BOOK-004); confirmations/cancellations notify the resident
/// (FR-BOOK-006); booking history is retained (FR-BOOK-007).
/// </summary>
public class BookingService : IBookingService
{
    private readonly BookingDbContext _db;
    private readonly PropertyDbContext _propertyDb;
    private readonly ResidentDbContext _residentDb;
    private readonly ICommunicationService _communication;

    public BookingService(
        BookingDbContext db,
        PropertyDbContext propertyDb,
        ResidentDbContext residentDb,
        ICommunicationService communication)
    {
        _db = db;
        _propertyDb = propertyDb;
        _residentDb = residentDb;
        _communication = communication;
    }

    public async Task<IReadOnlyList<FacilityDto>> GetFacilitiesAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId)
    {
        var scopedIds = await ScopedPropertyIdsAsync(actorId, roles);
        var query = _db.Facilities.AsNoTracking().Where(f => scopedIds.Contains(f.PropertyId));

        if (propertyId.HasValue)
        {
            query = query.Where(f => f.PropertyId == propertyId.Value);
        }

        var facilities = await query.ToListAsync();
        return await ToFacilityDtosAsync(facilities);
    }

    public async Task<Result<FacilityDto>> GetFacilityAsync(Guid actorId, IReadOnlyList<string> roles, Guid facilityId)
    {
        var facility = await _db.Facilities.AsNoTracking().SingleOrDefaultAsync(f => f.Id == facilityId);
        if (facility is null)
        {
            return Result.Fail<FacilityDto>("Facility not found.");
        }

        if (!await CanAccessFacilityAsync(actorId, roles, facility))
        {
            return Result.Fail<FacilityDto>("You do not have access to this facility.");
        }

        var dtos = await ToFacilityDtosAsync([facility]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<FacilityDto>> CreateFacilityAsync(Guid actorId, IReadOnlyList<string> roles, CreateFacilityRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<FacilityDto>("Only a property manager or administrator can configure facilities.");
        }

        var property = await _propertyDb.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PropertyId);
        if (property is null)
        {
            return Result.Fail<FacilityDto>("Property not found.");
        }

        if (!roles.Contains(AppRoles.Administrator) && property.ManagerUserId != actorId)
        {
            return Result.Fail<FacilityDto>("You do not manage this property.");
        }

        if (request.CloseMinutes <= request.OpenMinutes)
        {
            return Result.Fail<FacilityDto>("Closing time must be after opening time.");
        }

        var facility = new Facility
        {
            PropertyId = request.PropertyId,
            Name = request.Name,
            Description = request.Description,
            OpenMinutes = request.OpenMinutes,
            CloseMinutes = request.CloseMinutes,
            SlotMinutes = request.SlotMinutes,
            CancellationWindowHours = request.CancellationWindowHours,
        };

        _db.Facilities.Add(facility);
        await _db.SaveChangesAsync();

        var dtos = await ToFacilityDtosAsync([facility]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<FacilityDto>> UpdateFacilityAsync(Guid actorId, IReadOnlyList<string> roles, Guid facilityId, UpdateFacilityRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<FacilityDto>("Only a property manager or administrator can configure facilities.");
        }

        var facility = await _db.Facilities.SingleOrDefaultAsync(f => f.Id == facilityId);
        if (facility is null || !await CanAccessFacilityAsync(actorId, roles, facility))
        {
            return Result.Fail<FacilityDto>("Facility not found or you do not have access to it.");
        }

        if (request.Name is not null)
        {
            facility.Name = request.Name;
        }

        if (request.Description is not null)
        {
            facility.Description = request.Description;
        }

        if (request.IsActive.HasValue)
        {
            facility.IsActive = request.IsActive.Value;
        }

        if (request.OpenMinutes.HasValue)
        {
            facility.OpenMinutes = request.OpenMinutes.Value;
        }

        if (request.CloseMinutes.HasValue)
        {
            facility.CloseMinutes = request.CloseMinutes.Value;
        }

        if (request.SlotMinutes.HasValue)
        {
            facility.SlotMinutes = request.SlotMinutes.Value;
        }

        if (request.CancellationWindowHours.HasValue)
        {
            facility.CancellationWindowHours = request.CancellationWindowHours.Value;
        }

        if (facility.CloseMinutes <= facility.OpenMinutes)
        {
            return Result.Fail<FacilityDto>("Closing time must be after opening time.");
        }

        facility.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var dtos = await ToFacilityDtosAsync([facility]);
        return Result.Ok(dtos[0]);
    }

    public async Task<Result<AvailabilityDto>> GetAvailabilityAsync(Guid actorId, IReadOnlyList<string> roles, Guid facilityId, DateTimeOffset date)
    {
        var facility = await _db.Facilities.AsNoTracking().SingleOrDefaultAsync(f => f.Id == facilityId);
        if (facility is null || !await CanAccessFacilityAsync(actorId, roles, facility))
        {
            return Result.Fail<AvailabilityDto>("Facility not found or you do not have access to it.");
        }

        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        // SQLite cannot translate DateTimeOffset range comparisons: filter on
        // facility/status in SQL, then the day window in memory.
        var dayCandidates = await _db.FacilityBookings.AsNoTracking()
            .Where(b => b.FacilityId == facilityId && b.Status == BookingStatus.Reserved)
            .ToListAsync();

        var bookings = dayCandidates.Where(b => b.StartAt >= dayStart && b.StartAt < dayEnd).ToList();

        var dtos = await ToBookingDtosAsync(bookings);

        return Result.Ok(new AvailabilityDto
        {
            FacilityId = facilityId,
            Date = dayStart,
            OpenMinutes = facility.OpenMinutes,
            CloseMinutes = facility.CloseMinutes,
            SlotMinutes = facility.SlotMinutes,
            Bookings = dtos,
        });
    }

    public async Task<IReadOnlyList<FacilityBookingDto>> GetBookingsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? facilityId, bool mineOnly)
    {
        var query = _db.FacilityBookings.AsNoTracking();

        if (mineOnly)
        {
            query = query.Where(b => b.BookedByUserId == actorId);
        }
        else
        {
            if (!roles.Contains(AppRoles.Administrator))
            {
                if (!roles.Contains(AppRoles.PropertyManager))
                {
                    return Array.Empty<FacilityBookingDto>();
                }

                var scopedFacilityIds = await ScopedFacilityIdsAsync(actorId, roles);
                query = query.Where(b => scopedFacilityIds.Contains(b.FacilityId));
            }
        }

        if (facilityId.HasValue)
        {
            query = query.Where(b => b.FacilityId == facilityId.Value);
        }

        var rows = await query.ToListAsync();
        return await ToBookingDtosAsync(rows.OrderByDescending(b => b.StartAt));
    }

    public async Task<Result<FacilityBookingDto>> BookAsync(Guid actorId, IReadOnlyList<string> roles, CreateBookingRequest request)
    {
        if (!roles.Contains(AppRoles.Resident))
        {
            return Result.Fail<FacilityBookingDto>("Only residents can book facilities.");
        }

        var facility = await _db.Facilities.AsNoTracking().SingleOrDefaultAsync(f => f.Id == request.FacilityId);
        if (facility is null || !facility.IsActive)
        {
            return Result.Fail<FacilityBookingDto>("Facility not found or not available for booking.");
        }

        if (!await CanAccessFacilityAsync(actorId, roles, facility))
        {
            return Result.Fail<FacilityBookingDto>("You can only book facilities in the property you occupy.");
        }

        if (request.EndAt <= request.StartAt)
        {
            return Result.Fail<FacilityBookingDto>("Booking end must be after start.");
        }

        if (request.StartAt < DateTimeOffset.UtcNow)
        {
            return Result.Fail<FacilityBookingDto>("Cannot book a past time slot.");
        }

        // Respect operating hours (booking rules configured by managers).
        var startMinutes = request.StartAt.TimeOfDay.TotalMinutes;
        var endMinutes = request.EndAt.TimeOfDay.TotalMinutes;
        if (startMinutes < facility.OpenMinutes || endMinutes > facility.CloseMinutes)
        {
            return Result.Fail<FacilityBookingDto>("Booking must be within the facility's operating hours.");
        }

        // FR-BOOK-003: prevent overlapping bookings. SQLite cannot translate
        // DateTimeOffset range comparisons, so load reserved bookings and check
        // overlap in memory.
        var reserved = await _db.FacilityBookings.AsNoTracking()
            .Where(b => b.FacilityId == facility.Id && b.Status == BookingStatus.Reserved)
            .ToListAsync();

        var overlap = reserved.Any(b => b.StartAt < request.EndAt && b.EndAt > request.StartAt);
        if (overlap)
        {
            return Result.Fail<FacilityBookingDto>("This facility is already booked for an overlapping time slot.");
        }

        var booking = new FacilityBooking
        {
            FacilityId = facility.Id,
            BookedByUserId = actorId,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Status = BookingStatus.Reserved,
            BookingReference = $"BK-{Guid.NewGuid():N}".ToUpperInvariant(),
        };

        _db.FacilityBookings.Add(booking);
        await _db.SaveChangesAsync();

        var dtos = await ToBookingDtosAsync([booking]);
        await NotifyBookingUserAsync(actorId, "Booking confirmed", $"Your booking {booking.BookingReference} for {facility.Name} is confirmed.");
        return Result.Ok(dtos[0]);
    }

    public async Task<Result> CancelBookingAsync(Guid actorId, IReadOnlyList<string> roles, Guid bookingId, CancelBookingRequest request)
    {
        var booking = await _db.FacilityBookings.SingleOrDefaultAsync(b => b.Id == bookingId);
        if (booking is null)
        {
            return Result.Fail("Booking not found.");
        }

        var facility = await _db.Facilities.AsNoTracking().SingleOrDefaultAsync(f => f.Id == booking.FacilityId);

        var isManager = roles.Contains(AppRoles.PropertyManager) || roles.Contains(AppRoles.Administrator);
        var isOwner = booking.BookedByUserId == actorId && roles.Contains(AppRoles.Resident);

        if (!isOwner && !isManager)
        {
            return Result.Fail("You are not allowed to cancel this booking.");
        }

        if (booking.Status != BookingStatus.Reserved)
        {
            return Result.Fail("Only reserved bookings can be cancelled.");
        }

        if (isOwner && !isManager)
        {
            // FR-BOOK-004: cancellation window.
            var window = facility?.CancellationWindowHours ?? 2;
            var cutoff = booking.StartAt.AddHours(-window);
            if (DateTimeOffset.UtcNow > cutoff)
            {
                return Result.Fail($"Bookings can be cancelled at least {window} hour(s) before start.");
            }
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTimeOffset.UtcNow;
        booking.CancelledByUserId = actorId;
        booking.CancellationReason = request.Reason;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        await NotifyBookingUserAsync(booking.BookedByUserId, "Booking cancelled", $"Your booking for {facility?.Name ?? "facility"} was cancelled.");
        return Result.Ok();
    }

    // ---- helpers ----

    /// <summary>Properties a resident occupies OR properties a manager manages (or all for admin).</summary>
    private async Task<List<Guid>> ScopedPropertyIdsAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        if (roles.Contains(AppRoles.Administrator))
        {
            return await _propertyDb.Properties.AsNoTracking().Select(p => p.Id).ToListAsync();
        }

        if (roles.Contains(AppRoles.PropertyManager))
        {
            return await _propertyDb.Properties.AsNoTracking()
                .Where(p => p.ManagerUserId == actorId)
                .Select(p => p.Id)
                .ToListAsync();
        }

        if (roles.Contains(AppRoles.Resident))
        {
            var profile = await _residentDb.ResidentProfiles.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == actorId && !r.IsDeleted);
            if (profile is null)
            {
                return new List<Guid>();
            }

            var activeUnitIds = await _residentDb.ResidentUnits.AsNoTracking()
                .Where(ru => ru.ResidentProfileId == profile.Id && ru.MoveOutDate == null && !ru.IsDeleted)
                .Select(ru => ru.UnitId)
                .ToListAsync();

            if (activeUnitIds.Count == 0)
            {
                return new List<Guid>();
            }

            return await _propertyDb.ResidentialUnits.AsNoTracking()
                .Where(u => activeUnitIds.Contains(u.Id))
                .Select(u => u.Building!.PropertyId)
                .Distinct()
                .ToListAsync();
        }

        return new List<Guid>();
    }

    private async Task<bool> CanAccessFacilityAsync(Guid actorId, IReadOnlyList<string> roles, Facility facility)
    {
        var scoped = await ScopedPropertyIdsAsync(actorId, roles);
        return scoped.Contains(facility.PropertyId);
    }

    private async Task<List<Guid>> ScopedFacilityIdsAsync(Guid actorId, IReadOnlyList<string> roles)
    {
        var scopedPropertyIds = await ScopedPropertyIdsAsync(actorId, roles);
        return await _db.Facilities.AsNoTracking()
            .Where(f => scopedPropertyIds.Contains(f.PropertyId))
            .Select(f => f.Id)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<FacilityDto>> ToFacilityDtosAsync(IEnumerable<Facility> facilities)
    {
        var list = facilities.ToList();
        var propertyIds = list.Select(f => f.PropertyId).Distinct().ToList();
        var names = await _propertyDb.Properties.AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return list.Select(f => new FacilityDto
        {
            Id = f.Id,
            PropertyId = f.PropertyId,
            PropertyName = names.GetValueOrDefault(f.PropertyId) ?? string.Empty,
            Name = f.Name,
            Description = f.Description,
            IsActive = f.IsActive,
            OpenMinutes = f.OpenMinutes,
            CloseMinutes = f.CloseMinutes,
            SlotMinutes = f.SlotMinutes,
            CancellationWindowHours = f.CancellationWindowHours,
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityBookingDto>> ToBookingDtosAsync(IEnumerable<FacilityBooking> bookings)
    {
        var list = bookings.ToList();
        var result = new List<FacilityBookingDto>();
        foreach (var booking in list)
        {
            var facility = await _db.Facilities.AsNoTracking().SingleOrDefaultAsync(f => f.Id == booking.FacilityId);
            var profile = await _residentDb.ResidentProfiles.AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == booking.BookedByUserId && !r.IsDeleted);

            result.Add(new FacilityBookingDto
            {
                Id = booking.Id,
                FacilityId = booking.FacilityId,
                FacilityName = facility?.Name ?? string.Empty,
                BookedByUserId = booking.BookedByUserId,
                BookedByName = profile is null ? string.Empty : $"{profile.FirstName} {profile.LastName}".Trim(),
                StartAt = booking.StartAt,
                EndAt = booking.EndAt,
                Status = booking.Status,
                CancelledAt = booking.CancelledAt,
                CancellationReason = booking.CancellationReason,
                BookingReference = booking.BookingReference,
            });
        }

        return result;
    }

    private async Task NotifyBookingUserAsync(Guid userId, string title, string message)
    {
        await _communication.SendUserNotificationAsync(userId, title, message, "Booking", NotificationChannel.InApp);
    }
}

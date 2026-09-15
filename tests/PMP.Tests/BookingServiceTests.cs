using Microsoft.EntityFrameworkCore;
using PMP.Modules.Booking.Contracts;
using PMP.Modules.Booking.Data;
using PMP.Modules.Booking.Entities;
using PMP.Modules.Booking.Enums;
using PMP.Modules.Booking.Services;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-040 — facility booking rules (FR-BOOK-002/003/004/005/006): residents book only
/// facilities in the property they occupy, overlapping slots are rejected, operating
/// hours and the cancellation window are enforced, and outcomes notify the resident.
/// </summary>
public class BookingServiceTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _otherManagerId = Guid.NewGuid();
    private readonly Guid _residentUserId = Guid.NewGuid();

    private static DbContextOptions<T> NewOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"pmp-booking-{Guid.NewGuid():N}")
            .Options;

    private static DateTimeOffset TomorrowAt(int hour) =>
        new(DateTime.UtcNow.Date.AddDays(1).AddHours(hour), TimeSpan.Zero);

    private async Task<BookingArrange> ArrangeAsync()
    {
        var bookingDb = new BookingDbContext(NewOptions<BookingDbContext>());
        var propertyDb = new PropertyDbContext(NewOptions<PropertyDbContext>());
        var residentDb = new ResidentDbContext(NewOptions<ResidentDbContext>());
        var communication = new RecordingCommunicationService();

        var property = new ManagedProperty { Name = "Sunrise", Address = "1 Greenway", ManagerUserId = _managerId };
        propertyDb.Properties.Add(property);
        var building = new Building { PropertyId = property.Id, Name = "Tower A", Property = property };
        propertyDb.Buildings.Add(building);
        var unit = new ResidentialUnit
        {
            BuildingId = building.Id,
            UnitNumber = "A-101",
            UnitType = "TwoBedroom",
            OperationalStatus = UnitOperationalStatus.Active,
            Building = building,
        };
        propertyDb.ResidentialUnits.Add(unit);
        await propertyDb.SaveChangesAsync();

        var profile = new ResidentProfile
        {
            UserId = _residentUserId,
            Email = "rita@pmp.test",
            FirstName = "Rita",
            LastName = "Resident",
            IsActive = true,
        };
        residentDb.ResidentProfiles.Add(profile);
        residentDb.ResidentUnits.Add(new ResidentUnit
        {
            ResidentProfileId = profile.Id,
            UnitId = unit.Id,
            MoveInDate = DateTimeOffset.UtcNow.AddMonths(-1),
        });
        await residentDb.SaveChangesAsync();

        var facility = new Facility
        {
            PropertyId = property.Id,
            Name = "Gym",
            OpenMinutes = 8 * 60,
            CloseMinutes = 22 * 60,
            SlotMinutes = 60,
            CancellationWindowHours = 2,
            IsActive = true,
        };
        bookingDb.Facilities.Add(facility);
        await bookingDb.SaveChangesAsync();

        var service = new BookingService(bookingDb, propertyDb, residentDb, communication);
        return new BookingArrange(service, bookingDb, propertyDb, communication, property, facility);
    }

    [Fact]
    public async Task CreateFacilityAsync_WhenActorIsNotAManager_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.CreateFacilityAsync(_residentUserId, [AppRoles.Resident],
            new CreateFacilityRequest { PropertyId = ctx.Property.Id, Name = "Pool" });

        Assert.False(result.Succeeded);
        Assert.Contains("property manager or administrator", result.Error);
    }

    [Fact]
    public async Task CreateFacilityAsync_WhenCloseTimeIsNotAfterOpenTime_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.CreateFacilityAsync(_managerId, [AppRoles.PropertyManager],
            new CreateFacilityRequest { PropertyId = ctx.Property.Id, Name = "Pool", OpenMinutes = 600, CloseMinutes = 600 });

        Assert.False(result.Succeeded);
        Assert.Contains("Closing time must be after opening time", result.Error);
    }

    [Fact]
    public async Task CreateFacilityAsync_WhenManagerDoesNotManageTheProperty_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.CreateFacilityAsync(_otherManagerId, [AppRoles.PropertyManager],
            new CreateFacilityRequest { PropertyId = ctx.Property.Id, Name = "Pool" });

        Assert.False(result.Succeeded);
        Assert.Contains("do not manage this property", result.Error);
    }

    [Fact]
    public async Task BookAsync_WhenActorIsNotAResident_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.BookAsync(_managerId, [AppRoles.PropertyManager],
            new CreateBookingRequest { FacilityId = ctx.Facility.Id, StartAt = TomorrowAt(10), EndAt = TomorrowAt(11) });

        Assert.False(result.Succeeded);
        Assert.Contains("Only residents can book facilities", result.Error);
        Assert.Empty(await ctx.BookingDb.FacilityBookings.ToListAsync());
    }

    [Fact]
    public async Task BookAsync_WhenResidentDoesNotOccupyTheFacilityProperty_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.BookAsync(Guid.NewGuid(), [AppRoles.Resident],
            new CreateBookingRequest { FacilityId = ctx.Facility.Id, StartAt = TomorrowAt(10), EndAt = TomorrowAt(11) });

        Assert.False(result.Succeeded);
        Assert.Contains("property you occupy", result.Error);
    }

    [Fact]
    public async Task BookAsync_WhenRequestedSlotIsInThePast_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var yesterday = DateTimeOffset.UtcNow.Date.AddDays(-1);

        var result = await ctx.Service.BookAsync(_residentUserId, [AppRoles.Resident],
            new CreateBookingRequest
            {
                FacilityId = ctx.Facility.Id,
                StartAt = new DateTimeOffset(yesterday.AddHours(10), TimeSpan.Zero),
                EndAt = new DateTimeOffset(yesterday.AddHours(11), TimeSpan.Zero),
            });

        Assert.False(result.Succeeded);
        Assert.Contains("past time slot", result.Error);
    }

    [Fact]
    public async Task BookAsync_WhenRequestedSlotIsOutsideOperatingHours_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.BookAsync(_residentUserId, [AppRoles.Resident],
            new CreateBookingRequest { FacilityId = ctx.Facility.Id, StartAt = TomorrowAt(6), EndAt = TomorrowAt(7) });

        Assert.False(result.Succeeded);
        Assert.Contains("operating hours", result.Error);
    }

    [Fact]
    public async Task BookAsync_WhenSlotOverlapsAReservedBooking_IsRejected()
    {
        var ctx = await ArrangeAsync();
        ctx.BookingDb.FacilityBookings.Add(new FacilityBooking
        {
            FacilityId = ctx.Facility.Id,
            BookedByUserId = Guid.NewGuid(),
            StartAt = TomorrowAt(10),
            EndAt = TomorrowAt(11),
            Status = BookingStatus.Reserved,
            BookingReference = "BK-EXISTING",
        });
        await ctx.BookingDb.SaveChangesAsync();

        var result = await ctx.Service.BookAsync(_residentUserId, [AppRoles.Resident],
            new CreateBookingRequest { FacilityId = ctx.Facility.Id, StartAt = TomorrowAt(10).AddMinutes(30), EndAt = TomorrowAt(11).AddMinutes(30) });

        Assert.False(result.Succeeded);
        Assert.Contains("overlapping", result.Error);
        Assert.Single(await ctx.BookingDb.FacilityBookings.ToListAsync());
    }

    [Fact]
    public async Task BookAsync_WhenValid_ReservesTheSlotAndNotifiesTheResident()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.BookAsync(_residentUserId, [AppRoles.Resident],
            new CreateBookingRequest { FacilityId = ctx.Facility.Id, StartAt = TomorrowAt(10), EndAt = TomorrowAt(11) });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(BookingStatus.Reserved, result.Data!.Status);
        Assert.StartsWith("BK-", result.Data.BookingReference);
        Assert.Contains(ctx.Communication.Sent,
            n => n.UserId == _residentUserId && n.Title == "Booking confirmed" && n.Channel == NotificationChannel.InApp);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenOwnerCancelsInsideTheWindow_IsRejected()
    {
        var ctx = await ArrangeAsync();

        // A slot less than the 2-hour cancellation window away.
        var booking = new FacilityBooking
        {
            FacilityId = ctx.Facility.Id,
            BookedByUserId = _residentUserId,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(30),
            EndAt = DateTimeOffset.UtcNow.AddMinutes(90),
            Status = BookingStatus.Reserved,
            BookingReference = "BK-SOON",
        };
        ctx.BookingDb.FacilityBookings.Add(booking);
        await ctx.BookingDb.SaveChangesAsync();

        var result = await ctx.Service.CancelBookingAsync(_residentUserId, [AppRoles.Resident], booking.Id,
            new CancelBookingRequest { Reason = "Changed my mind" });

        Assert.False(result.Succeeded);
        Assert.Contains("at least 2 hour(s)", result.Error);
        Assert.Equal(BookingStatus.Reserved, (await ctx.BookingDb.FacilityBookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenOwnerCancelsOutsideTheWindow_CancelsAndNotifies()
    {
        var ctx = await ArrangeAsync();
        var booking = new FacilityBooking
        {
            FacilityId = ctx.Facility.Id,
            BookedByUserId = _residentUserId,
            StartAt = DateTimeOffset.UtcNow.AddHours(48),
            EndAt = DateTimeOffset.UtcNow.AddHours(49),
            Status = BookingStatus.Reserved,
            BookingReference = "BK-LATER",
        };
        ctx.BookingDb.FacilityBookings.Add(booking);
        await ctx.BookingDb.SaveChangesAsync();

        var result = await ctx.Service.CancelBookingAsync(_residentUserId, [AppRoles.Resident], booking.Id,
            new CancelBookingRequest { Reason = "Plans changed" });

        Assert.True(result.Succeeded, result.Error);
        var stored = await ctx.BookingDb.FacilityBookings.SingleAsync();
        Assert.Equal(BookingStatus.Cancelled, stored.Status);
        Assert.NotNull(stored.CancelledAt);
        Assert.Contains(ctx.Communication.Sent, n => n.UserId == _residentUserId && n.Title == "Booking cancelled");
    }

    [Fact]
    public async Task CancelBookingAsync_WhenStrangerTriesToCancel_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var booking = new FacilityBooking
        {
            FacilityId = ctx.Facility.Id,
            BookedByUserId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddHours(48),
            EndAt = DateTimeOffset.UtcNow.AddHours(49),
            Status = BookingStatus.Reserved,
            BookingReference = "BK-SOMEONE-ELSE",
        };
        ctx.BookingDb.FacilityBookings.Add(booking);
        await ctx.BookingDb.SaveChangesAsync();

        var result = await ctx.Service.CancelBookingAsync(_residentUserId, [AppRoles.Resident], booking.Id,
            new CancelBookingRequest());

        Assert.False(result.Succeeded);
        Assert.Contains("not allowed to cancel", result.Error);
    }

    [Fact]
    public async Task GetBookingsAsync_WhenResidentRequestsAllBookings_ReturnsNothing()
    {
        var ctx = await ArrangeAsync();
        ctx.BookingDb.FacilityBookings.Add(new FacilityBooking
        {
            FacilityId = ctx.Facility.Id,
            BookedByUserId = _residentUserId,
            StartAt = TomorrowAt(10),
            EndAt = TomorrowAt(11),
            Status = BookingStatus.Reserved,
            BookingReference = "BK-1",
        });
        await ctx.BookingDb.SaveChangesAsync();

        var all = await ctx.Service.GetBookingsAsync(_residentUserId, [AppRoles.Resident], null, mineOnly: false);
        var mine = await ctx.Service.GetBookingsAsync(_residentUserId, [AppRoles.Resident], null, mineOnly: true);

        Assert.Empty(all);
        Assert.Single(mine);
    }

    private sealed record BookingArrange(
        BookingService Service,
        BookingDbContext BookingDb,
        PropertyDbContext PropertyDb,
        RecordingCommunicationService Communication,
        ManagedProperty Property,
        Facility Facility);
}

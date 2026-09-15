using Microsoft.EntityFrameworkCore;
using PMP.Modules.Auth.Data;
using PMP.Modules.Booking.Data;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Security.Contracts;
using PMP.Modules.Security.Data;
using PMP.Modules.Security.Enums;
using PMP.Modules.Security.Services;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-040 — visitor and access-control rules (FR-SEC-001..007, BR-010): only security
/// staff (manager/administrator for the property) may register visitors and grant
/// access; check-in/out follow the visitor state machine; grants are revoked, not deleted.
/// </summary>
public class SecurityServiceTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _otherManagerId = Guid.NewGuid();

    private static DbContextOptions<T> NewOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"pmp-security-{Guid.NewGuid():N}")
            .Options;

    private async Task<SecurityArrange> ArrangeAsync()
    {
        var securityDb = new SecurityDbContext(NewOptions<SecurityDbContext>());
        var authDb = new AuthDbContext(NewOptions<AuthDbContext>());
        var propertyDb = new PropertyDbContext(NewOptions<PropertyDbContext>());
        var bookingDb = new BookingDbContext(NewOptions<BookingDbContext>());

        var propertyA = new ManagedProperty { Name = "Sunrise", Address = "1 Greenway", ManagerUserId = _managerId };
        var propertyB = new ManagedProperty { Name = "Other", Address = "2 Elsewhere", ManagerUserId = _otherManagerId };
        propertyDb.Properties.AddRange(propertyA, propertyB);
        await propertyDb.SaveChangesAsync();

        var service = new SecurityService(securityDb, authDb, propertyDb, bookingDb);
        return new SecurityArrange(service, securityDb, propertyA, propertyB);
    }

    private RegisterVisitorRequest NewVisitor(ManagedProperty property, string firstName = "Vera") =>
        new() { PropertyId = property.Id, FirstName = firstName, LastName = "Visitor", UnitNumber = "A-101" };

    [Fact]
    public async Task RegisterVisitorAsync_WhenActorIsNotSecurityStaff_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var asResident = await ctx.Service.RegisterVisitorAsync(Guid.NewGuid(), [AppRoles.Resident], NewVisitor(ctx.PropertyA));
        var asTechnician = await ctx.Service.RegisterVisitorAsync(Guid.NewGuid(), [AppRoles.Technician], NewVisitor(ctx.PropertyA));

        Assert.False(asResident.Succeeded);
        Assert.False(asTechnician.Succeeded);
        Assert.Contains("security staff", asResident.Error);
        Assert.Empty(await ctx.SecurityDb.Visitors.ToListAsync());
    }

    [Fact]
    public async Task RegisterVisitorAsync_WhenManagerDoesNotManageTheProperty_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.RegisterVisitorAsync(_otherManagerId, [AppRoles.PropertyManager], NewVisitor(ctx.PropertyA));

        Assert.False(result.Succeeded);
        Assert.Contains("do not have access", result.Error);
    }

    [Fact]
    public async Task VisitorJourney_RegisterCheckInCheckOut_RecordsBothTimes()
    {
        var ctx = await ArrangeAsync();

        var registered = await ctx.Service.RegisterVisitorAsync(_managerId, [AppRoles.PropertyManager], NewVisitor(ctx.PropertyA));
        Assert.True(registered.Succeeded, registered.Error);
        Assert.Equal(VisitorStatus.Registered, registered.Data!.Status);

        var checkedIn = await ctx.Service.CheckInVisitorAsync(_managerId, [AppRoles.PropertyManager], registered.Data.Id,
            new CheckInVisitorRequest { Notes = "Photo ID verified" });
        Assert.True(checkedIn.Succeeded, checkedIn.Error);
        Assert.Equal(VisitorStatus.CheckedIn, checkedIn.Data!.Status);
        Assert.NotNull(checkedIn.Data.CheckInAt);

        var checkedOut = await ctx.Service.CheckOutVisitorAsync(_managerId, [AppRoles.PropertyManager], registered.Data.Id);
        Assert.True(checkedOut.Succeeded, checkedOut.Error);
        Assert.Equal(VisitorStatus.CheckedOut, checkedOut.Data!.Status);
        Assert.NotNull(checkedOut.Data.CheckOutAt);
    }

    [Fact]
    public async Task CheckInVisitorAsync_WhenVisitorIsAlreadyCheckedIn_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var visitor = await ctx.Service.RegisterVisitorAsync(_managerId, [AppRoles.PropertyManager], NewVisitor(ctx.PropertyA));
        await ctx.Service.CheckInVisitorAsync(_managerId, [AppRoles.PropertyManager], visitor.Data!.Id, new CheckInVisitorRequest());

        var second = await ctx.Service.CheckInVisitorAsync(_managerId, [AppRoles.PropertyManager], visitor.Data.Id, new CheckInVisitorRequest());

        Assert.False(second.Succeeded);
        Assert.Contains("Only registered visitors", second.Error);
    }

    [Fact]
    public async Task CheckOutVisitorAsync_WhenVisitorIsNotCheckedIn_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var visitor = await ctx.Service.RegisterVisitorAsync(_managerId, [AppRoles.PropertyManager], NewVisitor(ctx.PropertyA));

        var result = await ctx.Service.CheckOutVisitorAsync(_managerId, [AppRoles.PropertyManager], visitor.Data!.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("Only checked-in visitors", result.Error);
    }

    [Fact]
    public async Task GrantAccessAsync_WhenVisitorSubjectHasNoVisitorId_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.GrantAccessAsync(_managerId, [AppRoles.PropertyManager], new GrantAccessRequest
        {
            PropertyId = ctx.PropertyA.Id,
            TargetType = AccessTargetType.Building,
            TargetId = Guid.NewGuid(),
            SubjectType = AccessSubjectType.Visitor,
            VisitorId = null,
        });

        Assert.False(result.Succeeded);
        Assert.Contains("VisitorId is required", result.Error);
    }

    [Fact]
    public async Task GrantAccessAsync_WhenExpiryIsInThePast_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.GrantAccessAsync(_managerId, [AppRoles.PropertyManager], new GrantAccessRequest
        {
            PropertyId = ctx.PropertyA.Id,
            TargetType = AccessTargetType.Unit,
            TargetId = Guid.NewGuid(),
            SubjectType = AccessSubjectType.User,
            SubjectUserId = Guid.NewGuid(),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
        });

        Assert.False(result.Succeeded);
        Assert.Contains("Expiration must be in the future", result.Error);
        Assert.Empty(await ctx.SecurityDb.AccessGrants.ToListAsync());
    }

    [Fact]
    public async Task RevokeAccessAsync_WhenGrantIsAlreadyRevoked_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var granted = await ctx.Service.GrantAccessAsync(_managerId, [AppRoles.PropertyManager], new GrantAccessRequest
        {
            PropertyId = ctx.PropertyA.Id,
            TargetType = AccessTargetType.Building,
            TargetId = Guid.NewGuid(),
            SubjectType = AccessSubjectType.User,
            SubjectUserId = Guid.NewGuid(),
        });
        Assert.True(granted.Succeeded, granted.Error);
        await ctx.Service.RevokeAccessAsync(_managerId, [AppRoles.PropertyManager], granted.Data!.Id);

        var second = await ctx.Service.RevokeAccessAsync(_managerId, [AppRoles.PropertyManager], granted.Data.Id);

        Assert.False(second.Succeeded);
        Assert.Contains("already revoked", second.Error);
        Assert.Single(await ctx.SecurityDb.AccessGrants.ToListAsync());
    }

    [Fact]
    public async Task GetAccessLogAsync_WhenActiveOnly_HidesRevokedGrantsButRetainsThem()
    {
        var ctx = await ArrangeAsync();
        var granted = await ctx.Service.GrantAccessAsync(_managerId, [AppRoles.PropertyManager], new GrantAccessRequest
        {
            PropertyId = ctx.PropertyA.Id,
            TargetType = AccessTargetType.Building,
            TargetId = Guid.NewGuid(),
            SubjectType = AccessSubjectType.User,
            SubjectUserId = Guid.NewGuid(),
        });
        await ctx.Service.RevokeAccessAsync(_managerId, [AppRoles.PropertyManager], granted.Data!.Id);

        var activeOnly = await ctx.Service.GetAccessLogAsync(_managerId, [AppRoles.PropertyManager], null, activeOnly: true);
        var full = await ctx.Service.GetAccessLogAsync(_managerId, [AppRoles.PropertyManager], null, activeOnly: false);

        Assert.Empty(activeOnly);
        Assert.Single(full);
        Assert.NotNull(full[0].RevokedAt);
    }

    [Fact]
    public async Task GetVisitorsAsync_WhenManager_IsScopedToManagedProperties()
    {
        var ctx = await ArrangeAsync();
        await ctx.Service.RegisterVisitorAsync(_managerId, [AppRoles.PropertyManager], NewVisitor(ctx.PropertyA, "Mine"));
        await ctx.Service.RegisterVisitorAsync(_otherManagerId, [AppRoles.PropertyManager], NewVisitor(ctx.PropertyB, "Theirs"));

        var visitors = await ctx.Service.GetVisitorsAsync(_managerId, [AppRoles.PropertyManager], null, activeOnly: false);
        var adminVisitors = await ctx.Service.GetVisitorsAsync(_managerId, [AppRoles.Administrator], null, activeOnly: false);

        Assert.Single(visitors);
        Assert.Equal("Mine", visitors[0].FirstName);
        Assert.Equal(2, adminVisitors.Count);
    }

    private sealed record SecurityArrange(
        SecurityService Service,
        SecurityDbContext SecurityDb,
        ManagedProperty PropertyA,
        ManagedProperty PropertyB);
}

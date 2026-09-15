using Microsoft.EntityFrameworkCore;
using PMP.Modules.Lease.Contracts;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Entities;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Lease.Services;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-040 — lease lifecycle rules (BRULE-LEASE-002/003/005/006, FR-LEASE-004/006/007):
/// one active lease per unit, versioned history on change, termination and expiry that
/// retain the agreement for audit.
/// </summary>
public class LeaseServiceTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _otherManagerId = Guid.NewGuid();
    private readonly Guid _residentUserId = Guid.NewGuid();

    private static DbContextOptions<T> NewOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"pmp-lease-{Guid.NewGuid():N}")
            .Options;

    private async Task<LeaseArrange> ArrangeAsync()
    {
        var leaseDb = new LeaseDbContext(NewOptions<LeaseDbContext>());
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

        residentDb.ResidentProfiles.Add(new ResidentProfile
        {
            UserId = _residentUserId,
            Email = "rita@pmp.test",
            FirstName = "Rita",
            LastName = "Resident",
            IsActive = true,
        });
        await residentDb.SaveChangesAsync();

        var service = new LeaseService(leaseDb, propertyDb, residentDb, communication);
        return new LeaseArrange(service, leaseDb, propertyDb, residentDb, communication, unit);
    }

    private CreateLeaseRequest NewLeaseRequest(ResidentialUnit unit, DateTimeOffset? start = null) =>
        new()
        {
            ResidentUserId = _residentUserId,
            UnitId = unit.Id,
            StartDate = start ?? DateTimeOffset.UtcNow,
            EndDate = (start ?? DateTimeOffset.UtcNow).AddMonths(12),
            MonthlyRent = 1200m,
        };

    [Fact]
    public async Task CreateLeaseAsync_WhenActorIsNotAManager_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.CreateLeaseAsync(_residentUserId, [AppRoles.Resident, AppRoles.Technician],
            NewLeaseRequest(ctx.Unit));

        Assert.False(result.Succeeded);
        Assert.Contains("property manager or administrator", result.Error);
        Assert.Empty(await ctx.LeaseDb.LeaseAgreements.ToListAsync());
    }

    [Fact]
    public async Task CreateLeaseAsync_WhenEndDateIsNotAfterStartDate_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var start = DateTimeOffset.UtcNow;
        var request = NewLeaseRequest(ctx.Unit, start) with { EndDate = start };

        var result = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager], request);

        Assert.False(result.Succeeded);
        Assert.Contains("End date must be after start date", result.Error);
    }

    [Fact]
    public async Task CreateLeaseAsync_WhenManagerDoesNotManageTheUnit_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.CreateLeaseAsync(_otherManagerId, [AppRoles.PropertyManager],
            NewLeaseRequest(ctx.Unit));

        Assert.False(result.Succeeded);
        Assert.Contains("do not have access", result.Error);
    }

    [Fact]
    public async Task CreateLeaseAsync_WhenUnitAlreadyHasAnOverlappingActiveLease_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var start = DateTimeOffset.UtcNow;
        var first = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(ctx.Unit, start));
        Assert.True(first.Succeeded, first.Error);

        var overlapping = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(ctx.Unit, start.AddMonths(1)));

        Assert.False(overlapping.Succeeded);
        Assert.Contains("already has an active lease", overlapping.Error);
        Assert.Single(await ctx.LeaseDb.LeaseAgreements.ToListAsync());
    }

    [Fact]
    public async Task CreateLeaseAsync_WhenValid_PersistsAnActiveLeaseAndItsFirstVersion()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(ctx.Unit));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(LeaseStatus.Active, result.Data!.Status);
        Assert.Equal(1, result.Data.CurrentVersion);
        Assert.Contains(result.Data.History, h => h.ChangeType == "Created");
        Assert.Equal("Rita Resident", result.Data.ResidentName);
    }

    [Fact]
    public async Task UpdateLeaseAsync_IncrementsTheVersionAndAppendsHistory()
    {
        var ctx = await ArrangeAsync();
        var created = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager], NewLeaseRequest(ctx.Unit));

        var updated = await ctx.Service.UpdateLeaseAsync(_managerId, [AppRoles.PropertyManager], created.Data!.Id,
            new UpdateLeaseRequest
            {
                StartDate = created.Data.StartDate,
                EndDate = created.Data.EndDate.AddMonths(1),
                MonthlyRent = 1350m,
            });

        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal(2, updated.Data!.CurrentVersion);
        Assert.Equal(1350m, updated.Data.MonthlyRent);
        Assert.Contains(updated.Data.History, h => h.ChangeType == "Modified");

        var versions = await ctx.LeaseDb.LeaseHistory
            .Where(h => h.LeaseAgreementId == created.Data.Id)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
    }

    [Fact]
    public async Task UpdateLeaseAsync_WhenLeaseIsTerminated_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var created = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager], NewLeaseRequest(ctx.Unit));
        await ctx.Service.TerminateLeaseAsync(_managerId, [AppRoles.PropertyManager], created.Data!.Id,
            new TerminateLeaseRequest { Comment = "Moved out" });

        var update = await ctx.Service.UpdateLeaseAsync(_managerId, [AppRoles.PropertyManager], created.Data.Id,
            new UpdateLeaseRequest
            {
                StartDate = created.Data.StartDate,
                EndDate = created.Data.EndDate,
                MonthlyRent = 999m,
            });

        Assert.False(update.Succeeded);
        Assert.Contains("Terminated leases cannot be modified", update.Error);
    }

    [Fact]
    public async Task TerminateLeaseAsync_WhenActive_TerminatesAndNotifiesTheResident()
    {
        var ctx = await ArrangeAsync();
        var created = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager], NewLeaseRequest(ctx.Unit));

        var result = await ctx.Service.TerminateLeaseAsync(_managerId, [AppRoles.PropertyManager], created.Data!.Id,
            new TerminateLeaseRequest { Comment = "Breach of terms" });

        Assert.True(result.Succeeded, result.Error);
        var lease = await ctx.LeaseDb.LeaseAgreements.SingleAsync(l => l.Id == created.Data.Id);
        Assert.Equal(LeaseStatus.Terminated, lease.Status);
        Assert.NotNull(lease.TerminatedAt);
        Assert.Contains(ctx.Communication.Sent, n => n.UserId == _residentUserId && n.Title == "Lease terminated");
    }

    [Fact]
    public async Task TerminateLeaseAsync_WhenLeaseIsNoLongerActive_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var created = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager], NewLeaseRequest(ctx.Unit));
        await ctx.Service.TerminateLeaseAsync(_managerId, [AppRoles.PropertyManager], created.Data!.Id, new TerminateLeaseRequest());

        var second = await ctx.Service.TerminateLeaseAsync(_managerId, [AppRoles.PropertyManager], created.Data.Id,
            new TerminateLeaseRequest());

        Assert.False(second.Succeeded);
        Assert.Contains("Only an active lease can be terminated", second.Error);
    }

    [Fact]
    public async Task RunExpiryLifecycleAsync_ExpiresPastDueLeasesButRetainsThem()
    {
        var ctx = await ArrangeAsync();
        var expired = new LeaseAgreement
        {
            ResidentUserId = _residentUserId,
            UnitId = ctx.Unit.Id,
            StartDate = DateTimeOffset.UtcNow.AddYears(-2),
            EndDate = DateTimeOffset.UtcNow.AddDays(-1),
            MonthlyRent = 1000m,
            Status = LeaseStatus.Active,
        };
        ctx.LeaseDb.LeaseAgreements.Add(expired);
        await ctx.LeaseDb.SaveChangesAsync();

        var touched = await ctx.Service.RunExpiryLifecycleAsync();

        Assert.Equal(1, touched);
        var lease = await ctx.LeaseDb.LeaseAgreements.SingleAsync(l => l.Id == expired.Id);
        Assert.Equal(LeaseStatus.Expired, lease.Status);
        Assert.Contains(await ctx.LeaseDb.LeaseHistory.ToListAsync(),
            h => h.LeaseAgreementId == expired.Id && h.ChangeType == "Expired");
    }

    [Fact]
    public async Task GetLeaseAsync_WhenCallerHasNoRelationshipToTheLease_IsRejected()
    {
        var ctx = await ArrangeAsync();
        var created = await ctx.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager], NewLeaseRequest(ctx.Unit));

        var result = await ctx.Service.GetLeaseAsync(_otherManagerId, [AppRoles.PropertyManager], created.Data!.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("do not have access", result.Error);
    }

    private sealed record LeaseArrange(
        LeaseService Service,
        LeaseDbContext LeaseDb,
        PropertyDbContext PropertyDb,
        ResidentDbContext ResidentDb,
        RecordingCommunicationService Communication,
        ResidentialUnit Unit);
}

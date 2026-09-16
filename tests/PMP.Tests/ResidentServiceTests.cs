using Microsoft.EntityFrameworkCore;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Contracts;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Modules.Resident.Services;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-040 — resident profile lifecycle and the derived, effective-dated occupancy
/// model (FR-RES-004/005/006, ADR-0010): assignment, transfer, move-out and
/// deactivation must retain history and respect manager scoping.
/// </summary>
public class ResidentServiceTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _otherManagerId = Guid.NewGuid();
    private readonly Guid _residentUserId = Guid.NewGuid();

    private static DbContextOptions<T> NewOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"pmp-resident-{Guid.NewGuid():N}")
            .Options;

    /// <param name="alreadyOccupiesUnit">
    /// Seeds an active association to the manager's first unit. Manager scoping grants
    /// access to a resident only when the resident already occupies one of the units
    /// the manager manages, so most manager-facing scenarios start from that state.
    /// </param>
    private async Task<ResidentArrange> ArrangeAsync(bool alreadyOccupiesUnit = false)
    {
        var residentDb = new ResidentDbContext(NewOptions<ResidentDbContext>());
        var propertyDb = new PropertyDbContext(NewOptions<PropertyDbContext>());

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
        var secondUnit = new ResidentialUnit
        {
            BuildingId = building.Id,
            UnitNumber = "A-102",
            UnitType = "TwoBedroom",
            OperationalStatus = UnitOperationalStatus.Active,
            Building = building,
        };
        propertyDb.ResidentialUnits.AddRange(unit, secondUnit);
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
        if (alreadyOccupiesUnit)
        {
            residentDb.ResidentUnits.Add(new ResidentUnit
            {
                ResidentProfileId = profile.Id,
                UnitId = unit.Id,
                MoveInDate = DateTimeOffset.UtcNow.AddMonths(-1),
            });
        }

        await residentDb.SaveChangesAsync();

        var service = new ResidentService(residentDb, propertyDb);
        return new ResidentArrange(service, residentDb, propertyDb, profile, unit, secondUnit);
    }

    [Fact]
    public async Task CreateProfileForUserAsync_WhenCalledTwice_KeepsASingleProfile()
    {
        var ctx = await ArrangeAsync();

        await ctx.Service.CreateProfileForUserAsync(_residentUserId, "rita@pmp.test", "Rita", "Resident", "555");
        await ctx.Service.CreateProfileForUserAsync(_residentUserId, "rita2@pmp.test", "Rita2", "Resident", "556");

        var stored = await ctx.ResidentDb.ResidentProfiles.ToListAsync();
        Assert.Single(stored);
        Assert.Equal("rita@pmp.test", stored[0].Email);
    }

    [Fact]
    public async Task GetResidentsAsync_WhenManagerDoesNotManageTheOccupiedUnit_ReturnsNothing()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var managerList = await ctx.Service.GetResidentsAsync(_managerId, [AppRoles.PropertyManager], null, null);
        var otherManagerList = await ctx.Service.GetResidentsAsync(_otherManagerId, [AppRoles.PropertyManager], null, null);
        var adminList = await ctx.Service.GetResidentsAsync(_otherManagerId, [AppRoles.Administrator], null, null);

        Assert.Single(managerList);
        Assert.Empty(otherManagerList);
        Assert.Single(adminList);
    }

    [Fact]
    public async Task AssignUnitAsync_WhenManagerHasNoExistingAssociationWithTheResident_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.AssignUnitAsync(_managerId, [AppRoles.PropertyManager], ctx.Profile.Id,
            new AssignUnitRequest { UnitId = ctx.Unit.Id });

        Assert.False(result.Succeeded);
        Assert.Contains("do not have access", result.Error);
        Assert.Empty(await ctx.ResidentDb.ResidentUnits.ToListAsync());
    }

    [Fact]
    public async Task AssignUnitAsync_WhenUnitIsNotActive_RejectsAssignment()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);
        ctx.Unit.OperationalStatus = UnitOperationalStatus.UnderMaintenance;
        await ctx.PropertyDb.SaveChangesAsync();

        var result = await ctx.Service.AssignUnitAsync(_managerId, [AppRoles.PropertyManager], ctx.Profile.Id,
            new AssignUnitRequest { UnitId = ctx.Unit.Id });

        Assert.False(result.Succeeded);
        Assert.Contains("not available", result.Error);

        var association = await ctx.ResidentDb.ResidentUnits.SingleAsync();
        Assert.Equal(ctx.Unit.Id, association.UnitId);
        Assert.Null(association.MoveOutDate);
    }

    [Fact]
    public async Task AssignUnitAsync_WhenManagerDoesNotManageTheUnit_RejectsAssignment()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var result = await ctx.Service.AssignUnitAsync(_otherManagerId, [AppRoles.PropertyManager], ctx.Profile.Id,
            new AssignUnitRequest { UnitId = ctx.Unit.Id });

        Assert.False(result.Succeeded);
        Assert.Contains("do not have access", result.Error);
        Assert.Single(await ctx.ResidentDb.ResidentUnits.ToListAsync());
    }

    [Fact]
    public async Task AssignUnitAsync_WhenTransferringUnits_EndsThePreviousPeriodAndKeepsIt()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var transfer = await ctx.Service.AssignUnitAsync(_managerId, [AppRoles.PropertyManager], ctx.Profile.Id,
            new AssignUnitRequest { UnitId = ctx.SecondUnit.Id });

        Assert.True(transfer.Succeeded, transfer.Error);

        var associations = await ctx.ResidentDb.ResidentUnits.ToListAsync();
        Assert.Equal(2, associations.Count);
        Assert.NotNull(associations.Single(a => a.UnitId == ctx.Unit.Id).MoveOutDate);
        Assert.Null(associations.Single(a => a.UnitId == ctx.SecondUnit.Id).MoveOutDate);
        Assert.Equal(ctx.SecondUnit.Id, transfer.Data!.CurrentUnitId);
    }

    [Fact]
    public async Task MoveOutAsync_WhenResidentHasNoActiveOccupancy_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.MoveOutAsync(_managerId, [AppRoles.Administrator], ctx.Profile.Id,
            new MoveOutRequest());

        Assert.False(result.Succeeded);
        Assert.Contains("no active unit association", result.Error);
    }

    [Fact]
    public async Task DeactivateResidentAsync_EndsOccupancyButRetainsTheProfileAndHistory()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var result = await ctx.Service.DeactivateResidentAsync(_managerId, [AppRoles.PropertyManager], ctx.Profile.Id);

        Assert.True(result.Succeeded, result.Error);

        var profile = await ctx.ResidentDb.ResidentProfiles.SingleAsync(p => p.Id == ctx.Profile.Id);
        Assert.False(profile.IsActive);

        var association = await ctx.ResidentDb.ResidentUnits.SingleAsync();
        Assert.NotNull(association.MoveOutDate);
        Assert.False(association.IsDeleted);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenNoProfileExists_ReturnsFailure()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.GetMyProfileAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetResidentsAsync_WithSearch_FiltersByNameOrEmail()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var second = new ResidentProfile
        {
            UserId = Guid.NewGuid(),
            Email = "bob@pmp.test",
            FirstName = "Bob",
            LastName = "Builder",
            IsActive = true,
        };
        ctx.ResidentDb.ResidentProfiles.Add(second);
        ctx.ResidentDb.ResidentUnits.Add(new ResidentUnit
        {
            ResidentProfileId = second.Id,
            UnitId = ctx.Unit.Id,
            MoveInDate = DateTimeOffset.UtcNow,
        });
        await ctx.ResidentDb.SaveChangesAsync();

        var byName = await ctx.Service.GetResidentsAsync(_managerId, [AppRoles.PropertyManager], "rita", null);
        var byEmail = await ctx.Service.GetResidentsAsync(_managerId, [AppRoles.PropertyManager], "bob@", null);

        var nameMatch = Assert.Single(byName);
        Assert.Equal("Rita", nameMatch.FirstName);

        var emailMatch = Assert.Single(byEmail);
        Assert.Equal("Bob", emailMatch.FirstName);
    }

    [Fact]
    public async Task UpdateResidentAsync_UpdatesProfileFields()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var result = await ctx.Service.UpdateResidentAsync(_managerId, [AppRoles.PropertyManager], ctx.Profile.Id,
            new UpdateResidentProfileRequest { FirstName = "Rita2", LastName = "Smith", PhoneNumber = "555-999" });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Rita2", result.Data!.FirstName);
        Assert.Equal("Smith", result.Data.LastName);
        Assert.Equal("555-999", result.Data.PhoneNumber);

        var stored = await ctx.ResidentDb.ResidentProfiles.SingleAsync();
        Assert.Equal("Rita2", stored.FirstName);
        Assert.Equal("Smith", stored.LastName);
    }

    [Fact]
    public async Task UpdateResidentAsync_WhenManagerDoesNotManageTheResident_IsRejected()
    {
        var ctx = await ArrangeAsync(alreadyOccupiesUnit: true);

        var result = await ctx.Service.UpdateResidentAsync(_otherManagerId, [AppRoles.PropertyManager], ctx.Profile.Id,
            new UpdateResidentProfileRequest { FirstName = "X", LastName = "Y" });

        Assert.False(result.Succeeded);
        Assert.Contains("do not have access", result.Error);
    }

    private sealed record ResidentArrange(
        ResidentService Service,
        ResidentDbContext ResidentDb,
        PropertyDbContext PropertyDb,
        ResidentProfile Profile,
        ResidentialUnit Unit,
        ResidentialUnit SecondUnit);
}

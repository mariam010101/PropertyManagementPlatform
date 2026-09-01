using Microsoft.EntityFrameworkCore;
using PMP.Modules.Property.Contracts;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Enums;
using PMP.Modules.Property.Services;
using PMP.Shared.Common;

namespace PMP.Tests;

public class PropertyServiceTests
{
    private static PropertyDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PropertyDbContext>()
            .UseInMemoryDatabase($"pmp-property-{Guid.NewGuid():N}")
            .Options;
        return new PropertyDbContext(options);
    }

    [Fact]
    public async Task CreateProperty_Building_Unit_And_DuplicateUnitNumber_Rejected()
    {
        var db = CreateDb();
        var service = new PropertyService(db, new StubOccupancyProvider());
        var managerId = Guid.NewGuid();

        var property = await service.CreatePropertyAsync(managerId, new CreatePropertyRequest
        {
            Name = "Sunrise",
            Address = "1 Greenway",
            ManagerUserId = managerId,
        });
        Assert.True(property.Succeeded);

        var building = await service.CreateBuildingAsync(
            managerId, new[] { AppRoles.PropertyManager }, property.Data!.Id,
            new CreateBuildingRequest { Name = "Tower A" });
        Assert.True(building.Succeeded);

        var unit1 = await service.CreateUnitAsync(
            managerId, new[] { AppRoles.PropertyManager }, building.Data!.Id,
            new CreateUnitRequest { UnitNumber = "A-101", UnitType = "TwoBedroom", OperationalStatus = UnitOperationalStatus.Active });
        Assert.True(unit1.Succeeded);

        var duplicate = await service.CreateUnitAsync(
            managerId, new[] { AppRoles.PropertyManager }, building.Data!.Id,
            new CreateUnitRequest { UnitNumber = "A-101", UnitType = "TwoBedroom", OperationalStatus = UnitOperationalStatus.Active });
        Assert.False(duplicate.Succeeded);
        Assert.Contains("already exists", duplicate.Error);
    }

    [Fact]
    public async Task Manager_Scoping_OnlySeesOwnProperties()
    {
        var db = CreateDb();
        var service = new PropertyService(db, new StubOccupancyProvider());
        var managerId = Guid.NewGuid();
        var otherManager = Guid.NewGuid();

        await service.CreatePropertyAsync(managerId, new CreatePropertyRequest
        {
            Name = "Mine",
            Address = "1 A",
            ManagerUserId = managerId,
        });

        var list = await service.GetPropertiesAsync(otherManager, new[] { AppRoles.PropertyManager });
        Assert.Empty(list);

        var adminList = await service.GetPropertiesAsync(otherManager, new[] { AppRoles.Administrator });
        Assert.Single(adminList);
    }
}

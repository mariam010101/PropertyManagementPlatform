using PMP.Modules.Property.Contracts;
using PMP.Modules.Property.Enums;

namespace PMP.Tests.Integration;

/// <summary>
/// IMP-003 — property API behaviour not covered by the MVP journey: the
/// duplicate-unit-number rule is enforced through the HTTP surface (the unit
/// service rule is already covered by <c>PropertyServiceTests</c>).
/// </summary>
[Collection(PmpApiCollection.Name)]
public class PropertyApiTests
{
    private readonly PmpApiFixture _api;

    public PropertyApiTests(PmpApiFixture api) => _api = api;

    [Fact]
    public async Task DuplicateUnitNumber_InSameBuilding_IsRejected()
    {
        var manager = await _api.LoginAsync("manager@pmp.com", "Manager123!");

        var createProperty = await _api.PostAsync("/api/properties", new CreatePropertyRequest
        {
            Name = "IT Duplicate Property",
            Address = "1 Integration Way",
            ManagerUserId = manager.UserId,
        }, manager.AccessToken);
        Assert.True(createProperty.Code == 200, $"Property creation failed: {createProperty}");
        var property = _api.Deserialize<PropertyDto>(createProperty.Body);

        var createBuilding = await _api.PostAsync($"/api/properties/{property.Id}/buildings",
            new CreateBuildingRequest { Name = "Tower IT" }, manager.AccessToken);
        Assert.True(createBuilding.Code == 200, $"Building creation failed: {createBuilding}");
        var building = _api.Deserialize<BuildingDto>(createBuilding.Body);

        var unit = new CreateUnitRequest
        {
            UnitNumber = "IT-101",
            UnitType = "TwoBedroom",
            OperationalStatus = UnitOperationalStatus.Active,
        };

        var first = await _api.PostAsync($"/api/buildings/{building.Id}/units", unit, manager.AccessToken);
        Assert.True(first.Code == 200, $"Unit creation failed: {first}");

        var duplicate = await _api.PostAsync($"/api/buildings/{building.Id}/units", unit, manager.AccessToken);
        Assert.True(duplicate.Code == 400, $"Duplicate unit number must be rejected but got {duplicate}");
        Assert.Contains("already exists", duplicate.Body, StringComparison.OrdinalIgnoreCase);
    }
}

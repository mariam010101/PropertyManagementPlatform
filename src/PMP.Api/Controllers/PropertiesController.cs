using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Property.Contracts;
using PMP.Modules.Property.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = AppPolicies.ManagerOrAdmin)]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _properties;
    private readonly CurrentUser _currentUser;

    public PropertiesController(IPropertyService properties, CurrentUser currentUser)
    {
        _properties = properties;
        _currentUser = currentUser;
    }

    [HttpGet("properties")]
    public async Task<IActionResult> GetProperties()
    {
        var result = await _properties.GetPropertiesAsync(_currentUser.Id, _currentUser.Roles);
        return Ok(result);
    }

    [HttpGet("properties/{propertyId:guid}")]
    public async Task<IActionResult> GetProperty(Guid propertyId)
    {
        var result = await _properties.GetPropertyAsync(_currentUser.Id, _currentUser.Roles, propertyId);
        return result.ToActionResult();
    }

    [HttpPost("properties")]
    public async Task<IActionResult> CreateProperty([FromBody] CreatePropertyRequest request)
    {
        var result = await _properties.CreatePropertyAsync(_currentUser.Id, request);
        return result.ToActionResult();
    }

    [HttpPut("properties/{propertyId:guid}")]
    public async Task<IActionResult> UpdateProperty(Guid propertyId, [FromBody] UpdatePropertyRequest request)
    {
        var result = await _properties.UpdatePropertyAsync(_currentUser.Id, _currentUser.Roles, propertyId, request);
        return result.ToActionResult();
    }

    [HttpGet("properties/{propertyId:guid}/buildings")]
    public async Task<IActionResult> GetBuildings(Guid propertyId)
    {
        var result = await _properties.GetBuildingsAsync(_currentUser.Id, _currentUser.Roles, propertyId);
        return Ok(result);
    }

    [HttpPost("properties/{propertyId:guid}/buildings")]
    public async Task<IActionResult> CreateBuilding(Guid propertyId, [FromBody] CreateBuildingRequest request)
    {
        var result = await _properties.CreateBuildingAsync(_currentUser.Id, _currentUser.Roles, propertyId, request);
        return result.ToActionResult();
    }

    [HttpPut("properties/{propertyId:guid}/buildings/{buildingId:guid}")]
    public async Task<IActionResult> UpdateBuilding(Guid propertyId, Guid buildingId, [FromBody] UpdateBuildingRequest request)
    {
        var result = await _properties.UpdateBuildingAsync(_currentUser.Id, _currentUser.Roles, propertyId, buildingId, request);
        return result.ToActionResult();
    }

    [HttpGet("buildings/{buildingId:guid}/units")]
    public async Task<IActionResult> GetUnits(Guid buildingId)
    {
        var result = await _properties.GetUnitsAsync(_currentUser.Id, _currentUser.Roles, buildingId);
        return Ok(result);
    }

    [HttpGet("units/{unitId:guid}")]
    public async Task<IActionResult> GetUnit(Guid unitId)
    {
        var result = await _properties.GetUnitAsync(_currentUser.Id, _currentUser.Roles, unitId);
        return result.ToActionResult();
    }

    [HttpPost("buildings/{buildingId:guid}/units")]
    public async Task<IActionResult> CreateUnit(Guid buildingId, [FromBody] CreateUnitRequest request)
    {
        var result = await _properties.CreateUnitAsync(_currentUser.Id, _currentUser.Roles, buildingId, request);
        return result.ToActionResult();
    }

    [HttpPut("units/{unitId:guid}")]
    public async Task<IActionResult> UpdateUnit(Guid unitId, [FromBody] UpdateUnitRequest request)
    {
        var result = await _properties.UpdateUnitAsync(_currentUser.Id, _currentUser.Roles, unitId, request);
        return result.ToActionResult();
    }
}

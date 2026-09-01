using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Resident.Contracts;
using PMP.Modules.Resident.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

[ApiController]
[Route("api/residents")]
public class ResidentsController : ControllerBase
{
    private readonly IResidentService _residents;
    private readonly CurrentUser _currentUser;

    public ResidentsController(IResidentService residents, CurrentUser currentUser)
    {
        _residents = residents;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetResidents([FromQuery] string? search, [FromQuery] bool? activeOnly)
    {
        var result = await _residents.GetResidentsAsync(_currentUser.Id, _currentUser.Roles, search, activeOnly);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _residents.GetMyProfileAsync(_currentUser.Id);
        return result.ToActionResult();
    }

    [HttpGet("{residentId:guid}")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetResident(Guid residentId)
    {
        var result = await _residents.GetResidentAsync(_currentUser.Id, _currentUser.Roles, residentId);
        return result.ToActionResult();
    }

    [HttpPut("{residentId:guid}")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateResident(Guid residentId, [FromBody] UpdateResidentProfileRequest request)
    {
        var result = await _residents.UpdateResidentAsync(_currentUser.Id, _currentUser.Roles, residentId, request);
        return result.ToActionResult();
    }

    [HttpPost("{residentId:guid}/assign-unit")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> AssignUnit(Guid residentId, [FromBody] AssignUnitRequest request)
    {
        var result = await _residents.AssignUnitAsync(_currentUser.Id, _currentUser.Roles, residentId, request);
        return result.ToActionResult();
    }

    [HttpPost("{residentId:guid}/move-out")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> MoveOut(Guid residentId, [FromBody] MoveOutRequest request)
    {
        var result = await _residents.MoveOutAsync(_currentUser.Id, _currentUser.Roles, residentId, request);
        return result.ToActionResult();
    }

    [HttpPost("{residentId:guid}/deactivate")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> Deactivate(Guid residentId)
    {
        var result = await _residents.DeactivateResidentAsync(_currentUser.Id, _currentUser.Roles, residentId);
        return result.ToActionResult();
    }
}

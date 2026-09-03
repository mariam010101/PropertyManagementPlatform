using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Security.Contracts;
using PMP.Modules.Security.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

/// <summary>
/// Physical security & visitor management (BR-010). Staff (manager/admin) act as
/// authorized security personnel: register visitors, check in/out, and grant/revoke
/// access to buildings/units/facilities, all recorded in audit logs.
/// </summary>
[ApiController]
[Route("api/security")]
[Authorize]
public class SecurityController : ControllerBase
{
    private readonly ISecurityService _security;
    private readonly CurrentUser _currentUser;

    public SecurityController(ISecurityService security, CurrentUser currentUser)
    {
        _security = security;
        _currentUser = currentUser;
    }

    /// <summary>Register a visitor prior to/at arrival (FR-SEC-001).</summary>
    [HttpPost("visitors")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> RegisterVisitor([FromBody] RegisterVisitorRequest request)
    {
        var result = await _security.RegisterVisitorAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>Check in a registered visitor (FR-SEC-002/003).</summary>
    [HttpPost("visitors/{visitorId:guid}/check-in")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CheckInVisitor(Guid visitorId, [FromBody] CheckInVisitorRequest request)
    {
        var result = await _security.CheckInVisitorAsync(_currentUser.Id, _currentUser.Roles, visitorId, request);
        return result.ToActionResult();
    }

    /// <summary>Check out a visitor, recording the exit time (FR-SEC-003).</summary>
    [HttpPost("visitors/{visitorId:guid}/check-out")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CheckOutVisitor(Guid visitorId)
    {
        var result = await _security.CheckOutVisitorAsync(_currentUser.Id, _currentUser.Roles, visitorId);
        return result.ToActionResult();
    }

    /// <summary>Visitor log (FR-SEC-006).</summary>
    [HttpGet("visitors")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetVisitors([FromQuery] Guid? propertyId, [FromQuery] bool activeOnly = false)
    {
        var result = await _security.GetVisitorsAsync(_currentUser.Id, _currentUser.Roles, propertyId, activeOnly);
        return Ok(result);
    }

    /// <summary>Grant access to a building/unit/facility (FR-SEC-004), optional expiry (FR-SEC-007).</summary>
    [HttpPost("access")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GrantAccess([FromBody] GrantAccessRequest request)
    {
        var result = await _security.GrantAccessAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>Revoke access (FR-SEC-004).</summary>
    [HttpPost("access/{accessGrantId:guid}/revoke")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> RevokeAccess(Guid accessGrantId)
    {
        var result = await _security.RevokeAccessAsync(_currentUser.Id, _currentUser.Roles, accessGrantId);
        return result.ToActionResult();
    }

    /// <summary>Access grant/revocation audit log (FR-SEC-005/006).</summary>
    [HttpGet("access")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetAccessLog([FromQuery] Guid? propertyId, [FromQuery] bool activeOnly = false)
    {
        var result = await _security.GetAccessLogAsync(_currentUser.Id, _currentUser.Roles, propertyId, activeOnly);
        return Ok(result);
    }
}

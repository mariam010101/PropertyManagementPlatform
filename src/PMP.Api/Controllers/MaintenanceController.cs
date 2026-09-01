using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Maintenance.Contracts;
using PMP.Modules.Maintenance.Enums;
using PMP.Modules.Maintenance.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenance;
    private readonly CurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public MaintenanceController(IMaintenanceService maintenance, CurrentUser currentUser, IWebHostEnvironment env)
    {
        _maintenance = maintenance;
        _currentUser = currentUser;
        _env = env;
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Resident)]
    public async Task<IActionResult> Submit([FromBody] CreateMaintenanceRequestRequest request)
    {
        var result = await _maintenance.SubmitAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ResidentOrStaff)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] MaintenanceStatus? status,
        [FromQuery] MaintenancePriority? priority,
        [FromQuery] Guid? unitId)
    {
        var result = await _maintenance.GetRequestsAsync(_currentUser.Id, _currentUser.Roles, status, priority, unitId);
        return Ok(result);
    }

    [HttpGet("{requestId:guid}")]
    [Authorize(Policy = AppPolicies.ResidentOrStaff)]
    public async Task<IActionResult> GetRequest(Guid requestId)
    {
        var result = await _maintenance.GetRequestAsync(_currentUser.Id, _currentUser.Roles, requestId);
        return result.ToActionResult();
    }

    [HttpPost("{requestId:guid}/assign")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> Assign(Guid requestId, [FromBody] AssignMaintenanceRequest request)
    {
        var result = await _maintenance.AssignAsync(_currentUser.Id, _currentUser.Roles, requestId, request);
        return result.ToActionResult();
    }

    [HttpPost("{requestId:guid}/status")]
    [Authorize(Policy = AppPolicies.ResidentOrStaff)]
    public async Task<IActionResult> UpdateStatus(Guid requestId, [FromBody] UpdateMaintenanceStatusRequest request)
    {
        var result = await _maintenance.UpdateStatusAsync(_currentUser.Id, _currentUser.Roles, requestId, request);
        return result.ToActionResult();
    }

    [HttpPost("{requestId:guid}/priority")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> SetPriority(Guid requestId, [FromBody] SetMaintenancePriorityRequest request)
    {
        var result = await _maintenance.SetPriorityAsync(_currentUser.Id, _currentUser.Roles, requestId, request);
        return result.ToActionResult();
    }

    [HttpPost("{requestId:guid}/confirm")]
    [Authorize(Policy = AppPolicies.ResidentOrStaff)]
    public async Task<IActionResult> Confirm(Guid requestId, [FromBody] ConfirmCompletionRequest request)
    {
        var result = await _maintenance.ConfirmAsync(_currentUser.Id, _currentUser.Roles, requestId, request);
        return result.ToActionResult();
    }

    [HttpPost("{requestId:guid}/attachments")]
    [Authorize(Policy = AppPolicies.ResidentOrStaff)]
    public async Task<IActionResult> UploadAttachment(Guid requestId, IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "A file is required." });
        }

        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var fullPath = Path.Combine(uploadsDir, safeName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var result = await _maintenance.AddAttachmentAsync(
            _currentUser.Id,
            _currentUser.Roles,
            requestId,
            file.FileName,
            file.ContentType,
            fullPath);

        return result.ToActionResult();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Lease.Contracts;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Lease.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

/// <summary>
/// Lease & document management (BR-006): leases, secure document uploads, version
/// history, termination, and expiry lifecycle.
/// </summary>
[ApiController]
[Route("api/leases")]
[Authorize]
public class LeasesController : ControllerBase
{
    private readonly ILeaseService _leases;
    private readonly CurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public LeasesController(ILeaseService leases, CurrentUser currentUser, IWebHostEnvironment env)
    {
        _leases = leases;
        _currentUser = currentUser;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeases([FromQuery] LeaseStatus? status)
    {
        var result = await _leases.GetLeasesAsync(_currentUser.Id, _currentUser.Roles, status);
        return Ok(result);
    }

    [HttpGet("{leaseId:guid}")]
    public async Task<IActionResult> GetLease(Guid leaseId)
    {
        var result = await _leases.GetLeaseAsync(_currentUser.Id, _currentUser.Roles, leaseId);
        return result.ToActionResult();
    }

    /// <summary>Manager/admin creates a lease (FR-LEASE-001).</summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateLease([FromBody] CreateLeaseRequest request)
    {
        var result = await _leases.CreateLeaseAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>Manager/admin updates lease terms (new version, FR-LEASE-004/005).</summary>
    [HttpPut("{leaseId:guid}")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateLease(Guid leaseId, [FromBody] UpdateLeaseRequest request)
    {
        var result = await _leases.UpdateLeaseAsync(_currentUser.Id, _currentUser.Roles, leaseId, request);
        return result.ToActionResult();
    }

    [HttpPost("{leaseId:guid}/terminate")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> TerminateLease(Guid leaseId, [FromBody] TerminateLeaseRequest request)
    {
        var result = await _leases.TerminateLeaseAsync(_currentUser.Id, _currentUser.Roles, leaseId, request);
        return result.ToActionResult();
    }

    /// <summary>Upload a lease document (FR-LEASE-002/003).</summary>
    [HttpPost("{leaseId:guid}/documents")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UploadDocument(Guid leaseId, IFormFile file)
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

        var result = await _leases.UploadDocumentAsync(_currentUser.Id, _currentUser.Roles, leaseId, file.FileName, file.ContentType, fullPath);
        return result.ToActionResult();
    }

    /// <summary>List documents for a lease (BRULE-LEASE-004).</summary>
    [HttpGet("{leaseId:guid}/documents")]
    public async Task<IActionResult> GetDocuments(Guid leaseId)
    {
        var result = await _leases.GetDocumentsAsync(_currentUser.Id, _currentUser.Roles, leaseId);
        return result.ToActionResult();
    }

    /// <summary>Admin trigger for the expiry/archive sweep (FR-LEASE-006/007).</summary>
    [HttpPost("run-lifecycle")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> RunLifecycle()
    {
        var touched = await _leases.RunExpiryLifecycleAsync();
        return Ok(new { touched });
    }
}

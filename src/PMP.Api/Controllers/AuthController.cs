using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Auth.Contracts;
using PMP.Modules.Auth.Services;
using PMP.Modules.Resident.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IResidentService _residents;
    private readonly CurrentUser _currentUser;

    public AuthController(IAuthService auth, IResidentService residents, CurrentUser currentUser)
    {
        _auth = auth;
        _residents = residents;
        _currentUser = currentUser;
    }

    /// <summary>Self-registration for residents (FR-AUTH-001).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.RegisterResidentAsync(request, ip);
        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        // Composition root: create the resident profile for the new user.
        await _residents.CreateProfileForUserAsync(result.Data!.UserId, result.Data.Email, request.FirstName, request.LastName, request.PhoneNumber);

        return Ok(result.Data);
    }

    /// <summary>Confirm email after registration (email verification).</summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var result = await _auth.ConfirmEmailAsync(userId, token);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(request, ip);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.RefreshAsync(request.RefreshToken, ip);
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var result = await _auth.LogoutAsync(request.RefreshToken, null);
        return result.ToActionResult();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _auth.ChangePasswordAsync(_currentUser.Id, request);
        return result.ToActionResult();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // In a real deployment the token is emailed; here it is returned for dev.
        var result = await _auth.ForgotPasswordAsync(request.Email);
        return result.Succeeded
            ? Ok(new { resetToken = result.Data })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _auth.ResetPasswordAsync(request);
        return result.ToActionResult();
    }

    /// <summary>Admin provisions staff accounts (Property Manager / Technician / Administrator).</summary>
    [HttpPost("staff")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.CreateStaffAsync(request, ip);
        return result.ToActionResult();
    }

    /// <summary>List staff users (optionally filtered by role), e.g. technicians for assignment.</summary>
    [HttpGet("staff")]
    [Authorize]
    public async Task<IActionResult> GetStaff([FromQuery] string? role)
    {
        var result = await _auth.GetStaffAsync(role);
        return Ok(result);
    }

    /// <summary>Admin lists all user accounts with their roles (FR-RBAC-001, ROLE-US-001..004).</summary>
    [HttpGet("users")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> GetUsers([FromQuery] string? search)
    {
        var result = await _auth.GetUsersAsync(search);
        return Ok(result);
    }

    /// <summary>Admin sets a user's roles (assign/remove).</summary>
    [HttpPut("users/{userId:guid}/roles")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> SetUserRoles(Guid userId, [FromBody] SetUserRolesRequest request)
    {
        var result = await _auth.SetUserRolesAsync(_currentUser.Id, userId, request);
        return result.ToActionResult();
    }

    /// <summary>Admin deactivates a user account (soft delete; blocks sign-in, preserves history).</summary>
    [HttpPost("users/{userId:guid}/deactivate")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> DeactivateUser(Guid userId)
    {
        var result = await _auth.DeactivateUserAsync(_currentUser.Id, userId);
        return result.ToActionResult();
    }

    /// <summary>Admin reactivates a user account.</summary>
    [HttpPost("users/{userId:guid}/activate")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> ActivateUser(Guid userId)
    {
        var result = await _auth.ActivateUserAsync(_currentUser.Id, userId);
        return result.ToActionResult();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = _currentUser.Id,
            roles = _currentUser.Roles,
        });
    }
}

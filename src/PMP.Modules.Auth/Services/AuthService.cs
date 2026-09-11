using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMP.Modules.Auth.Contracts;
using PMP.Modules.Auth.Data;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Auth.Options;
using PMP.Shared.Common;
using Microsoft.Extensions.Options;

namespace PMP.Modules.Auth.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterResidentAsync(RegisterRequest request, string? ip);

    Task<Result> ConfirmEmailAsync(Guid userId, string token);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ip);

    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, string? ip);

    Task<Result> LogoutAsync(string refreshToken, string? ip);

    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);

    Task<Result<string>> ForgotPasswordAsync(string email);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);

    Task<Result<AuthResponse>> CreateStaffAsync(CreateStaffRequest request, string? ip);

    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId);

    Task<IReadOnlyList<UserSummaryDto>> GetStaffAsync(string? role);

    Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(string? search);

    Task<Result> SetUserRolesAsync(Guid actorId, Guid userId, SetUserRolesRequest request);

    Task<Result> DeactivateUserAsync(Guid actorId, Guid userId);

    Task<Result> ActivateUserAsync(Guid actorId, Guid userId);

    /// <summary>Identity of the authenticated caller (name/email/roles) for the client shell.</summary>
    Task<Result<MeResponse>> GetCurrentUserAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly AuthDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _jwt;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AuthDbContext db,
        ITokenService tokens,
        IOptions<JwtOptions> jwt)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _tokens = tokens;
        _jwt = jwt.Value;
    }

    public async Task<Result<MeResponse>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Fail<MeResponse>("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Result.Ok(new MeResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles.ToList(),
        });
    }

    public async Task<Result<AuthResponse>> RegisterResidentAsync(RegisterRequest request, string? ip)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result.Fail<AuthResponse>(IdentityErrors(createResult));
        }

        // Residents self-register; staff are provisioned separately (FR-AUTH-001).
        await _userManager.AddToRoleAsync(user, AppRoles.Resident);

        // Email verification required before sign-in (locked security posture).
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        await RecordEventAsync(user.Id, user.Email, "Register", ip);

        var response = await BuildAuthResponseAsync(user, confirmationToken);
        return Result.Ok(response);
    }

    public async Task<Result> ConfirmEmailAsync(Guid userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? Result.Ok()
            : Result.Fail(IdentityErrors(result));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ip)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await RecordEventAsync(null, request.Email, "LoginFailed", ip);
            return Result.Fail<AuthResponse>("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            await RecordEventAsync(user.Id, user.Email, "LoginBlocked", ip);
            return Result.Fail<AuthResponse>("This account has been deactivated.");
        }

        // JWT-only setup: validate credentials/lockout WITHOUT performing a cookie
        // sign-in (PasswordSignInAsync requires a registered cookie handler).
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            await RecordEventAsync(user.Id, user.Email, "LoginLockedOut", ip);
            return Result.Fail<AuthResponse>("Account locked due to too many failed attempts. Try again later.");
        }

        if (!result.Succeeded)
        {
            await RecordEventAsync(user.Id, user.Email, "LoginFailed", ip);
            return Result.Fail<AuthResponse>("Invalid email or password.");
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            return Result.Fail<AuthResponse>("Email not confirmed. Please confirm your email before signing in.");
        }

        await RecordEventAsync(user.Id, user.Email, "Login", ip);
        var response = await BuildAuthResponseAsync(user);
        return Result.Ok(response);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, string? ip)
    {
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == refreshToken);

        if (stored is null || stored.User is null || !stored.IsActive || !stored.User.IsActive)
        {
            return Result.Fail<AuthResponse>("Invalid or expired refresh token.");
        }

        // Rotate the refresh token (revoke current, issue new).
        stored.RevokedAt = DateTimeOffset.UtcNow;
        var newToken = _tokens.GenerateRefreshToken();
        stored.ReplacedByToken = newToken;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            Token = newToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
        });
        await _db.SaveChangesAsync();

        await RecordEventAsync(stored.User.Id, stored.User.Email, "Refresh", ip);

        var roles = await _userManager.GetRolesAsync(stored.User);
        var accessToken = _tokens.CreateAccessToken(stored.User, roles);

        return Result.Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newToken,
            ExpiresInSeconds = _jwt.AccessTokenExpiryMinutes * 60,
            UserId = stored.User.Id,
            Email = stored.User.Email ?? string.Empty,
            FirstName = stored.User.FirstName,
            LastName = stored.User.LastName,
            Roles = roles.ToList(),
        });
    }

    public async Task<Result> LogoutAsync(string refreshToken, string? ip)
    {
        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.Token == refreshToken);
        if (stored is not null)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            await RecordEventAsync(stored.UserId, null, "Logout", ip);
        }

        return Result.Ok();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Fail(IdentityErrors(result));
        }

        await RecordEventAsync(user.Id, user.Email, "PasswordChanged", null);
        return Result.Ok();
    }

    public async Task<Result<string>> ForgotPasswordAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            // Do not reveal whether the account exists.
            return Result.Ok(string.Empty);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return Result.Ok(token);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Result.Fail("Invalid reset request.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Fail(IdentityErrors(result));
        }

        await RecordEventAsync(user.Id, user.Email, "PasswordReset", null);
        return Result.Ok();
    }

    public async Task<Result<AuthResponse>> CreateStaffAsync(CreateStaffRequest request, string? ip)
    {
        if (!request.Role.Equals(AppRoles.PropertyManager, StringComparison.OrdinalIgnoreCase) &&
            !request.Role.Equals(AppRoles.Technician, StringComparison.OrdinalIgnoreCase) &&
            !request.Role.Equals(AppRoles.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<AuthResponse>("Invalid staff role. Use PropertyManager, Technician, or Administrator.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result.Fail<AuthResponse>(IdentityErrors(createResult));
        }

        // Staff accounts are provisioned by an admin; email considered verified.
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        await _userManager.AddToRoleAsync(user, request.Role);

        await RecordEventAsync(user.Id, user.Email, "StaffCreated", ip);
        var response = await BuildAuthResponseAsync(user);
        return Result.Ok(response);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Array.Empty<string>()
            : (await _userManager.GetRolesAsync(user)).ToList();
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetStaffAsync(string? role)
    {
        IReadOnlyList<Guid> userIds;
        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleEntity = await _roleManager.FindByNameAsync(role);
            if (roleEntity is null)
            {
                return Array.Empty<UserSummaryDto>();
            }

            userIds = await _db.UserRoles
                .Where(ur => ur.RoleId == roleEntity.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();
        }
        else
        {
            userIds = await _db.UserRoles.Select(ur => ur.UserId).Distinct().ToListAsync();
        }

        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.LastName)
            .ToListAsync();

        var result = new List<UserSummaryDto>();
        foreach (var user in users)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            result.Add(new UserSummaryDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Role = string.Join(", ", userRoles),
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<UserAdminDto>> GetUsersAsync(string? search)
    {
        var query = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => (u.Email != null && u.Email.ToLower().Contains(term)) ||
                                     u.FirstName.ToLower().Contains(term) ||
                                     u.LastName.ToLower().Contains(term));
        }

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        var roleNameById = await _db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty);
        var groups = await _db.UserRoles.AsNoTracking()
            .Where(ur => users.Select(u => u.Id).Contains(ur.UserId))
            .GroupBy(ur => ur.UserId)
            .ToListAsync();

        var roleByUser = new Dictionary<Guid, List<string>>();
        foreach (var group in groups)
        {
            roleByUser[group.Key] = group
                .Select(ur => roleNameById.GetValueOrDefault(ur.RoleId) ?? string.Empty)
                .Where(n => n.Length > 0)
                .ToList();
        }

        return users.Select(u => new UserAdminDto
        {
            Id = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email ?? string.Empty,
            IsActive = u.IsActive,
            Roles = roleByUser.TryGetValue(u.Id, out var roles) ? roles : Array.Empty<string>(),
            CreatedAt = u.CreatedAt,
        }).ToList();
    }

    public async Task<Result> SetUserRolesAsync(Guid actorId, Guid userId, SetUserRolesRequest request)
    {
        var target = await _userManager.FindByIdAsync(userId.ToString());
        if (target is null)
        {
            return Result.Fail("User not found.");
        }

        var desired = request.Roles.Distinct().ToList();
        var invalid = desired.Where(r => !AppRoles.All.Contains(r)).ToList();
        if (invalid.Count > 0)
        {
            return Result.Fail($"Invalid role(s): {string.Join(", ", invalid)}. Allowed: {string.Join(", ", AppRoles.All)}.");
        }

        var current = (await _userManager.GetRolesAsync(target)).ToList();

        // Removing Administrator: never allow an admin to strip their own role,
        // and always keep at least one active Administrator.
        if (current.Contains(AppRoles.Administrator) && !desired.Contains(AppRoles.Administrator))
        {
            if (userId == actorId)
            {
                return Result.Fail("You cannot remove the Administrator role from your own account.");
            }

            var otherActiveAdmins = await CountActiveAdministratorsAsync(target.Id);
            if (otherActiveAdmins == 0)
            {
                return Result.Fail("At least one active Administrator must remain.");
            }
        }

        foreach (var role in desired.Except(current))
        {
            await _userManager.AddToRoleAsync(target, role);
        }

        foreach (var role in current.Except(desired))
        {
            await _userManager.RemoveFromRoleAsync(target, role);
        }

        target.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(target);

        await RecordEventAsync(target.Id, target.Email, "RolesUpdated", null);
        return Result.Ok();
    }

    public async Task<Result> DeactivateUserAsync(Guid actorId, Guid userId)
    {
        if (userId == actorId)
        {
            return Result.Fail("You cannot deactivate your own account.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (!user.IsActive)
        {
            return Result.Ok();
        }

        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        // Revoke all active refresh tokens so a deactivated account cannot mint new access tokens.
        var activeTokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();
        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        await RecordEventAsync(user.Id, user.Email, "AccountDeactivated", null);
        return Result.Ok();
    }

    public async Task<Result> ActivateUserAsync(Guid actorId, Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (user.IsActive)
        {
            return Result.Ok();
        }

        user.IsActive = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        await RecordEventAsync(user.Id, user.Email, "AccountActivated", null);
        return Result.Ok();
    }

    private async Task<int> CountActiveAdministratorsAsync(Guid excludeUserId)
    {
        var adminRole = await _roleManager.FindByNameAsync(AppRoles.Administrator);
        if (adminRole is null)
        {
            return 0;
        }

        var adminUserIds = await _db.UserRoles
            .Where(ur => ur.RoleId == adminRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync();

        return await _userManager.Users
            .CountAsync(u => u.IsActive && adminUserIds.Contains(u.Id) && u.Id != excludeUserId);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, string? emailConfirmationToken = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokens.CreateAccessToken(user, roles);

        var refreshToken = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
        });
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInSeconds = _jwt.AccessTokenExpiryMinutes * 60,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles.ToList(),
            EmailConfirmationToken = emailConfirmationToken,
        };
    }

    private async Task RecordEventAsync(Guid? userId, string? email, string eventType, string? ip)
    {
        _db.AuthEvents.Add(new AuthEvent
        {
            UserId = userId,
            Email = email,
            EventType = eventType,
            IpAddress = ip,
        });
        await _db.SaveChangesAsync();
    }

    private static string IdentityErrors(IdentityResult result)
    {
        return string.Join("; ", result.Errors.Select(e => e.Description));
    }
}

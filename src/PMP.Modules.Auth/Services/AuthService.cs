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

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
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

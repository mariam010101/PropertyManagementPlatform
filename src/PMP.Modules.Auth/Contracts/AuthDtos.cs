using System.ComponentModel.DataAnnotations;

namespace PMP.Modules.Auth.Contracts;

public record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Phone, MaxLength(32)]
    public string? PhoneNumber { get; init; }
}

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}

public record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public record ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}

public record CreateStaffRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    /// <summary>One of: PropertyManager, Technician, Administrator.</summary>
    [Required]
    public string Role { get; init; } = string.Empty;
}

public record AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public int ExpiresInSeconds { get; init; }

    public Guid UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Only populated immediately after registration (dev convenience until an
    /// email delivery service is wired in). Must be null in all other responses.
    /// </summary>
    public string? EmailConfirmationToken { get; init; }
}

/// <summary>
/// The authenticated caller's own identity, used by the client to render the signed-in
/// user (name, email, roles) after a reload. Read-only: the identity is sourced from the
/// Auth user record only, so the Auth module never depends on other modules' data.
/// </summary>
public record MeResponse
{
    public Guid UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public record UserSummaryDto
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}

public record UserAdminDto
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; init; }
}

public record SetUserRolesRequest
{
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The authenticated caller's own profile edit (FR-AUTH-005, BRULE-AUTH-006).
/// Mutates the Identity name fields (and phone) only; no role or email changes.
/// </summary>
public record UpdateMyProfileRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Phone, MaxLength(32)]
    public string? PhoneNumber { get; init; }
}

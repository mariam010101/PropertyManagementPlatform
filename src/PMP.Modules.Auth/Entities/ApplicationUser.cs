using Microsoft.AspNetCore.Identity;

namespace PMP.Modules.Auth.Entities;

/// <summary>
/// The platform's user identity. Residents self-register; staff are
/// provisioned by an administrator.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Soft-deactivation flag. Deactivated accounts are blocked from sign-in
    /// but their historical records are preserved.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}

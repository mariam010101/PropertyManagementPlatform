namespace PMP.Modules.Auth.Entities;

/// <summary>
/// A refresh token used to obtain new access tokens without re-authenticating.
/// Tokens are rotated on use and can be revoked on logout.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// The token that replaced this one after rotation.
    /// </summary>
    public string? ReplacedByToken { get; set; }

    public bool IsActive => RevokedAt == null && ExpiresAt > DateTimeOffset.UtcNow;

    public ApplicationUser? User { get; set; }
}

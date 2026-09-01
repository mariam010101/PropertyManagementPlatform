namespace PMP.Modules.Auth.Entities;

/// <summary>
/// Audit record of authentication events (FR-AUTH-008).
/// </summary>
public class AuthEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    /// <summary>Event type, e.g. Register, Login, LoginFailed, Logout, PasswordChanged, PasswordReset, Refresh.</summary>
    public string EventType { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

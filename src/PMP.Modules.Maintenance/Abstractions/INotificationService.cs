namespace PMP.Modules.Maintenance.Abstractions;

/// <summary>
/// Lightweight notification contract used by the MVP (FR-MNT-005). The full
/// Communication module is post-MVP; this is satisfied in the composition root
/// by a stub that logs and (later) sends email/in-app notifications.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(Guid recipientUserId, string title, string message);
}

using PMP.Modules.Communication.Enums;
using PMP.Modules.Maintenance.Abstractions;

namespace PMP.Modules.Communication.Services;

/// <summary>
/// Adapter that lets the Maintenance module's notification contract (INotificationService)
/// write real persisted in-app notifications via the Communication module. Registered in
/// the composition root in place of the MVP logging stub (FR-MNT-005, FR-COM-001).
/// </summary>
public class PersistedNotificationService : INotificationService
{
    private readonly ICommunicationService _communication;

    public PersistedNotificationService(ICommunicationService communication)
    {
        _communication = communication;
    }

    public async Task NotifyAsync(Guid recipientUserId, string title, string message)
    {
        await _communication.SendUserNotificationAsync(
            recipientUserId,
            title,
            message,
            "Maintenance",
            NotificationChannel.InApp);
    }
}

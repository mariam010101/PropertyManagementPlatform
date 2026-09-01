using PMP.Modules.Maintenance.Abstractions;

namespace PMP.Api.Services;

/// <summary>
/// MVP notification stub (FR-MNT-005). Logs notifications; the real
/// Communication module (post-MVP) will persist them and send email/in-app.
/// </summary>
public class LoggingNotificationService : INotificationService
{
    private readonly ILogger<LoggingNotificationService> _logger;

    public LoggingNotificationService(ILogger<LoggingNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(Guid recipientUserId, string title, string message)
    {
        _logger.LogInformation("[NOTIFICATION] user={UserId} title={Title} message={Message}", recipientUserId, title, message);
        return Task.CompletedTask;
    }
}

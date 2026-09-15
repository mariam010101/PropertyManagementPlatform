using PMP.Modules.Communication.Contracts;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Communication.Services;
using PMP.Modules.Maintenance.Abstractions;
using PMP.Modules.Property.Abstractions;
using PMP.Shared.Common;

namespace PMP.Tests;

public class StubOccupancyProvider : IUnitOccupancyProvider
{
    public Task<Dictionary<Guid, bool>> GetOccupancyAsync(IEnumerable<Guid> unitIds)
    {
        return Task.FromResult(unitIds.Distinct().ToDictionary(id => id, _ => false));
    }
}

public class StubNotificationService : INotificationService
{
    public List<(Guid UserId, string Title, string Message)> Sent { get; } = new();

    public Task NotifyAsync(Guid recipientUserId, string title, string message)
    {
        Sent.Add((recipientUserId, title, message));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Records the in-app notifications raised by a module under test. The query members
/// satisfy the interface but are never exercised by the services that only publish.
/// </summary>
public class RecordingCommunicationService : ICommunicationService
{
    public List<(Guid UserId, string Title, string Body, string EventType, NotificationChannel Channel)> Sent { get; } = [];

    public Task<Result> SendUserNotificationAsync(Guid recipientUserId, string title, string body, string eventType, NotificationChannel channel)
    {
        Sent.Add((recipientUserId, title, body, eventType, channel));
        return Task.FromResult(Result.Ok());
    }

    public Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(Guid userId, bool unreadOnly = false) =>
        Task.FromResult<IReadOnlyList<NotificationDto>>([]);

    public Task<UnreadCountDto> GetUnreadCountAsync(Guid userId) => Task.FromResult(new UnreadCountDto());

    public Task<Result> MarkNotificationReadAsync(Guid userId, Guid notificationId) => Task.FromResult(Result.Ok());

    public Task<Result> MarkAllReadAsync(Guid userId) => Task.FromResult(Result.Ok());

    public Task<Result<AnnouncementDto>> PublishAnnouncementAsync(Guid actorId, IReadOnlyList<string> roles, PublishAnnouncementRequest request) =>
        Task.FromResult(Result.Fail<AnnouncementDto>("Not used in this test."));

    public Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId) =>
        Task.FromResult<IReadOnlyList<AnnouncementDto>>([]);

    public Task<IReadOnlyList<AnnouncementDto>> GetResidentAnnouncementsAsync(Guid residentUserId) =>
        Task.FromResult<IReadOnlyList<AnnouncementDto>>([]);
}

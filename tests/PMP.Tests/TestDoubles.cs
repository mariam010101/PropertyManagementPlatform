using PMP.Modules.Maintenance.Abstractions;
using PMP.Modules.Property.Abstractions;

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

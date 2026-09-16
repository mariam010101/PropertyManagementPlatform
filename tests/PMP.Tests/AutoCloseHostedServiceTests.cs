using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PMP.Modules.Maintenance.Background;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Maintenance.Entities;
using PMP.Modules.Maintenance.Enums;

namespace PMP.Tests;

/// <summary>
/// IMP-005 — the 7-day auto-close job (ADR-0009) closes requests left in
/// <see cref="MaintenanceStatus.Completed"/> and unconfirmed for more than 7 days,
/// and leaves newer completed requests untouched.
/// </summary>
public class AutoCloseHostedServiceTests
{
    [Fact]
    public async Task CloseExpiredAsync_ClosesOnlyCompletedRequests_OlderThanSevenDays()
    {
        var db = new MaintenanceDbContext(new DbContextOptionsBuilder<MaintenanceDbContext>()
            .UseInMemoryDatabase($"autoclose-{Guid.NewGuid():N}")
            .Options);

        var expired = new MaintenanceRequest
        {
            Title = "Expired completion",
            Status = MaintenanceStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-8),
        };
        var fresh = new MaintenanceRequest
        {
            Title = "Fresh completion",
            Status = MaintenanceStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.MaintenanceRequests.AddRange(expired, fresh);
        await db.SaveChangesAsync();

        var service = new AutoCloseHostedService(null!, NullLogger<AutoCloseHostedService>.Instance);

        await service.CloseExpiredAsync(db, CancellationToken.None);

        Assert.Equal(MaintenanceStatus.Closed, expired.Status);
        Assert.NotNull(expired.ClosedAt);
        Assert.Equal(MaintenanceStatus.Completed, fresh.Status);
        Assert.Null(fresh.ClosedAt);

        // The auto-close is recorded in history so the state change is auditable.
        var history = await db.MaintenanceHistory
            .Where(h => h.MaintenanceRequestId == expired.Id)
            .ToListAsync();
        Assert.Contains(history, h => h.FromStatus == MaintenanceStatus.Completed && h.ToStatus == MaintenanceStatus.Closed);
    }
}

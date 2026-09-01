using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Maintenance.Entities;
using PMP.Modules.Maintenance.Enums;

namespace PMP.Modules.Maintenance.Background;

/// <summary>
/// Auto-closes maintenance requests left in Completed and unconfirmed for more
/// than 7 days (locked decision during grilling).
/// </summary>
public class AutoCloseHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoCloseHostedService> _logger;
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public AutoCloseHostedService(IServiceScopeFactory scopeFactory, ILogger<AutoCloseHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CloseExpiredAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-close job failed.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CloseExpiredAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>();

        var cutoff = DateTimeOffset.UtcNow.Subtract(GracePeriod);
        var expired = await db.MaintenanceRequests
            .Where(r => r.Status == MaintenanceStatus.Completed &&
                        r.CompletedAt != null &&
                        r.CompletedAt < cutoff &&
                        !r.IsDeleted)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var request in expired)
        {
            request.Status = MaintenanceStatus.Closed;
            request.ClosedAt = DateTimeOffset.UtcNow;
            request.UpdatedAt = DateTimeOffset.UtcNow;

            db.MaintenanceHistory.Add(new MaintenanceHistoryEntry
            {
                MaintenanceRequestId = request.Id,
                FromStatus = MaintenanceStatus.Completed,
                ToStatus = MaintenanceStatus.Closed,
                Comment = "Auto-closed: no resident confirmation within 7 days.",
                ChangedByUserId = Guid.Empty,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Auto-closed {Count} unconfirmed maintenance request(s).", expired.Count);
    }
}

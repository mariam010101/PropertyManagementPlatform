using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMP.Modules.Lease.Services;

namespace PMP.Modules.Lease.Background;

/// <summary>
/// Runs daily: expires past-due leases (FR-LEASE-007) and notifies residents whose
/// lease ends within the notice window (FR-LEASE-006).
/// </summary>
public class LeaseLifecycleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaseLifecycleHostedService> _logger;

    public LeaseLifecycleHostedService(IServiceScopeFactory scopeFactory, ILogger<LeaseLifecycleHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run shortly after startup, then daily.
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseService>();
            var touched = await leases.RunExpiryLifecycleAsync();
            if (touched > 0)
            {
                _logger.LogInformation("Lease lifecycle sweep touched {Count} lease(s).", touched);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lease lifecycle sweep failed.");
        }
    }
}

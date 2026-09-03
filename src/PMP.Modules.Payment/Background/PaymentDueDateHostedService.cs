using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMP.Modules.Payment.Services;

namespace PMP.Modules.Payment.Background;

/// <summary>
/// Runs daily: notifies residents about invoices due within the next few days
/// (FR-PAY-007). Runs once shortly after startup, then daily.
/// </summary>
public class PaymentDueDateHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentDueDateHostedService> _logger;

    public PaymentDueDateHostedService(IServiceScopeFactory scopeFactory, ILogger<PaymentDueDateHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
            var reminded = await payments.SendDueDateRemindersAsync();
            if (reminded > 0)
            {
                _logger.LogInformation("Payment due-date reminders sent to {Count} invoice(s).", reminded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment due-date reminder sweep failed.");
        }
    }
}

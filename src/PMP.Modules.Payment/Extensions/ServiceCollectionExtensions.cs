using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Payment.Background;
using PMP.Modules.Payment.Data;
using PMP.Modules.Payment.Services;

namespace PMP.Modules.Payment.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IPaymentService, PaymentService>();
        services.AddHostedService<PaymentDueDateHostedService>();

        return services;
    }
}

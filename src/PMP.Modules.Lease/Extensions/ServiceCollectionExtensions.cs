using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Lease.Background;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Services;

namespace PMP.Modules.Lease.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeaseModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<LeaseDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<ILeaseService, LeaseService>();
        services.AddHostedService<LeaseLifecycleHostedService>();

        return services;
    }
}

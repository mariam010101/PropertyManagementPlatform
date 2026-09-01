using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Maintenance.Background;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Maintenance.Services;

namespace PMP.Modules.Maintenance.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<MaintenanceDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddHostedService<AutoCloseHostedService>();

        return services;
    }
}

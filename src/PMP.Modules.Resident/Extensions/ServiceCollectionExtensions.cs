using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Resident.Abstractions;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Services;

namespace PMP.Modules.Resident.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResidentModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<ResidentDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IResidentService, ResidentService>();
        services.AddScoped<UnitOccupancyProvider>();

        return services;
    }
}

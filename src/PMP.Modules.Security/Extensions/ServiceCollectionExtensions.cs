using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Security.Data;
using PMP.Modules.Security.Services;

namespace PMP.Modules.Security.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<SecurityDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<ISecurityService, SecurityService>();

        return services;
    }
}

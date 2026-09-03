using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Communication.Data;
using PMP.Modules.Communication.Services;

namespace PMP.Modules.Communication.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommunicationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<CommunicationDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<ICommunicationService, CommunicationService>();
        services.AddScoped<PersistedNotificationService>();

        return services;
    }
}

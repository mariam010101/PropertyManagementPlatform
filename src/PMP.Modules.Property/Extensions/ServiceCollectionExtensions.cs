using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Services;

namespace PMP.Modules.Property.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPropertyModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<PropertyDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IPropertyService, PropertyService>();

        return services;
    }
}

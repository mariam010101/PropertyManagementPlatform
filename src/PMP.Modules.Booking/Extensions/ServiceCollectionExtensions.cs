using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMP.Modules.Booking.Data;
using PMP.Modules.Booking.Services;

namespace PMP.Modules.Booking.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");

        services.AddDbContext<BookingDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IBookingService, BookingService>();

        return services;
    }
}

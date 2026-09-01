using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMP.Modules.Auth.Data;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Api.Seed;

/// <summary>
/// Applies migrations and seeds roles + demo data (admin, manager, technician,
/// resident, a sample property). Idempotent; failures are non-fatal.
/// </summary>
public class DbSeeder
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(IServiceProvider services, ILogger<DbSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            using var scope = _services.CreateScope();

            var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var propertyDb = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();
            var residentDb = scope.ServiceProvider.GetRequiredService<ResidentDbContext>();
            var maintenanceDb = scope.ServiceProvider.GetRequiredService<MaintenanceDbContext>();

            await authDb.Database.MigrateAsync();
            await propertyDb.Database.MigrateAsync();
            await residentDb.Database.MigrateAsync();
            await maintenanceDb.Database.MigrateAsync();

            await SeedRolesAsync(scope);

            var admin = await EnsureUserAsync(scope, "admin@pmp.com", "Admin123!", AppRoles.Administrator, "System", "Admin");
            var manager = await EnsureUserAsync(scope, "manager@pmp.com", "Manager123!", AppRoles.PropertyManager, "Anna", "Manager");
            var technician = await EnsureUserAsync(scope, "tech@pmp.com", "Tech123!", AppRoles.Technician, "Tom", "Tech");
            var resident = await EnsureUserAsync(scope, "resident@pmp.com", "Resident123!", AppRoles.Resident, "Rita", "Resident");

            await SeedSamplePropertyAsync(scope, manager.Id, resident.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database seeding skipped. Ensure the SQLite connection string is valid (Data Source=pmp.db) and restart the API.");
        }
    }

    private static async Task SeedRolesAsync(IServiceScope scope)
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        IServiceScope scope,
        string email,
        string password,
        string role,
        string firstName,
        string lastName)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            return user;
        }

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to seed user {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return user;
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private async Task SeedSamplePropertyAsync(IServiceScope scope, Guid managerUserId, Guid residentUserId)
    {
        var propertyDb = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();
        var residentDb = scope.ServiceProvider.GetRequiredService<ResidentDbContext>();

        if (await propertyDb.Properties.AnyAsync())
        {
            return;
        }

        var property = new ManagedProperty
        {
            Name = "Sunrise Residences",
            Address = "1 Greenway Ave",
            City = "Yerevan",
            Description = "Demo property seeded for development.",
            ManagerUserId = managerUserId,
        };
        propertyDb.Properties.Add(property);

        var building = new Building
        {
            PropertyId = property.Id,
            Name = "Tower A",
            Address = "1 Greenway Ave, Tower A",
            Floors = 10,
        };
        propertyDb.Buildings.Add(building);

        var unit1 = new ResidentialUnit
        {
            BuildingId = building.Id,
            UnitNumber = "A-101",
            UnitType = "TwoBedroom",
            Bedrooms = 2,
            Bathrooms = 1,
            AreaSqM = 68,
            OperationalStatus = UnitOperationalStatus.Active,
        };
        var unit2 = new ResidentialUnit
        {
            BuildingId = building.Id,
            UnitNumber = "A-102",
            UnitType = "OneBedroom",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaSqM = 45,
            OperationalStatus = UnitOperationalStatus.Active,
        };
        propertyDb.ResidentialUnits.AddRange(unit1, unit2);
        await propertyDb.SaveChangesAsync();

        // Resident profile + assignment to unit A-101.
        var profile = await residentDb.ResidentProfiles.SingleOrDefaultAsync(r => r.UserId == residentUserId);
        if (profile is null)
        {
            profile = new ResidentProfile
            {
                UserId = residentUserId,
                Email = "resident@pmp.com",
                FirstName = "Rita",
                LastName = "Resident",
                PhoneNumber = "+37400000000",
                IsActive = true,
            };
            residentDb.ResidentProfiles.Add(profile);
            await residentDb.SaveChangesAsync();
        }

        var hasAssociation = await residentDb.ResidentUnits.AnyAsync(ru => ru.ResidentProfileId == profile.Id);
        if (!hasAssociation)
        {
            residentDb.ResidentUnits.Add(new ResidentUnit
            {
                ResidentProfileId = profile.Id,
                UnitId = unit1.Id,
                MoveInDate = DateTimeOffset.UtcNow.AddMonths(-3),
            });
            await residentDb.SaveChangesAsync();
        }
    }
}

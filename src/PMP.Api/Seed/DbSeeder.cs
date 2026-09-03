using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMP.Modules.Auth.Data;
using PMP.Modules.Auth.Entities;
using PMP.Modules.Booking.Data;
using PMP.Modules.Booking.Entities;
using PMP.Modules.Communication.Data;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Entities;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Payment.Data;
using PMP.Modules.Payment.Entities;
using PMP.Modules.Payment.Enums;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Modules.Security.Data;
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
            var communicationDb = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
            var leaseDb = scope.ServiceProvider.GetRequiredService<LeaseDbContext>();
            var paymentDb = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var bookingDb = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

            await authDb.Database.MigrateAsync();
            await propertyDb.Database.MigrateAsync();
            await residentDb.Database.MigrateAsync();
            await maintenanceDb.Database.MigrateAsync();
            await communicationDb.Database.MigrateAsync();
            await leaseDb.Database.MigrateAsync();
            await paymentDb.Database.MigrateAsync();
            await bookingDb.Database.MigrateAsync();
            await securityDb.Database.MigrateAsync();

            await SeedRolesAsync(scope);

            var admin = await EnsureUserAsync(scope, "admin@pmp.com", "Admin123!", AppRoles.Administrator, "System", "Admin");
            var manager = await EnsureUserAsync(scope, "manager@pmp.com", "Manager123!", AppRoles.PropertyManager, "Anna", "Manager");
            var technician = await EnsureUserAsync(scope, "tech@pmp.com", "Tech123!", AppRoles.Technician, "Tom", "Tech");
            var resident = await EnsureUserAsync(scope, "resident@pmp.com", "Resident123!", AppRoles.Resident, "Rita", "Resident");
            var accountant = await EnsureUserAsync(scope, "accountant@pmp.com", "Accountant123!", AppRoles.Accountant, "Alice", "Accountant");

            await SeedSamplePropertyAsync(scope, manager.Id, resident.Id);
            await SeedSamplePostMvpAsync(scope, manager.Id, resident.Id);
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

    /// <summary>
    /// Seeds demo data for the post-MVP modules (lease, invoice/facility, booking
    /// facility, welcome notifications) only when the demo property was created.
    /// Idempotent: skips if data already exists.
    /// </summary>
    private async Task SeedSamplePostMvpAsync(IServiceScope scope, Guid managerUserId, Guid residentUserId)
    {
        var propertyDb = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();
        var residentDb = scope.ServiceProvider.GetRequiredService<ResidentDbContext>();
        var leaseDb = scope.ServiceProvider.GetRequiredService<LeaseDbContext>();
        var paymentDb = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var bookingDb = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var communicationDb = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        var property = await propertyDb.Properties.AsNoTracking().FirstOrDefaultAsync();
        var unit = await propertyDb.ResidentialUnits.AsNoTracking().FirstOrDefaultAsync(u => u.UnitNumber == "A-101");
        var profile = await residentDb.ResidentProfiles.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == residentUserId);

        if (property is null || unit is null || profile is null)
        {
            return;
        }

        // --- Lease (BR-006) ---
        var hasLease = await leaseDb.LeaseAgreements.AnyAsync(l => l.ResidentUserId == residentUserId);
        if (!hasLease)
        {
            var lease = new LeaseAgreement
            {
                ResidentUserId = residentUserId,
                UnitId = unit.Id,
                StartDate = DateTimeOffset.UtcNow.AddMonths(-3),
                EndDate = DateTimeOffset.UtcNow.AddMonths(9),
                MonthlyRent = 1200,
                Status = LeaseStatus.Active,
                CurrentVersion = 1,
            };
            leaseDb.LeaseAgreements.Add(lease);
            await leaseDb.SaveChangesAsync();
        }

        // --- Invoice (BR-005) ---
        var activeLease = await leaseDb.LeaseAgreements.AsNoTracking()
            .FirstOrDefaultAsync(l => l.ResidentUserId == residentUserId && l.Status == LeaseStatus.Active);
        var hasInvoice = await paymentDb.Invoices.AnyAsync(i => i.ResidentUserId == residentUserId && i.Status != "Voided");
        if (activeLease is not null && !hasInvoice)
        {
            var now = DateTimeOffset.UtcNow;
            paymentDb.Invoices.Add(new Invoice
            {
                LeaseAgreementId = activeLease.Id,
                ResidentUserId = residentUserId,
                UnitId = unit.Id,
                PropertyId = property.Id,
                PeriodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset),
                PeriodEnd = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(1),
                DueDate = new DateTimeOffset(now.Year, now.Month, 10, 0, 0, 0, now.Offset),
                Amount = activeLease.MonthlyRent,
                Status = "Open",
            });
            await paymentDb.SaveChangesAsync();
        }

        // --- Facility (BR-009) ---
        var hasFacility = await bookingDb.Facilities.AnyAsync(f => f.PropertyId == property.Id);
        if (!hasFacility)
        {
            bookingDb.Facilities.Add(new Facility
            {
                PropertyId = property.Id,
                Name = "Community Gym",
                Description = "Demo facility seeded for development (gym).",
                IsActive = true,
                OpenMinutes = 8 * 60,
                CloseMinutes = 22 * 60,
                SlotMinutes = 60,
                CancellationWindowHours = 2,
            });
            await bookingDb.SaveChangesAsync();
        }

        // --- Welcome notification (BR-007) ---
        var hasWelcome = await communicationDb.Notifications.AnyAsync(n =>
            n.RecipientUserId == residentUserId && n.EventType == "Welcome");
        if (!hasWelcome)
        {
            communicationDb.Notifications.Add(new PMP.Modules.Communication.Entities.Notification
            {
                RecipientUserId = residentUserId,
                Title = "Welcome to PMP",
                Body = "Your account is ready. You can view your lease, pay rent, book facilities, and track maintenance.",
                EventType = "Welcome",
                Channel = PMP.Modules.Communication.Enums.NotificationChannel.InApp,
                DeliveryStatus = PMP.Modules.Communication.Enums.NotificationDeliveryStatus.Sent,
                IsRead = false,
            });
            await communicationDb.SaveChangesAsync();
        }
    }
}

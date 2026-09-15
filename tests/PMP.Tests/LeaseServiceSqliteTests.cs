using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using PMP.Modules.Lease.Contracts;
using PMP.Modules.Lease.Data;
using PMP.Modules.Lease.Enums;
using PMP.Modules.Lease.Services;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// Regression coverage for the lease overlap check (BRULE-LEASE-002). The in-memory
/// provider does not enforce SQL translation rules, so these tests run the exact
/// <see cref="LeaseService.CreateLeaseAsync"/> path against a real SQLite engine to
/// guarantee the enum/status comparison in the overlap predicate actually translates.
/// </summary>
public class LeaseServiceSqliteTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _residentUserId = Guid.NewGuid();

    [Fact]
    public async Task CreateLeaseAsync_OnSqlite_PersistsLeaseWhenNoOverlap()
    {
        await using var db = await LeaseSqliteFixture.CreateAsync(_managerId, _residentUserId);

        var start = DateTimeOffset.UtcNow;
        var result = await db.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(db.Unit.Id, start));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(LeaseStatus.Active, result.Data!.Status);
    }

    [Fact]
    public async Task LeaseStatusQueries_OnSqlite_FilterAndExpirySweep_Translate()
    {
        await using var db = await LeaseSqliteFixture.CreateAsync(_managerId, _residentUserId);

        var created = await db.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(db.Unit.Id, DateTimeOffset.UtcNow));
        Assert.True(created.Succeeded, created.Error);

        // Status filter (BRULE-LEASE-003) and the expiry sweep both compare the
        // persisted status; both must translate against SQLite.
        var active = await db.Service.GetLeasesAsync(_managerId, [AppRoles.PropertyManager], LeaseStatus.Active);
        Assert.Single(active);

        var touched = await db.Service.RunExpiryLifecycleAsync();
        Assert.Equal(0, touched);
    }

    [Fact]
    public async Task CreateLeaseAsync_OnSqlite_RejectsOverlappingActiveLease()
    {
        await using var db = await LeaseSqliteFixture.CreateAsync(_managerId, _residentUserId);

        var start = DateTimeOffset.UtcNow;
        var first = await db.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(db.Unit.Id, start));
        Assert.True(first.Succeeded, first.Error);

        var overlapping = await db.Service.CreateLeaseAsync(_managerId, [AppRoles.PropertyManager],
            NewLeaseRequest(db.Unit.Id, start.AddMonths(1)));

        Assert.False(overlapping.Succeeded);
        Assert.Contains("already has an active lease", overlapping.Error);
    }

    private CreateLeaseRequest NewLeaseRequest(Guid unitId, DateTimeOffset start) => new()
    {
        ResidentUserId = _residentUserId,
        UnitId = unitId,
        StartDate = start,
        EndDate = start.AddMonths(12),
        MonthlyRent = 1200m,
    };

    private sealed class LeaseSqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly LeaseDbContext _leaseDb;
        private readonly PropertyDbContext _propertyDb;
        private readonly ResidentDbContext _residentDb;

        private LeaseSqliteFixture(
            SqliteConnection connection,
            LeaseDbContext leaseDb,
            PropertyDbContext propertyDb,
            ResidentDbContext residentDb,
            LeaseService service,
            ResidentialUnit unit)
        {
            _connection = connection;
            _leaseDb = leaseDb;
            _propertyDb = propertyDb;
            _residentDb = residentDb;
            Service = service;
            Unit = unit;
        }

        public LeaseService Service { get; }

        public ResidentialUnit Unit { get; }

        public static async Task<LeaseSqliteFixture> CreateAsync(Guid managerId, Guid residentUserId)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var leaseDb = new LeaseDbContext(new DbContextOptionsBuilder<LeaseDbContext>()
                .UseSqlite(connection).Options);
            var propertyDb = new PropertyDbContext(new DbContextOptionsBuilder<PropertyDbContext>()
                .UseSqlite(connection).Options);
            var residentDb = new ResidentDbContext(new DbContextOptionsBuilder<ResidentDbContext>()
                .UseSqlite(connection).Options);

            // EnsureCreated skips a context whose database already contains tables
            // (the three contexts share one SQLite file), so create each context's
            // tables explicitly via the relational database creator.
            await leaseDb.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await propertyDb.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await residentDb.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();

            var property = new ManagedProperty { Name = "Sunrise", Address = "1 Greenway", ManagerUserId = managerId };
            propertyDb.Properties.Add(property);
            var building = new Building { PropertyId = property.Id, Name = "Tower A", Property = property };
            propertyDb.Buildings.Add(building);
            var unit = new ResidentialUnit
            {
                BuildingId = building.Id,
                UnitNumber = "A-101",
                UnitType = "TwoBedroom",
                OperationalStatus = UnitOperationalStatus.Active,
                Building = building,
            };
            propertyDb.ResidentialUnits.Add(unit);
            await propertyDb.SaveChangesAsync();

            residentDb.ResidentProfiles.Add(new ResidentProfile
            {
                UserId = residentUserId,
                Email = "rita@pmp.test",
                FirstName = "Rita",
                LastName = "Resident",
                IsActive = true,
            });
            await residentDb.SaveChangesAsync();

            var service = new LeaseService(leaseDb, propertyDb, residentDb, new RecordingCommunicationService());
            return new LeaseSqliteFixture(connection, leaseDb, propertyDb, residentDb, service, unit);
        }

        public async ValueTask DisposeAsync()
        {
            await _leaseDb.DisposeAsync();
            await _propertyDb.DisposeAsync();
            await _residentDb.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using PMP.Modules.Maintenance.Contracts;
using PMP.Modules.Maintenance.Data;
using PMP.Modules.Maintenance.Enums;
using PMP.Modules.Maintenance.Services;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Property.Enums;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Tests;

public class MaintenanceServiceTests
{
    private readonly Guid _residentUserId = Guid.NewGuid();
    private readonly Guid _managerUserId = Guid.NewGuid();
    private readonly Guid _techUserId = Guid.NewGuid();

    private MaintenanceService CreateService(
        MaintenanceDbContext mdb,
        ResidentDbContext rdb,
        PropertyDbContext pdb,
        StubNotificationService notifier)
    {
        return new MaintenanceService(mdb, rdb, pdb, notifier);
    }

    private async Task<(MaintenanceService Service, Guid UnitId, Guid ResidentProfileId, StubNotificationService Notifier)> Arrange()
    {
        var mdb = new MaintenanceDbContext(new DbContextOptionsBuilder<MaintenanceDbContext>()
            .UseInMemoryDatabase($"mnt-{Guid.NewGuid():N}").Options);
        var rdb = new ResidentDbContext(new DbContextOptionsBuilder<ResidentDbContext>()
            .UseInMemoryDatabase($"res-{Guid.NewGuid():N}").Options);
        var pdb = new PropertyDbContext(new DbContextOptionsBuilder<PropertyDbContext>()
            .UseInMemoryDatabase($"prop-{Guid.NewGuid():N}").Options);
        var notifier = new StubNotificationService();

        var property = new ManagedProperty { Name = "P", Address = "A", ManagerUserId = _managerUserId };
        pdb.Properties.Add(property);
        var building = new Building { PropertyId = property.Id, Name = "B" };
        pdb.Buildings.Add(building);
        var unit = new ResidentialUnit
        {
            BuildingId = building.Id,
            UnitNumber = "U1",
            UnitType = "TwoBedroom",
            OperationalStatus = UnitOperationalStatus.Active,
        };
        pdb.ResidentialUnits.Add(unit);
        await pdb.SaveChangesAsync();

        var profile = new ResidentProfile
        {
            UserId = _residentUserId,
            Email = "resident@test.com",
            FirstName = "R",
            LastName = "R",
            IsActive = true,
        };
        rdb.ResidentProfiles.Add(profile);
        rdb.ResidentUnits.Add(new ResidentUnit
        {
            ResidentProfileId = profile.Id,
            UnitId = unit.Id,
            MoveInDate = DateTimeOffset.UtcNow.AddMonths(-1),
        });
        await rdb.SaveChangesAsync();

        return (CreateService(mdb, rdb, pdb, notifier), unit.Id, profile.Id, notifier);
    }

    [Fact]
    public async Task FullLifecycle_Submitted_Assigned_InProgress_Completed_Closed()
    {
        var (service, unitId, _, _) = await Arrange();

        var submit = await service.SubmitAsync(_residentUserId, new[] { AppRoles.Resident },
            new CreateMaintenanceRequestRequest { Title = "Water leak", Description = "Kitchen", UnitId = unitId, Priority = MaintenancePriority.High });
        Assert.True(submit.Succeeded);
        Assert.Equal(MaintenanceStatus.Submitted, submit.Data!.Status);

        var requestId = submit.Data.Id;

        var assign = await service.AssignAsync(_managerUserId, new[] { AppRoles.PropertyManager }, requestId,
            new AssignMaintenanceRequest { TechnicianUserId = _techUserId });
        Assert.True(assign.Succeeded);
        Assert.Equal(MaintenanceStatus.Assigned, assign.Data!.Status);

        var start = await service.UpdateStatusAsync(_techUserId, new[] { AppRoles.Technician }, requestId,
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.InProgress });
        Assert.True(start.Succeeded);

        var complete = await service.UpdateStatusAsync(_techUserId, new[] { AppRoles.Technician }, requestId,
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.Completed });
        Assert.True(complete.Succeeded);

        var confirm = await service.ConfirmAsync(_residentUserId, new[] { AppRoles.Resident }, requestId,
            new ConfirmCompletionRequest());
        Assert.True(confirm.Succeeded);
        Assert.Equal(MaintenanceStatus.Closed, confirm.Data!.Status);
        Assert.Contains(confirm.Data.History, h => h.ToStatus == MaintenanceStatus.Confirmed);
    }

    [Fact]
    public async Task InvalidTransition_SubmittedToCompleted_Rejected()
    {
        var (service, unitId, _, _) = await Arrange();

        var submit = await service.SubmitAsync(_residentUserId, new[] { AppRoles.Resident },
            new CreateMaintenanceRequestRequest { Title = "Leak", UnitId = unitId, Priority = MaintenancePriority.Medium });
        Assert.True(submit.Succeeded);

        var invalid = await service.UpdateStatusAsync(_techUserId, new[] { AppRoles.Technician }, submit.Data!.Id,
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.Completed });
        Assert.False(invalid.Succeeded);
    }

    [Fact]
    public async Task ResidentCannotAssign_And_CannotSubmitForForeignUnit()
    {
        var (service, unitId, _, _) = await Arrange();

        var submit = await service.SubmitAsync(_residentUserId, new[] { AppRoles.Resident },
            new CreateMaintenanceRequestRequest { Title = "Leak", UnitId = unitId, Priority = MaintenancePriority.Medium });
        Assert.True(submit.Succeeded);

        var assignAsResident = await service.AssignAsync(_residentUserId, new[] { AppRoles.Resident }, submit.Data!.Id,
            new AssignMaintenanceRequest { TechnicianUserId = _techUserId });
        Assert.False(assignAsResident.Succeeded);

        var foreignUnit = Guid.NewGuid();
        var foreignSubmit = await service.SubmitAsync(_residentUserId, new[] { AppRoles.Resident },
            new CreateMaintenanceRequestRequest { Title = "Nope", UnitId = foreignUnit, Priority = MaintenancePriority.Low });
        Assert.False(foreignSubmit.Succeeded);
    }

    [Fact]
    public async Task ClosedRequest_IsImmutable_RejectsFurtherTransitions()
    {
        var (service, unitId, _, _) = await Arrange();

        var submit = await service.SubmitAsync(_residentUserId, new[] { AppRoles.Resident },
            new CreateMaintenanceRequestRequest { Title = "Leak", UnitId = unitId, Priority = MaintenancePriority.Medium });
        Assert.True(submit.Succeeded);

        var requestId = submit.Data!.Id;
        await service.AssignAsync(_managerUserId, new[] { AppRoles.PropertyManager }, requestId,
            new AssignMaintenanceRequest { TechnicianUserId = _techUserId });
        await service.UpdateStatusAsync(_techUserId, new[] { AppRoles.Technician }, requestId,
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.InProgress });
        await service.UpdateStatusAsync(_techUserId, new[] { AppRoles.Technician }, requestId,
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.Completed });
        var confirm = await service.ConfirmAsync(_residentUserId, new[] { AppRoles.Resident }, requestId,
            new ConfirmCompletionRequest());

        Assert.True(confirm.Succeeded);
        Assert.Equal(MaintenanceStatus.Closed, confirm.Data!.Status);

        // BRULE-MNT-004: a closed request cannot transition to any other state.
        var reopen = await service.UpdateStatusAsync(_techUserId, new[] { AppRoles.Technician }, requestId,
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.InProgress });
        Assert.False(reopen.Succeeded);

        var reassign = await service.AssignAsync(_managerUserId, new[] { AppRoles.PropertyManager }, requestId,
            new AssignMaintenanceRequest { TechnicianUserId = _techUserId });
        Assert.False(reassign.Succeeded);
    }

    [Fact]
    public async Task StatusChange_PersistsResidentNotification()
    {
        var (service, unitId, _, notifier) = await Arrange();

        var submit = await service.SubmitAsync(_residentUserId, new[] { AppRoles.Resident },
            new CreateMaintenanceRequestRequest { Title = "Leak", UnitId = unitId, Priority = MaintenancePriority.Medium });
        Assert.True(submit.Succeeded);

        var requestId = submit.Data!.Id;
        await service.AssignAsync(_managerUserId, new[] { AppRoles.PropertyManager }, requestId,
            new AssignMaintenanceRequest { TechnicianUserId = _techUserId });

        // The assignment and subsequent status changes must notify the resident
        // through the persisted notification service (FR-MNT-005).
        Assert.NotEmpty(notifier.Sent);
        Assert.Contains(notifier.Sent, n => n.UserId == _residentUserId);
    }
}

using Microsoft.EntityFrameworkCore;
using PMP.Modules.Communication.Contracts;
using PMP.Modules.Communication.Data;
using PMP.Modules.Communication.Entities;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Communication.Services;
using PMP.Modules.Property.Data;
using PMP.Modules.Property.Entities;
using PMP.Modules.Resident.Data;
using PMP.Modules.Resident.Entities;
using PMP.Shared.Common;

namespace PMP.Tests;

/// <summary>
/// IMP-040 — persisted notifications and announcements (FR-COM-001..005,
/// BRULE-COM-001/003/006): notifications are recipient-scoped, delivery status is
/// recorded per channel, and announcements are scoped to a property.
/// </summary>
public class CommunicationServiceTests
{
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _residentUserId = Guid.NewGuid();

    private static DbContextOptions<T> NewOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase($"pmp-comms-{Guid.NewGuid():N}")
            .Options;

    private async Task<CommsArrange> ArrangeAsync()
    {
        var commsDb = new CommunicationDbContext(NewOptions<CommunicationDbContext>());
        var propertyDb = new PropertyDbContext(NewOptions<PropertyDbContext>());
        var residentDb = new ResidentDbContext(NewOptions<ResidentDbContext>());

        var property = new ManagedProperty { Name = "Sunrise", Address = "1 Greenway", ManagerUserId = _managerId };
        propertyDb.Properties.Add(property);
        var building = new Building { PropertyId = property.Id, Name = "Tower A", Property = property };
        propertyDb.Buildings.Add(building);
        var unit = new ResidentialUnit { BuildingId = building.Id, UnitNumber = "A-101", Building = building };
        propertyDb.ResidentialUnits.Add(unit);
        await propertyDb.SaveChangesAsync();

        var profile = new ResidentProfile
        {
            UserId = _residentUserId,
            Email = "rita@pmp.test",
            FirstName = "Rita",
            LastName = "Resident",
            IsActive = true,
        };
        residentDb.ResidentProfiles.Add(profile);
        residentDb.ResidentUnits.Add(new ResidentUnit
        {
            ResidentProfileId = profile.Id,
            UnitId = unit.Id,
            MoveInDate = DateTimeOffset.UtcNow.AddMonths(-1),
        });
        await residentDb.SaveChangesAsync();

        var service = new CommunicationService(commsDb, propertyDb, residentDb);
        return new CommsArrange(service, commsDb, property, profile);
    }

    [Fact]
    public async Task SendUserNotificationAsync_InAppChannel_IsRecordedAsDelivered()
    {
        var ctx = await ArrangeAsync();

        await ctx.Service.SendUserNotificationAsync(_residentUserId, "New payment request", "Rent due", "Payment", NotificationChannel.InApp);

        var notification = await ctx.CommsDb.Notifications.SingleAsync();
        Assert.Equal(NotificationDeliveryStatus.Sent, notification.DeliveryStatus);
        Assert.Equal(_residentUserId, notification.RecipientUserId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task SendUserNotificationAsync_EmailChannel_IsRecordedAsPendingUntilATransportExists()
    {
        var ctx = await ArrangeAsync();

        await ctx.Service.SendUserNotificationAsync(_residentUserId, "Rent reminder", "Due soon", "Payment", NotificationChannel.Email);

        Assert.Equal(NotificationDeliveryStatus.Pending, (await ctx.CommsDb.Notifications.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsOnlyTheCallersNotifications()
    {
        var ctx = await ArrangeAsync();
        var otherUser = Guid.NewGuid();
        await ctx.Service.SendUserNotificationAsync(_residentUserId, "Mine", "Body", "Test", NotificationChannel.InApp);
        await ctx.Service.SendUserNotificationAsync(otherUser, "Theirs", "Body", "Test", NotificationChannel.InApp);

        var mine = await ctx.Service.GetMyNotificationsAsync(_residentUserId);

        Assert.Single(mine);
        Assert.Equal("Mine", mine[0].Title);
    }

    [Fact]
    public async Task MarkNotificationReadAsync_WhenNotificationBelongsToAnotherUser_IsRejected()
    {
        var ctx = await ArrangeAsync();
        await ctx.Service.SendUserNotificationAsync(_residentUserId, "Mine", "Body", "Test", NotificationChannel.InApp);
        var notificationId = (await ctx.CommsDb.Notifications.SingleAsync()).Id;

        var result = await ctx.Service.MarkNotificationReadAsync(Guid.NewGuid(), notificationId);

        Assert.False(result.Succeeded);
        Assert.False((await ctx.CommsDb.Notifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task MarkAllReadAsync_OnlyMarksTheCallersUnreadNotifications()
    {
        var ctx = await ArrangeAsync();
        var otherUser = Guid.NewGuid();
        await ctx.Service.SendUserNotificationAsync(_residentUserId, "Mine 1", "Body", "Test", NotificationChannel.InApp);
        await ctx.Service.SendUserNotificationAsync(_residentUserId, "Mine 2", "Body", "Test", NotificationChannel.InApp);
        await ctx.Service.SendUserNotificationAsync(otherUser, "Theirs", "Body", "Test", NotificationChannel.InApp);

        await ctx.Service.MarkAllReadAsync(_residentUserId);

        Assert.Equal(0, (await ctx.Service.GetUnreadCountAsync(_residentUserId)).Count);
        Assert.Equal(1, (await ctx.Service.GetUnreadCountAsync(otherUser)).Count);
    }

    [Fact]
    public async Task PublishAnnouncementAsync_WhenActorIsNotAManager_IsRejected()
    {
        var ctx = await ArrangeAsync();

        var result = await ctx.Service.PublishAnnouncementAsync(_residentUserId, [AppRoles.Resident],
            new PublishAnnouncementRequest { PropertyId = ctx.Property.Id, Title = "Elevator", Body = "Out of service" });

        Assert.False(result.Succeeded);
        Assert.Contains("property manager or administrator", result.Error);
        Assert.Empty(await ctx.CommsDb.Announcements.ToListAsync());
    }

    [Fact]
    public async Task GetAnnouncementsAsync_WhenManager_IsScopedToManagedProperties()
    {
        var ctx = await ArrangeAsync();
        var otherProperty = new ManagedProperty { Name = "Other", Address = "2 Elsewhere", ManagerUserId = Guid.NewGuid() };
        ctx.CommsDb.Announcements.AddRange(
            new Announcement { PropertyId = ctx.Property.Id, Title = "Mine", Body = "Body", PublishedByUserId = _managerId },
            new Announcement { PropertyId = otherProperty.Id, Title = "Theirs", Body = "Body", PublishedByUserId = Guid.NewGuid() });
        await ctx.CommsDb.SaveChangesAsync();

        var managerAnnouncements = await ctx.Service.GetAnnouncementsAsync(_managerId, [AppRoles.PropertyManager], null);
        var adminAnnouncements = await ctx.Service.GetAnnouncementsAsync(_managerId, [AppRoles.Administrator], null);

        Assert.Single(managerAnnouncements);
        Assert.Equal("Mine", managerAnnouncements[0].Title);
        Assert.Equal(2, adminAnnouncements.Count);
    }

    [Fact]
    public async Task GetResidentAnnouncementsAsync_ReturnsAnnouncementsForThePropertyTheResidentOccupies()
    {
        var ctx = await ArrangeAsync();
        var otherProperty = new ManagedProperty { Name = "Other", Address = "2 Elsewhere", ManagerUserId = Guid.NewGuid() };
        ctx.CommsDb.Announcements.AddRange(
            new Announcement { PropertyId = ctx.Property.Id, Title = "My building", Body = "Body", PublishedByUserId = _managerId },
            new Announcement { PropertyId = otherProperty.Id, Title = "Elsewhere", Body = "Body", PublishedByUserId = Guid.NewGuid() });
        await ctx.CommsDb.SaveChangesAsync();

        var announcements = await ctx.Service.GetResidentAnnouncementsAsync(_residentUserId);

        Assert.Single(announcements);
        Assert.Equal("My building", announcements[0].Title);
    }

    private sealed record CommsArrange(
        CommunicationService Service,
        CommunicationDbContext CommsDb,
        ManagedProperty Property,
        ResidentProfile Profile);
}

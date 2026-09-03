using Microsoft.EntityFrameworkCore;
using PMP.Modules.Communication.Contracts;
using PMP.Modules.Communication.Data;
using PMP.Modules.Communication.Entities;
using PMP.Modules.Communication.Enums;
using PMP.Modules.Property.Data;
using PMP.Modules.Resident.Data;
using PMP.Shared.Common;

namespace PMP.Modules.Communication.Services;

public interface ICommunicationService
{
    Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(Guid userId, bool unreadOnly = false);

    Task<UnreadCountDto> GetUnreadCountAsync(Guid userId);

    Task<Result> MarkNotificationReadAsync(Guid userId, Guid notificationId);

    Task<Result> MarkAllReadAsync(Guid userId);

    Task<Result> SendUserNotificationAsync(Guid recipientUserId, string title, string body, string eventType, NotificationChannel channel);

    Task<Result<AnnouncementDto>> PublishAnnouncementAsync(Guid actorId, IReadOnlyList<string> roles, PublishAnnouncementRequest request);

    Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId);

    Task<IReadOnlyList<AnnouncementDto>> GetResidentAnnouncementsAsync(Guid residentUserId);
}

/// <summary>
/// Persisted communication service (BR-007). Replaces the MVP logging stub so
/// maintenance status changes (FR-MNT-005) and other business events produce real,
/// retrievable in-app notifications (FR-COM-001..006).
/// </summary>
public class CommunicationService : ICommunicationService
{
    private readonly CommunicationDbContext _db;
    private readonly PropertyDbContext _propertyDb;
    private readonly ResidentDbContext _residentDb;

    public CommunicationService(
        CommunicationDbContext db,
        PropertyDbContext propertyDb,
        ResidentDbContext residentDb)
    {
        _db = db;
        _propertyDb = propertyDb;
        _residentDb = residentDb;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(Guid userId, bool unreadOnly = false)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        // SQLite cannot ORDER BY DateTimeOffset: materialize then order in memory.
        var rows = await query.ToListAsync();
        return rows
            .OrderByDescending(n => n.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(Guid userId)
    {
        var count = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
        return new UnreadCountDto { Count = count };
    }

    public async Task<Result> MarkNotificationReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _db.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId);

        if (notification is null)
        {
            return Result.Fail("Notification not found.");
        }

        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> MarkAllReadAsync(Guid userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> SendUserNotificationAsync(Guid recipientUserId, string title, string body, string eventType, NotificationChannel channel)
    {
        // BRULE-COM-001: delivered only to intended recipient (explicit recipient id).
        // BRULE-COM-006: record delivery status. In-app = delivered (Sent); email is
        // recorded Pending because no SMTP provider is configured in this build.
        var status = channel == NotificationChannel.InApp
            ? NotificationDeliveryStatus.Sent
            : NotificationDeliveryStatus.Pending;

        _db.Notifications.Add(new Notification
        {
            RecipientUserId = recipientUserId,
            Title = title,
            Body = body,
            EventType = eventType,
            Channel = channel,
            DeliveryStatus = status,
        });

        await _db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result<AnnouncementDto>> PublishAnnouncementAsync(Guid actorId, IReadOnlyList<string> roles, PublishAnnouncementRequest request)
    {
        if (!roles.Contains(AppRoles.PropertyManager) && !roles.Contains(AppRoles.Administrator))
        {
            return Result.Fail<AnnouncementDto>("Only a property manager or administrator can publish announcements.");
        }

        var property = await _propertyDb.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PropertyId);
        if (property is null)
        {
            return Result.Fail<AnnouncementDto>("Property not found.");
        }

        var announcement = new Announcement
        {
            PropertyId = request.PropertyId,
            Title = request.Title,
            Body = request.Body,
            PublishedByUserId = actorId,
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync();

        return Result.Ok(new AnnouncementDto
        {
            Id = announcement.Id,
            PropertyId = announcement.PropertyId,
            PropertyName = property.Name,
            Title = announcement.Title,
            Body = announcement.Body,
            PublishedByUserId = announcement.PublishedByUserId,
            PublishedAt = announcement.PublishedAt,
        });
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetAnnouncementsAsync(Guid actorId, IReadOnlyList<string> roles, Guid? propertyId)
    {
        // Managers/admins see announcements for their property; admins see all.
        var query = _db.Announcements.AsNoTracking().Where(a => a.IsActive);

        if (propertyId.HasValue)
        {
            query = query.Where(a => a.PropertyId == propertyId.Value);
        }
        else if (!roles.Contains(AppRoles.Administrator))
        {
            var managedIds = await _propertyDb.Properties.AsNoTracking()
                .Where(p => p.ManagerUserId == actorId)
                .Select(p => p.Id)
                .ToListAsync();
            query = query.Where(a => managedIds.Contains(a.PropertyId));
        }

        var rows = await query.ToListAsync();

        var propertyIds = rows.Select(a => a.PropertyId).Distinct().ToList();
        var names = await _propertyDb.Properties.AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return rows
            .OrderByDescending(a => a.PublishedAt)
            .Select(a => new AnnouncementDto
            {
                Id = a.Id,
                PropertyId = a.PropertyId,
                PropertyName = names.GetValueOrDefault(a.PropertyId) ?? string.Empty,
                Title = a.Title,
                Body = a.Body,
                PublishedByUserId = a.PublishedByUserId,
                PublishedAt = a.PublishedAt,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetResidentAnnouncementsAsync(Guid residentUserId)
    {
        // FR-COM-003 / BRULE-COM-003: a resident sees announcements for the property
        // where they currently occupy a unit.
        var profile = await _residentDb.ResidentProfiles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == residentUserId && !r.IsDeleted);

        if (profile is null)
        {
            return Array.Empty<AnnouncementDto>();
        }

        var activeUnitIds = await _residentDb.ResidentUnits.AsNoTracking()
            .Where(ru => ru.ResidentProfileId == profile.Id && ru.MoveOutDate == null && !ru.IsDeleted)
            .Select(ru => ru.UnitId)
            .ToListAsync();

        if (activeUnitIds.Count == 0)
        {
            return Array.Empty<AnnouncementDto>();
        }

        var propertyIds = await _propertyDb.ResidentialUnits.AsNoTracking()
            .Where(u => activeUnitIds.Contains(u.Id))
            .Select(u => u.Building!.PropertyId)
            .Distinct()
            .ToListAsync();

        if (propertyIds.Count == 0)
        {
            return Array.Empty<AnnouncementDto>();
        }

        var rows = await _db.Announcements.AsNoTracking()
            .Where(a => a.IsActive && propertyIds.Contains(a.PropertyId))
            .ToListAsync();

        var names = await _propertyDb.Properties.AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return rows
            .OrderByDescending(a => a.PublishedAt)
            .Select(a => new AnnouncementDto
            {
                Id = a.Id,
                PropertyId = a.PropertyId,
                PropertyName = names.GetValueOrDefault(a.PropertyId) ?? string.Empty,
                Title = a.Title,
                Body = a.Body,
                PublishedByUserId = a.PublishedByUserId,
                PublishedAt = a.PublishedAt,
            })
            .ToList();
    }

    private static NotificationDto ToDto(Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Body = n.Body,
        EventType = n.EventType,
        Channel = n.Channel,
        DeliveryStatus = n.DeliveryStatus,
        IsRead = n.IsRead,
        ReadAt = n.ReadAt,
        CreatedAt = n.CreatedAt,
    };
}

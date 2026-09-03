using PMP.Modules.Communication.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Communication.Entities;

/// <summary>
/// A persisted in-app / email notification (FR-COM-004/005, BRULE-COM-004/006).
/// Delivery is tracked per channel so recipients can see history and read state.
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>Recipient user id (auth.users).</summary>
    public Guid RecipientUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Predefined business event that triggered this notification (BRULE-COM-002).</summary>
    public string EventType { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public NotificationDeliveryStatus DeliveryStatus { get; set; } = NotificationDeliveryStatus.Pending;

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

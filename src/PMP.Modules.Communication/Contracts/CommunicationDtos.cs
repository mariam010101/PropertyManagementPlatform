using System.ComponentModel.DataAnnotations;
using PMP.Modules.Communication.Enums;

namespace PMP.Modules.Communication.Contracts;

public record NotificationDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public NotificationChannel Channel { get; init; }

    public NotificationDeliveryStatus DeliveryStatus { get; init; }

    public bool IsRead { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public record AnnouncementDto
{
    public Guid Id { get; init; }

    public Guid PropertyId { get; init; }

    public string PropertyName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public Guid PublishedByUserId { get; init; }

    public DateTimeOffset PublishedAt { get; init; }
}

public record PublishAnnouncementRequest
{
    [Required]
    public Guid PropertyId { get; init; }

    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

public record UnreadCountDto
{
    public int Count { get; init; }
}

using PMP.Shared.Domain;

namespace PMP.Modules.Communication.Entities;

/// <summary>
/// A property-wide announcement published by an authorized manager (FR-COM-002/003,
/// BRULE-COM-005). Residents of the property receive/see it.
/// </summary>
public class Announcement : BaseEntity
{
    public Guid PropertyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Publisher user id (must hold a manager/administrator role).</summary>
    public Guid PublishedByUserId { get; set; }

    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive { get; set; } = true;
}

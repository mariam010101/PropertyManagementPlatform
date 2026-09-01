namespace PMP.Shared.Domain;

/// <summary>
/// Base entity with a Guid identity and soft-delete/time-stamp support.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete flag (FR-RES-006: deactivate without deleting historical records).
    /// </summary>
    public bool IsDeleted { get; set; }
}

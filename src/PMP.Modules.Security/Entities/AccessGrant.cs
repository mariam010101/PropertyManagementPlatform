using PMP.Modules.Security.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Security.Entities;

/// <summary>
/// An access grant (or revocation) to a building/unit/facility for a resident, staff
/// member, or visitor (FR-SEC-004). Temporary grants carry an expiration (FR-SEC-007).
/// Revocations are recorded, not deleted, so grants/revocations form an audit log
/// (FR-SEC-005/006).
/// </summary>
public class AccessGrant : BaseEntity
{
    public Guid PropertyId { get; set; }

    public AccessTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public AccessSubjectType SubjectType { get; set; }

    /// <summary>User id when SubjectType == User.</summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>Visitor id when SubjectType == Visitor.</summary>
    public Guid? VisitorId { get; set; }

    public Guid GrantedByUserId { get; set; }

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional expiration for temporary access (FR-SEC-007).</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? RevokedByUserId { get; set; }

    public bool IsActive => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);
}

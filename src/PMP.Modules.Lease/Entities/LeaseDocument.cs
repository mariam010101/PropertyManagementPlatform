using PMP.Shared.Domain;

namespace PMP.Modules.Lease.Entities;

/// <summary>
/// A stored lease document (PDF etc.) — BRULE-LEASE-004 (secure storage, accessible
/// only to authorized users), FR-LEASE-002/003. Files are saved outside the DB; the
/// row stores its path + access metadata.
/// </summary>
public class LeaseDocument : BaseEntity
{
    public Guid LeaseAgreementId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }

    public int Version { get; set; } = 1;

    public LeaseAgreement? LeaseAgreement { get; set; }
}

using PMP.Shared.Domain;

namespace PMP.Modules.Resident.Entities;

/// <summary>
/// A resident's property-domain profile. One-to-one with an auth user account;
/// auto-created at registration, then completed by an admin/manager.
/// </summary>
public class ResidentProfile : BaseEntity
{
    /// <summary>Corresponding auth.users id.</summary>
    public Guid UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary>Profile is active unless deactivated (FR-RES-006) — soft delete only.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ResidentUnit> UnitAssociations { get; set; } = new List<ResidentUnit>();
}

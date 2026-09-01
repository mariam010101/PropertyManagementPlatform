using PMP.Shared.Domain;

namespace PMP.Modules.Property.Entities;

/// <summary>
/// A managed property. Every property has exactly one assigned manager
/// (BRULE-PROP-003); a manager may manage multiple properties.
/// </summary>
public class ManagedProperty : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? Description { get; set; }

    /// <summary>User id of the assigned property manager (auth.users).</summary>
    public Guid ManagerUserId { get; set; }

    public ICollection<Building> Buildings { get; set; } = new List<Building>();
}

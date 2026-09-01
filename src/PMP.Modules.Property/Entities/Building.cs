using PMP.Shared.Domain;

namespace PMP.Modules.Property.Entities;

/// <summary>
/// A building within a property. Every building belongs to one property
/// (BRULE-PROP-001).
/// </summary>
public class Building : BaseEntity
{
    public Guid PropertyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public int? Floors { get; set; }

    public ManagedProperty? Property { get; set; }

    public ICollection<ResidentialUnit> Units { get; set; } = new List<ResidentialUnit>();
}

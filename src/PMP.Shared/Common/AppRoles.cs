namespace PMP.Shared.Common;

/// <summary>
/// System roles used across the platform (RBAC).
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string PropertyManager = "PropertyManager";
    public const string Resident = "Resident";
    public const string Technician = "Technician";

    public static readonly string[] All = [Administrator, PropertyManager, Resident, Technician];

    public static bool IsKnown(string role) => All.Contains(role);
}

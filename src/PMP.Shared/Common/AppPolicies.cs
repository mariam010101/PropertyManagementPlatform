namespace PMP.Shared.Common;

/// <summary>
/// Authorization policy names. Policies combine roles and, where needed,
/// ownership/data-scoping checks performed in services.
/// </summary>
public static class AppPolicies
{
    public const string AdministratorOnly = "AdministratorOnly";
    public const string StaffOnly = "StaffOnly"; // PropertyManager or Technician
    public const string ManagerOrAdmin = "ManagerOrAdmin";
    public const string Authenticated = "Authenticated";
    public const string ResidentOrStaff = "ResidentOrStaff";
}

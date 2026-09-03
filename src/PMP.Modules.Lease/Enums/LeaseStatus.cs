namespace PMP.Modules.Lease.Enums;

/// <summary>
/// Lease lifecycle (BRULE-LEASE-003/006). Expired leases remain stored for history.
/// </summary>
public enum LeaseStatus
{
    Active = 0,
    Expired = 1,
    Terminated = 2,
    Cancelled = 3,
}

namespace PMP.Modules.Maintenance.Enums;

/// <summary>
/// Lifecycle of a maintenance request (locked during grilling):
/// Submitted → Assigned → In Progress → Completed → Confirmed / Closed (+ Cancelled).
/// </summary>
public enum MaintenanceStatus
{
    Submitted = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Confirmed = 4,
    Closed = 5,
    Cancelled = 6,
}

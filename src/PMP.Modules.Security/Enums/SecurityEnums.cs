namespace PMP.Modules.Security.Enums;

public enum VisitorStatus
{
    Registered = 0,
    CheckedIn = 1,
    CheckedOut = 2,
    Cancelled = 3,
}

/// <summary>What physical asset an access grant applies to (FR-SEC-004).</summary>
public enum AccessTargetType
{
    Building = 0,
    Unit = 1,
    Facility = 2,
}

/// <summary>Who holds the granted access (FR-SEC-004).</summary>
public enum AccessSubjectType
{
    User = 0,
    Visitor = 1,
}

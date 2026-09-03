using PMP.Shared.Domain;

namespace PMP.Modules.Booking.Entities;

/// <summary>
/// A shared facility (gym, pool, hall…) available for residents to book. Managers
/// configure availability windows and booking rules (FR-BOOK-005).
/// </summary>
public class Facility : BaseEntity
{
    public Guid PropertyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Opening time of the facility in minutes since midnight (e.g. 8*60).</summary>
    public int OpenMinutes { get; set; } = 8 * 60;

    /// <summary>Closing time of the facility in minutes since midnight (e.g. 22*60).</summary>
    public int CloseMinutes { get; set; } = 22 * 60;

    /// <summary>Default slot length in minutes (booking rule).</summary>
    public int SlotMinutes { get; set; } = 60;

    /// <summary>Cancellation window: bookings may be cancelled up to this many hours before start.</summary>
    public int CancellationWindowHours { get; set; } = 2;
}

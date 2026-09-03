using PMP.Modules.Booking.Enums;
using PMP.Shared.Domain;

namespace PMP.Modules.Booking.Entities;

/// <summary>
/// A facility booking for a specific date/time slot (FR-BOOK-002). Overlaps are
/// prevented at creation (FR-BOOK-003); cancellations within the window are allowed
/// (FR-BOOK-004) and the history is retained (FR-BOOK-007).
/// </summary>
public class FacilityBooking : BaseEntity
{
    public Guid FacilityId { get; set; }

    /// <summary>Resident user who made the booking.</summary>
    public Guid BookedByUserId { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Reserved;

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public string BookingReference { get; set; } = string.Empty;

    public Facility? Facility { get; set; }
}

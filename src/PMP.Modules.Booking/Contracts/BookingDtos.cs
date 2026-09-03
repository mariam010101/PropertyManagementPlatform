using System.ComponentModel.DataAnnotations;
using PMP.Modules.Booking.Enums;

namespace PMP.Modules.Booking.Contracts;

public record FacilityDto
{
    public Guid Id { get; init; }

    public Guid PropertyId { get; init; }

    public string PropertyName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int OpenMinutes { get; init; }

    public int CloseMinutes { get; init; }

    public int SlotMinutes { get; init; }

    public int CancellationWindowHours { get; init; }
}

public record CreateFacilityRequest
{
    [Required]
    public Guid PropertyId { get; init; }

    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    public int OpenMinutes { get; init; } = 8 * 60;

    public int CloseMinutes { get; init; } = 22 * 60;

    public int SlotMinutes { get; init; } = 60;

    public int CancellationWindowHours { get; init; } = 2;
}

public record UpdateFacilityRequest
{
    [MaxLength(150)]
    public string? Name { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }

    public int? OpenMinutes { get; init; }

    public int? CloseMinutes { get; init; }

    public int? SlotMinutes { get; init; }

    public int? CancellationWindowHours { get; init; }
}

public record FacilityBookingDto
{
    public Guid Id { get; init; }

    public Guid FacilityId { get; init; }

    public string FacilityName { get; init; } = string.Empty;

    public Guid BookedByUserId { get; init; }

    public string BookedByName { get; init; } = string.Empty;

    public DateTimeOffset StartAt { get; init; }

    public DateTimeOffset EndAt { get; init; }

    public BookingStatus Status { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public string? CancellationReason { get; init; }

    public string BookingReference { get; init; } = string.Empty;
}

public record CreateBookingRequest
{
    [Required]
    public Guid FacilityId { get; init; }

    public DateTimeOffset StartAt { get; init; }

    public DateTimeOffset EndAt { get; init; }
}

public record CancelBookingRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}

public record AvailabilityDto
{
    public Guid FacilityId { get; init; }

    public DateTimeOffset Date { get; init; }

    public int OpenMinutes { get; init; }

    public int CloseMinutes { get; init; }

    public int SlotMinutes { get; init; }

    public IReadOnlyList<FacilityBookingDto> Bookings { get; init; } = [];
}

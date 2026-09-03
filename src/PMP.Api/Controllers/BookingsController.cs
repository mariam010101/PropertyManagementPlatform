using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Booking.Contracts;
using PMP.Modules.Booking.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

/// <summary>
/// Facility booking (BR-009): facilities, availability, reservations with overlap
/// prevention, cancellations within the configured window, and history.
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;
    private readonly CurrentUser _currentUser;

    public BookingsController(IBookingService bookings, CurrentUser currentUser)
    {
        _bookings = bookings;
        _currentUser = currentUser;
    }

    /// <summary>Facilities available to the caller (FR-BOOK-001).</summary>
    [HttpGet("facilities")]
    public async Task<IActionResult> GetFacilities([FromQuery] Guid? propertyId)
    {
        var result = await _bookings.GetFacilitiesAsync(_currentUser.Id, _currentUser.Roles, propertyId);
        return Ok(result);
    }

    [HttpGet("facilities/{facilityId:guid}")]
    public async Task<IActionResult> GetFacility(Guid facilityId)
    {
        var result = await _bookings.GetFacilityAsync(_currentUser.Id, _currentUser.Roles, facilityId);
        return result.ToActionResult();
    }

    /// <summary>Availability for a facility on a date (FR-BOOK-001).</summary>
    [HttpGet("facilities/{facilityId:guid}/availability")]
    public async Task<IActionResult> GetAvailability(Guid facilityId, [FromQuery] DateTimeOffset date)
    {
        var result = await _bookings.GetAvailabilityAsync(_currentUser.Id, _currentUser.Roles, facilityId, date);
        return result.ToActionResult();
    }

    /// <summary>Resident books a facility/time slot (FR-BOOK-002/003).</summary>
    [HttpPost]
    public async Task<IActionResult> Book([FromBody] CreateBookingRequest request)
    {
        var result = await _bookings.BookAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>My bookings, or all (manager/admin) (FR-BOOK-007).</summary>
    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] Guid? facilityId, [FromQuery] bool mineOnly = false)
    {
        var result = await _bookings.GetBookingsAsync(_currentUser.Id, _currentUser.Roles, facilityId, mineOnly);
        return Ok(result);
    }

    /// <summary>Cancel a booking (resident within window / manager anytime) (FR-BOOK-004).</summary>
    [HttpPost("{bookingId:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid bookingId, [FromBody] CancelBookingRequest request)
    {
        var result = await _bookings.CancelBookingAsync(_currentUser.Id, _currentUser.Roles, bookingId, request);
        return result.ToActionResult();
    }

    /// <summary>Manager/admin configures a facility (FR-BOOK-005).</summary>
    [HttpPost("facilities")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateFacility([FromBody] CreateFacilityRequest request)
    {
        var result = await _bookings.CreateFacilityAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }

    /// <summary>Manager/admin updates a facility's availability/rules (FR-BOOK-005).</summary>
    [HttpPut("facilities/{facilityId:guid}")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateFacility(Guid facilityId, [FromBody] UpdateFacilityRequest request)
    {
        var result = await _bookings.UpdateFacilityAsync(_currentUser.Id, _currentUser.Roles, facilityId, request);
        return result.ToActionResult();
    }
}

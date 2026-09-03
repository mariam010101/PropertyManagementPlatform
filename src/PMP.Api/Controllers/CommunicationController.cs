using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMP.Api.Infrastructure;
using PMP.Api.Services;
using PMP.Modules.Communication.Contracts;
using PMP.Modules.Communication.Services;
using PMP.Shared.Common;

namespace PMP.Api.Controllers;

/// <summary>
/// Communication & notifications (BR-007): persisted in-app notifications and
/// property-wide announcements.
/// </summary>
[ApiController]
[Route("api/communication")]
[Authorize]
public class CommunicationController : ControllerBase
{
    private readonly ICommunicationService _communication;
    private readonly CurrentUser _currentUser;

    public CommunicationController(ICommunicationService communication, CurrentUser currentUser)
    {
        _communication = communication;
        _currentUser = currentUser;
    }

    /// <summary>My notifications (FR-COM-005).</summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false)
    {
        var result = await _communication.GetMyNotificationsAsync(_currentUser.Id, unreadOnly);
        return Ok(result);
    }

    /// <summary>Unread count for the notification bell.</summary>
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await _communication.GetUnreadCountAsync(_currentUser.Id);
        return Ok(result);
    }

    [HttpPost("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId)
    {
        var result = await _communication.MarkNotificationReadAsync(_currentUser.Id, notificationId);
        return result.ToActionResult();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var result = await _communication.MarkAllReadAsync(_currentUser.Id);
        return result.ToActionResult();
    }

    /// <summary>Resident feed: announcements relevant to the resident's property (FR-COM-003).</summary>
    [HttpGet("announcements/mine")]
    public async Task<IActionResult> GetResidentAnnouncements()
    {
        var result = await _communication.GetResidentAnnouncementsAsync(_currentUser.Id);
        return Ok(result);
    }

    /// <summary>Manager/admin: announcements for a property (or managed properties).</summary>
    [HttpGet("announcements")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetAnnouncements([FromQuery] Guid? propertyId)
    {
        var result = await _communication.GetAnnouncementsAsync(_currentUser.Id, _currentUser.Roles, propertyId);
        return Ok(result);
    }

    /// <summary>Manager/admin publishes an announcement (FR-COM-002, BRULE-COM-005).</summary>
    [HttpPost("announcements")]
    [Authorize(Policy = AppPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> PublishAnnouncement([FromBody] PublishAnnouncementRequest request)
    {
        var result = await _communication.PublishAnnouncementAsync(_currentUser.Id, _currentUser.Roles, request);
        return result.ToActionResult();
    }
}

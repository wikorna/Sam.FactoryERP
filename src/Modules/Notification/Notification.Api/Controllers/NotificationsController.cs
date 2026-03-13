using FactoryERP.Abstractions.Cqrs;
using FactoryERP.Abstractions.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Notification.Application.Features.GetNotifications;
using Notification.Application.Features.GetUnreadCount;
using Notification.Application.Features.MarkAllAsRead;
using Notification.Application.Features.MarkAsRead;

namespace Notification.Api.Controllers;

/// <summary>
/// In-app notification inbox REST API.
/// All endpoints are scoped to the authenticated user — no cross-user access.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed partial class NotificationsController : ControllerBase
{
    private readonly IMediator             _mediator;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IMediator           mediator,
        ICurrentUserService currentUser,
        ILogger<NotificationsController> logger)
    {
        _mediator    = mediator;
        _currentUser = currentUser;
        _logger      = logger;
    }

    // ── GET /api/notifications?skip=0&take=20 ────────────────────────────────

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId.ToString();
        var result = await _mediator.Send(new GetNotificationsQuery(userId, skip, take), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── GET /api/notifications/unread-count ──────────────────────────────────

    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId.ToString();
        var result = await _mediator.Send(new GetUnreadCountQuery(userId), ct);

        return result.IsSuccess
            ? Ok(new { unreadCount = result.Value })
            : Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── POST /api/notifications/{id:guid}/read ───────────────────────────────

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId.ToString();
        var result = await _mediator.Send(new MarkNotificationAsReadCommand(id, userId), ct);

        if (result.IsSuccess)
            return NoContent();

        if (result.Error.Code == "NotFound")
            return NotFound(result.Error.Message);

        return Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    // ── POST /api/notifications/read-all ─────────────────────────────────────

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId.ToString();
        var result = await _mediator.Send(new MarkAllNotificationsAsReadCommand(userId), ct);

        return result.IsSuccess
            ? NoContent()
            : Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Notification {NotificationId} not found for user {UserId}")]
    private partial void LogNotFound(Guid notificationId, string userId);
}


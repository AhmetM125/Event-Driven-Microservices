using EventDrivenShop.Common.Pagination;
using EventDrivenShop.NotificationService.Application.DTOs;
using EventDrivenShop.NotificationService.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventDrivenShop.NotificationService.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationApplicationService _notificationService;

    public NotificationsController(INotificationApplicationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Lists notifications with server-side pagination and optional order filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetNotificationsQuery(pageNumber, pageSize, orderId);
        var result = await _notificationService.GetNotificationsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all notifications emitted for a specific order.
    /// </summary>
    [HttpGet("by-order/{orderId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrderId([FromRoute] Guid orderId, CancellationToken cancellationToken)
    {
        var notifications = await _notificationService.GetNotificationsByOrderIdAsync(orderId, cancellationToken);
        return Ok(notifications);
    }
}

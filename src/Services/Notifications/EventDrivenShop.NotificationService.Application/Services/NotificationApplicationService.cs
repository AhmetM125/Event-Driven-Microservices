using EventDrivenShop.Common.Inbox;
using EventDrivenShop.Common.Pagination;
using EventDrivenShop.Contracts;
using EventDrivenShop.NotificationService.Application.Common;
using EventDrivenShop.NotificationService.Application.DTOs;
using EventDrivenShop.NotificationService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.NotificationService.Application.Services;

public sealed class NotificationApplicationService : INotificationApplicationService
{
    private readonly INotificationDbContext _dbContext;
    private readonly ILogger<NotificationApplicationService> _logger;

    public NotificationApplicationService(
        INotificationDbContext dbContext,
        ILogger<NotificationApplicationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessPaymentCompletedAsync(
        PaymentCompletedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        const string consumerName = "NotificationService.PaymentCompletedConsumer";

        // Idempotency check via Inbox
        var alreadyProcessed = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == @event.EventId && m.Consumer == consumerName, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate PaymentCompletedIntegrationEvent {EventId} for Order {OrderId} ignored via Inbox.",
                @event.EventId, @event.OrderId);
            return;
        }

        var notification = Notification.Create(
            Guid.NewGuid(),
            @event.OrderId,
            "customer@example.com", // Recipient placeholder derived from workflow
            NotificationType.PaymentSucceeded,
            $"Payment of {@event.Amount:F2} {@event.Currency} for Order {@event.OrderId} was completed successfully.",
            NotificationStatus.Sent);

        _dbContext.Notifications.Add(notification);
        _dbContext.InboxMessages.Add(InboxMessage.Create(@event.EventId, consumerName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created and saved PaymentSucceeded Notification {NotificationId} for Order {OrderId} (EventId: {EventId})",
            notification.Id, @event.OrderId, @event.EventId);
    }

    public async Task ProcessPaymentFailedAsync(
        PaymentFailedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        const string consumerName = "NotificationService.PaymentFailedConsumer";

        // Idempotency check via Inbox
        var alreadyProcessed = await _dbContext.InboxMessages
            .AnyAsync(m => m.EventId == @event.EventId && m.Consumer == consumerName, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Duplicate PaymentFailedIntegrationEvent {EventId} for Order {OrderId} ignored via Inbox.",
                @event.EventId, @event.OrderId);
            return;
        }

        var notification = Notification.Create(
            Guid.NewGuid(),
            @event.OrderId,
            "customer@example.com",
            NotificationType.PaymentFailed,
            $"Payment of {@event.Amount:F2} {@event.Currency} for Order {@event.OrderId} failed. Reason: {@event.Reason}",
            NotificationStatus.Sent);

        _dbContext.Notifications.Add(notification);
        _dbContext.InboxMessages.Add(InboxMessage.Create(@event.EventId, consumerName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created and saved PaymentFailed Notification {NotificationId} for Order {OrderId} (EventId: {EventId})",
            notification.Id, @event.OrderId, @event.EventId);
    }

    public async Task<PagedResult<NotificationResponse>> GetNotificationsAsync(
        GetNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        var dbQuery = _dbContext.Notifications
            .AsNoTracking()
            .AsQueryable();

        if (query.OrderId.HasValue && query.OrderId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(n => n.OrderId == query.OrderId.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var mapped = items.Select(MapToResponse).ToList();
        return new PagedResult<NotificationResponse>(mapped, pagination.PageNumber, pagination.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.OrderId == orderId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return notifications.Select(MapToResponse).ToList();
    }

    private static NotificationResponse MapToResponse(Notification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.OrderId,
            notification.Recipient,
            notification.Type.ToString(),
            notification.Status.ToString(),
            notification.Content,
            notification.CreatedAtUtc,
            notification.ProcessedAtUtc);
    }
}

using EventDrivenShop.Common.Pagination;
using EventDrivenShop.Contracts;
using EventDrivenShop.NotificationService.Application.DTOs;

namespace EventDrivenShop.NotificationService.Application.Services;

public interface INotificationApplicationService
{
    Task ProcessPaymentCompletedAsync(PaymentCompletedIntegrationEvent @event, CancellationToken cancellationToken = default);
    Task ProcessPaymentFailedAsync(PaymentFailedIntegrationEvent @event, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationResponse>> GetNotificationsAsync(GetNotificationsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationResponse>> GetNotificationsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}

using EventDrivenShop.Common.Pagination;
using EventDrivenShop.Contracts;
using EventDrivenShop.OrderService.Application.DTOs;

namespace EventDrivenShop.OrderService.Application.Services;

public interface IOrderApplicationService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderResponse>> GetOrdersAsync(GetOrdersQuery query, CancellationToken cancellationToken = default);
    Task ProcessPaymentCompletedAsync(PaymentCompletedIntegrationEvent @event, CancellationToken cancellationToken = default);
    Task ProcessPaymentFailedAsync(PaymentFailedIntegrationEvent @event, CancellationToken cancellationToken = default);
}

using EventDrivenShop.Contracts;
using EventDrivenShop.PaymentService.Application.DTOs;

namespace EventDrivenShop.PaymentService.Application.Services;

public interface IPaymentApplicationService
{
    Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent @event, CancellationToken cancellationToken = default);
    Task<PaymentResponse?> GetPaymentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentResponse?> GetPaymentByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}

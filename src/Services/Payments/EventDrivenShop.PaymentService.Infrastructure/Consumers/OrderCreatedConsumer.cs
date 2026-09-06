using EventDrivenShop.Contracts;
using EventDrivenShop.Observability;
using EventDrivenShop.PaymentService.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.PaymentService.Infrastructure.Consumers;

public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly IPaymentApplicationService _paymentService;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IPaymentApplicationService paymentService,
        ILogger<OrderCreatedConsumer> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var correlationId = context.CorrelationId?.ToString("D") ??
            context.Headers.Get<string>("CorrelationId") ??
            context.Message.CorrelationId;

        using (CorrelationIdContext.Set(correlationId))
        {
            _logger.LogInformation(
                "PaymentService received OrderCreatedIntegrationEvent for OrderId {OrderId}, Amount {Amount} {Currency}, CorrelationId {CorrelationId}",
                context.Message.OrderId, context.Message.TotalAmount, context.Message.Currency, correlationId);

            await _paymentService.ProcessOrderCreatedAsync(context.Message, context.CancellationToken);
        }
    }
}

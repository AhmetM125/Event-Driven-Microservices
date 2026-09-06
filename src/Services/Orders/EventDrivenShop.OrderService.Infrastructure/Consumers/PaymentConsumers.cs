using EventDrivenShop.Contracts;
using EventDrivenShop.Observability;
using EventDrivenShop.OrderService.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.OrderService.Infrastructure.Consumers;

public sealed class PaymentCompletedConsumer : IConsumer<PaymentCompletedIntegrationEvent>
{
    private readonly IOrderApplicationService _orderService;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(
        IOrderApplicationService orderService,
        ILogger<PaymentCompletedConsumer> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> context)
    {
        var correlationId = context.CorrelationId?.ToString("D") ??
            context.Headers.Get<string>("CorrelationId") ??
            context.Message.CorrelationId;

        using (CorrelationIdContext.Set(correlationId))
        {
            _logger.LogInformation(
                "Received PaymentCompletedIntegrationEvent for OrderId {OrderId}, PaymentId {PaymentId}, CorrelationId {CorrelationId}",
                context.Message.OrderId, context.Message.PaymentId, correlationId);

            await _orderService.ProcessPaymentCompletedAsync(context.Message, context.CancellationToken);
        }
    }
}

public sealed class PaymentFailedConsumer : IConsumer<PaymentFailedIntegrationEvent>
{
    private readonly IOrderApplicationService _orderService;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(
        IOrderApplicationService orderService,
        ILogger<PaymentFailedConsumer> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedIntegrationEvent> context)
    {
        var correlationId = context.CorrelationId?.ToString("D") ??
            context.Headers.Get<string>("CorrelationId") ??
            context.Message.CorrelationId;

        using (CorrelationIdContext.Set(correlationId))
        {
            _logger.LogInformation(
                "Received PaymentFailedIntegrationEvent for OrderId {OrderId}, Reason {Reason}, CorrelationId {CorrelationId}",
                context.Message.OrderId, context.Message.Reason, correlationId);

            await _orderService.ProcessPaymentFailedAsync(context.Message, context.CancellationToken);
        }
    }
}

using EventDrivenShop.Contracts;
using EventDrivenShop.NotificationService.Application.Services;
using EventDrivenShop.Observability;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.NotificationService.Infrastructure.Consumers;

public sealed class PaymentCompletedNotificationConsumer : IConsumer<PaymentCompletedIntegrationEvent>
{
    private readonly INotificationApplicationService _notificationService;
    private readonly ILogger<PaymentCompletedNotificationConsumer> _logger;

    public PaymentCompletedNotificationConsumer(
        INotificationApplicationService notificationService,
        ILogger<PaymentCompletedNotificationConsumer> logger)
    {
        _notificationService = notificationService;
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
                "NotificationService received PaymentCompletedIntegrationEvent for OrderId {OrderId}, PaymentId {PaymentId}, CorrelationId {CorrelationId}",
                context.Message.OrderId, context.Message.PaymentId, correlationId);

            await _notificationService.ProcessPaymentCompletedAsync(context.Message, context.CancellationToken);
        }
    }
}

public sealed class PaymentFailedNotificationConsumer : IConsumer<PaymentFailedIntegrationEvent>
{
    private readonly INotificationApplicationService _notificationService;
    private readonly ILogger<PaymentFailedNotificationConsumer> _logger;

    public PaymentFailedNotificationConsumer(
        INotificationApplicationService notificationService,
        ILogger<PaymentFailedNotificationConsumer> logger)
    {
        _notificationService = notificationService;
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
                "NotificationService received PaymentFailedIntegrationEvent for OrderId {OrderId}, Reason {Reason}, CorrelationId {CorrelationId}",
                context.Message.OrderId, context.Message.Reason, correlationId);

            await _notificationService.ProcessPaymentFailedAsync(context.Message, context.CancellationToken);
        }
    }
}

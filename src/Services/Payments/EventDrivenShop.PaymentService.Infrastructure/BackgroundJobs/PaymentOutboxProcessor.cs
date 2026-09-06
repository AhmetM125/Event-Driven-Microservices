using EventDrivenShop.Common.Outbox;
using EventDrivenShop.PaymentService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.PaymentService.Infrastructure.BackgroundJobs;

public sealed class PaymentOutboxProcessor : OutboxProcessorBackgroundService<PaymentDbContext>
{
    public PaymentOutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<PaymentOutboxProcessor> logger)
        : base(serviceProvider, logger, TimeSpan.FromMilliseconds(500))
    {
    }
}

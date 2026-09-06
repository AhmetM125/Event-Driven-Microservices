using EventDrivenShop.Common.Outbox;
using EventDrivenShop.OrderService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.OrderService.Infrastructure.BackgroundJobs;

public sealed class OrderOutboxProcessor : OutboxProcessorBackgroundService<OrderDbContext>
{
    public OrderOutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OrderOutboxProcessor> logger)
        : base(serviceProvider, logger, TimeSpan.FromMilliseconds(500))
    {
    }
}

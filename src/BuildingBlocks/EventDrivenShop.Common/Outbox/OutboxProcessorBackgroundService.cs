using EventDrivenShop.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventDrivenShop.Common.Outbox;

public abstract class OutboxProcessorBackgroundService<TDbContext> : BackgroundService
    where TDbContext : DbContext, IOutboxDbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly TimeSpan _pollingInterval;
    private const int BatchSize = 20;
    private const int MaxRetries = 5;

    protected OutboxProcessorBackgroundService(
        IServiceProvider serviceProvider,
        ILogger logger,
        TimeSpan? pollingInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor background service started for {DbContextName}", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessPendingMessagesAsync(stoppingToken);

                if (processedCount == 0)
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred in Outbox processor loop for {DbContextName}", typeof(TDbContext).Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox processor background service stopped for {DbContextName}", typeof(TDbContext).Name);
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // Query pending messages ordered by OccurredAtUtc
        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        foreach (var message in pendingMessages)
        {
            using (CorrelationIdContext.Set(message.CorrelationId))
            {
                try
                {
                    var eventObject = EventRegistry.Deserialize(message.EventType, message.Payload);
                    if (eventObject is null)
                    {
                        throw new InvalidOperationException($"Deserialized payload for event type {message.EventType} was null.");
                    }

                    // Publish message with CorrelationId propagated in header and context
                    await publishEndpoint.Publish(eventObject, eventObject.GetType(), context =>
                    {
                        context.CorrelationId = Guid.TryParse(message.CorrelationId, out var parsedGuid)
                            ? parsedGuid
                            : null;
                        context.Headers.Set("CorrelationId", message.CorrelationId);
                        context.Headers.Set("EventId", message.Id.ToString("D"));
                    }, cancellationToken);

                    message.ProcessedAtUtc = DateTime.UtcNow;
                    message.LastError = null;

                    _logger.LogInformation(
                        "Successfully published outbox message {EventId} of type {EventType} with CorrelationId {CorrelationId}",
                        message.Id, message.EventType, message.CorrelationId);
                }
                catch (Exception ex)
                {
                    message.RetryCount++;
                    message.LastError = $"{ex.GetType().Name}: {ex.Message}";

                    _logger.LogWarning(
                        ex,
                        "Failed to publish outbox message {EventId} (Attempt {RetryCount}/{MaxRetries}): {ErrorMessage}",
                        message.Id, message.RetryCount, MaxRetries, ex.Message);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pendingMessages.Count;
    }
}

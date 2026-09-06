using System.Text.Json;
using EventDrivenShop.Contracts;

namespace EventDrivenShop.Common.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    public static OutboxMessage FromEvent<TEvent>(TEvent @event) where TEvent : class, IIntegrationEvent
    {
        return new OutboxMessage
        {
            Id = @event.EventId,
            EventType = typeof(TEvent).FullName ?? typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(@event, OutboxJsonOptions.Default),
            OccurredAtUtc = @event.OccurredAtUtc,
            CorrelationId = @event.CorrelationId
        };
    }
}

public static class OutboxJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

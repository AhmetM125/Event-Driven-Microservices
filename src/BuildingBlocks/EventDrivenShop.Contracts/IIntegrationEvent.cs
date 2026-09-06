namespace EventDrivenShop.Contracts;

/// <summary>
/// Marker interface for all integration events.
/// Integration events represent public contracts across service boundaries.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
    string CorrelationId { get; }
}

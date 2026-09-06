namespace EventDrivenShop.Common.Inbox;

/// <summary>
/// Persisted record of processed message IDs per consumer.
/// Ensures consumer deduplication and at-least-once message tolerance.
/// </summary>
public sealed class InboxMessage
{
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;

    public static InboxMessage Create(Guid eventId, string consumer)
    {
        return new InboxMessage
        {
            EventId = eventId,
            Consumer = consumer,
            ProcessedAtUtc = DateTime.UtcNow
        };
    }
}

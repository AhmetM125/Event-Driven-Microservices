namespace EventDrivenShop.NotificationService.Domain;

public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    // EF Core constructor
    private Notification() { }

    public static Notification Create(
        Guid id,
        Guid orderId,
        string recipient,
        NotificationType type,
        string content,
        NotificationStatus status = NotificationStatus.Sent)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Notification ID cannot be empty.", nameof(id));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("Recipient cannot be empty.", nameof(recipient));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        }

        return new Notification
        {
            Id = id,
            OrderId = orderId,
            Recipient = recipient.Trim(),
            Type = type,
            Status = status,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            ProcessedAtUtc = DateTime.UtcNow
        };
    }
}

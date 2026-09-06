namespace EventDrivenShop.PaymentService.Domain;

public sealed class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // EF Core constructor
    private Payment() { }

    public static Payment Create(
        Guid id,
        Guid orderId,
        decimal amount,
        string currency,
        PaymentStatus initialStatus = PaymentStatus.Pending,
        string? failureReason = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty.", nameof(id));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
        }

        return new Payment
        {
            Id = id,
            OrderId = orderId,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            Status = initialStatus,
            FailureReason = failureReason,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkAsCompleted()
    {
        if (Status == PaymentStatus.Completed)
        {
            return;
        }

        if (Status == PaymentStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot mark failed payment '{Id}' as completed.");
        }

        Status = PaymentStatus.Completed;
        FailureReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        if (Status == PaymentStatus.Failed)
        {
            return;
        }

        if (Status == PaymentStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot mark already completed payment '{Id}' as failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

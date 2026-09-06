namespace EventDrivenShop.OrderService.Domain;

public sealed class Order
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "TRY"
    };

    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // EF Core constructor
    private Order() { }

    public static Order Create(
        Guid id,
        Guid customerId,
        string customerEmail,
        string currency,
        IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(id));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(customerEmail) || !customerEmail.Contains('@'))
        {
            throw new ArgumentException("A valid customer email is required.", nameof(customerEmail));
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SupportedCurrencies.Contains(normalizedCurrency))
        {
            throw new ArgumentException($"Unsupported currency '{currency}'. Supported: {string.Join(", ", SupportedCurrencies)}", nameof(currency));
        }

        var itemsList = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
        if (itemsList.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            CustomerEmail = customerEmail.Trim().ToLowerInvariant(),
            Currency = normalizedCurrency,
            Status = OrderStatus.PendingPayment,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var item in itemsList)
        {
            var orderItem = new OrderItem(
                Guid.NewGuid(),
                order.Id,
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice);

            order._items.Add(orderItem);
        }

        // Calculate total server-side
        order.TotalAmount = order._items.Sum(i => i.TotalPrice);

        return order;
    }

    public void MarkAsPaid()
    {
        if (Status == OrderStatus.Paid)
        {
            // Idempotent no-op
            return;
        }

        if (Status == OrderStatus.PaymentFailed)
        {
            throw new InvalidOperationException($"Cannot transition order '{Id}' from {Status} to {OrderStatus.Paid}. Order payment already failed.");
        }

        Status = OrderStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsPaymentFailed(string reason)
    {
        if (Status == OrderStatus.PaymentFailed)
        {
            // Idempotent no-op
            return;
        }

        if (Status == OrderStatus.Paid)
        {
            throw new InvalidOperationException($"Cannot transition order '{Id}' from {OrderStatus.Paid} to {OrderStatus.PaymentFailed}. Order has already been paid.");
        }

        Status = OrderStatus.PaymentFailed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

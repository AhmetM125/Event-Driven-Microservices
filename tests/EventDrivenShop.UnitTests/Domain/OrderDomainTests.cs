using EventDrivenShop.OrderService.Domain;
using FluentAssertions;
using Xunit;

namespace EventDrivenShop.UnitTests.Domain;

public sealed class OrderDomainTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldCalculateTotalAmountAndSetPendingPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var customerEmail = "buyer@example.com";
        var currency = "EUR";
        var items = new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>
        {
            (Guid.NewGuid(), "Mechanical Keyboard", 2, 120.00m),
            (Guid.NewGuid(), "USB-C Cable", 3, 15.50m)
        };

        // Act
        var order = Order.Create(orderId, customerId, customerEmail, currency, items);

        // Assert
        order.Id.Should().Be(orderId);
        order.CustomerId.Should().Be(customerId);
        order.CustomerEmail.Should().Be(customerEmail);
        order.Currency.Should().Be("EUR");
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.Items.Should().HaveCount(2);

        // 2 * 120.00 + 3 * 15.50 = 240.00 + 46.50 = 286.50
        order.TotalAmount.Should().Be(286.50m);
        order.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        order.UpdatedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-email")]
    public void Create_WithInvalidEmail_ShouldThrowArgumentException(string invalidEmail)
    {
        // Arrange
        var items = new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>
        {
            (Guid.NewGuid(), "Item", 1, 10.00m)
        };

        // Act
        var act = () => Order.Create(Guid.NewGuid(), Guid.NewGuid(), invalidEmail, "USD", items);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*valid customer email*");
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("BITCOIN")]
    [InlineData("")]
    public void Create_WithUnsupportedCurrency_ShouldThrowArgumentException(string unsupportedCurrency)
    {
        // Arrange
        var items = new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>
        {
            (Guid.NewGuid(), "Item", 1, 10.00m)
        };

        // Act
        var act = () => Order.Create(Guid.NewGuid(), Guid.NewGuid(), "test@example.com", unsupportedCurrency, items);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported currency*");
    }

    [Fact]
    public void Create_WithNoItems_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Order.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test@example.com",
            "USD",
            new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one item*");
    }

    [Fact]
    public void MarkAsPaid_FromPendingPayment_ShouldTransitionToPaid()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.MarkAsPaid();

        // Assert
        order.Status.Should().Be(OrderStatus.Paid);
        order.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_ShouldBeIdempotentNoOp()
    {
        // Arrange
        var order = CreateTestOrder();
        order.MarkAsPaid();
        var updatedTime = order.UpdatedAtUtc;

        // Act
        order.MarkAsPaid();

        // Assert
        order.Status.Should().Be(OrderStatus.Paid);
        order.UpdatedAtUtc.Should().Be(updatedTime);
    }

    [Fact]
    public void MarkAsPaid_WhenOrderPaymentFailed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.MarkAsPaymentFailed("Card declined");

        // Act
        var act = () => order.MarkAsPaid();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition order*from PaymentFailed to Paid*");
    }

    [Fact]
    public void MarkAsPaymentFailed_FromPendingPayment_ShouldTransitionToPaymentFailed()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.MarkAsPaymentFailed("Insufficient funds");

        // Assert
        order.Status.Should().Be(OrderStatus.PaymentFailed);
        order.FailureReason.Should().Be("Insufficient funds");
        order.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsPaymentFailed_WhenAlreadyPaid_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.MarkAsPaid();

        // Act
        var act = () => order.MarkAsPaymentFailed("Late failure");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition order*from Paid to PaymentFailed*");
    }

    private static Order CreateTestOrder()
    {
        return Order.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test@example.com",
            "USD",
            new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>
            {
                (Guid.NewGuid(), "Sample Item", 1, 50.00m)
            });
    }
}

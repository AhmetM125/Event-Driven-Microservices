using EventDrivenShop.PaymentService.Application.Services;
using EventDrivenShop.PaymentService.Domain;
using FluentAssertions;
using Xunit;

namespace EventDrivenShop.UnitTests.Domain;

public sealed class PaymentDomainTests
{
    [Fact]
    public void Create_WithValidInputs_ShouldInitializePaymentCorrectly()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var amount = 150.75m;
        var currency = "EUR";

        // Act
        var payment = Payment.Create(paymentId, orderId, amount, currency);

        // Assert
        payment.Id.Should().Be(paymentId);
        payment.OrderId.Should().Be(orderId);
        payment.Amount.Should().Be(amount);
        payment.Currency.Should().Be("EUR");
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.FailureReason.Should().BeNull();
        payment.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithInvalidAmount_ShouldThrowArgumentException(decimal invalidAmount)
    {
        // Act
        var act = () => Payment.Create(Guid.NewGuid(), Guid.NewGuid(), invalidAmount, "USD");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void MarkAsCompleted_FromPending_ShouldTransitionToCompleted()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD");

        // Act
        payment.MarkAsCompleted();

        // Assert
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.FailureReason.Should().BeNull();
        payment.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsCompleted_WhenFailed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD");
        payment.MarkAsFailed("Card declined");

        // Act
        var act = () => payment.MarkAsCompleted();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot mark failed payment*as completed*");
    }

    [Fact]
    public void MarkAsFailed_FromPending_ShouldTransitionToFailed()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD");

        // Act
        payment.MarkAsFailed("Declined");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Declined");
        payment.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_WhenAlreadyCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD");
        payment.MarkAsCompleted();

        // Act
        var act = () => payment.MarkAsFailed("Late rejection");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot mark already completed payment*as failed*");
    }
}

public sealed class PaymentSimulatorTests
{
    private readonly PaymentSimulator _simulator = new();

    [Theory]
    [InlineData("customer@example.com", "USD")]
    [InlineData("alice.smith@shop.org", "EUR")]
    [InlineData("john@company.co.uk", "GBP")]
    public void Simulate_NormalCustomer_ShouldReturnSuccess(string email, string currency)
    {
        // Act
        var result = _simulator.Simulate(email, 99.99m, currency);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FailureReason.Should().BeNull();
    }

    [Theory]
    [InlineData("fail@example.com", "USD")]
    [InlineData("fail-payment@test.com", "EUR")]
    [InlineData("failure_user@domain.com", "USD")]
    [InlineData("normal@example.com", "FAIL")]
    public void Simulate_DeterministicFailureTrigger_ShouldReturnFailure(string email, string currency)
    {
        // Act
        var result = _simulator.Simulate(email, 100m, currency);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }
}

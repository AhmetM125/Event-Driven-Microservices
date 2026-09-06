using EventDrivenShop.Common.Outbox;
using EventDrivenShop.Contracts;
using FluentAssertions;
using Xunit;

namespace EventDrivenShop.UnitTests.Serialization;

public sealed class OutboxSerializationTests
{
    [Fact]
    public void FromEvent_WithOrderCreatedEvent_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var originalEvent = new OrderCreatedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("D"),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            TotalAmount = 250.50m,
            Currency = "EUR",
            Items = new List<OrderItemDto>
            {
                new(Guid.NewGuid(), "Product A", 2, 100.00m, 200.00m),
                new(Guid.NewGuid(), "Product B", 1, 50.50m, 50.50m)
            }
        };

        // Act
        var outboxMessage = OutboxMessage.FromEvent(originalEvent);
        var deserialized = EventRegistry.Deserialize(outboxMessage.EventType, outboxMessage.Payload) as OrderCreatedIntegrationEvent;

        // Assert
        outboxMessage.Id.Should().Be(originalEvent.EventId);
        outboxMessage.CorrelationId.Should().Be(originalEvent.CorrelationId);
        outboxMessage.OccurredAtUtc.Should().Be(originalEvent.OccurredAtUtc);

        deserialized.Should().NotBeNull();
        deserialized!.OrderId.Should().Be(originalEvent.OrderId);
        deserialized.CustomerId.Should().Be(originalEvent.CustomerId);
        deserialized.CustomerEmail.Should().Be(originalEvent.CustomerEmail);
        deserialized.TotalAmount.Should().Be(originalEvent.TotalAmount);
        deserialized.Currency.Should().Be(originalEvent.Currency);
        deserialized.CorrelationId.Should().Be(originalEvent.CorrelationId);
        deserialized.Items.Should().HaveCount(2);
        deserialized.Items[0].ProductName.Should().Be("Product A");
    }

    [Fact]
    public void FromEvent_WithPaymentCompletedEvent_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var originalEvent = new PaymentCompletedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-12345",
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Amount = 99.00m,
            Currency = "USD"
        };

        // Act
        var outboxMessage = OutboxMessage.FromEvent(originalEvent);
        var deserialized = EventRegistry.Deserialize(outboxMessage.EventType, outboxMessage.Payload) as PaymentCompletedIntegrationEvent;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.PaymentId.Should().Be(originalEvent.PaymentId);
        deserialized.OrderId.Should().Be(originalEvent.OrderId);
        deserialized.Amount.Should().Be(99.00m);
        deserialized.Currency.Should().Be("USD");
        deserialized.CorrelationId.Should().Be("corr-12345");
    }

    [Fact]
    public void FromEvent_WithPaymentFailedEvent_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var originalEvent = new PaymentFailedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-fail-99",
            PaymentId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Amount = 149.99m,
            Currency = "GBP",
            Reason = "Card expired"
        };

        // Act
        var outboxMessage = OutboxMessage.FromEvent(originalEvent);
        var deserialized = EventRegistry.Deserialize(outboxMessage.EventType, outboxMessage.Payload) as PaymentFailedIntegrationEvent;

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.PaymentId.Should().Be(originalEvent.PaymentId);
        deserialized.OrderId.Should().Be(originalEvent.OrderId);
        deserialized.Amount.Should().Be(149.99m);
        deserialized.Currency.Should().Be("GBP");
        deserialized.Reason.Should().Be("Card expired");
        deserialized.CorrelationId.Should().Be("corr-fail-99");
    }

    [Fact]
    public void Deserialize_WithUnknownType_ShouldThrowInvalidOperationException()
    {
        // Act
        var act = () => EventRegistry.Deserialize("Unknown.RandomEvent", "{}");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unrecognized or unregistered*");
    }
}

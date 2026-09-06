using EventDrivenShop.Contracts;
using EventDrivenShop.PaymentService.Application.Services;
using EventDrivenShop.PaymentService.Domain;
using EventDrivenShop.PaymentService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventDrivenShop.IntegrationTests.Payments;

public sealed class PaymentDeduplicationIntegrationTests
{
    private static PaymentDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentsDb_{Guid.NewGuid()}")
            .Options;

        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task ProcessOrderCreated_WhenDeliveredMultipleTimes_ShouldCreateExactlyOnePaymentAndOutbox()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var simulator = new PaymentSimulator();
        var service = new PaymentApplicationService(dbContext, simulator, NullLogger<PaymentApplicationService>.Instance);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var correlationId = "corr-dedup-test";

        var orderCreatedEvent = new OrderCreatedIntegrationEvent
        {
            EventId = eventId,
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = correlationId,
            OrderId = orderId,
            CustomerId = Guid.NewGuid(),
            CustomerEmail = "customer@example.com",
            TotalAmount = 99.99m,
            Currency = "USD"
        };

        // Act - First message delivery
        await service.ProcessOrderCreatedAsync(orderCreatedEvent);

        // Act - Duplicate message delivery (simulating RabbitMQ at-least-once redelivery)
        await service.ProcessOrderCreatedAsync(orderCreatedEvent);

        // Act - Third duplicate delivery
        await service.ProcessOrderCreatedAsync(orderCreatedEvent);

        // Assert - Exactly 1 Payment persisted
        var payments = await dbContext.Payments.Where(p => p.OrderId == orderId).ToListAsync();
        payments.Should().HaveCount(1);
        payments[0].Status.Should().Be(PaymentStatus.Completed);
        payments[0].Amount.Should().Be(99.99m);

        // Assert - Exactly 1 Outbox message generated
        var outboxMessages = await dbContext.OutboxMessages.Where(m => m.CorrelationId == correlationId).ToListAsync();
        outboxMessages.Should().HaveCount(1);
        outboxMessages[0].EventType.Should().Contain(nameof(PaymentCompletedIntegrationEvent));

        // Assert - Exactly 1 Inbox entry recorded for this consumer
        var inboxEntries = await dbContext.InboxMessages.Where(m => m.EventId == eventId).ToListAsync();
        inboxEntries.Should().HaveCount(1);
        inboxEntries[0].Consumer.Should().Be("PaymentService.OrderCreatedConsumer");
    }

    [Fact]
    public async Task ProcessOrderCreated_WhenPaymentFails_ShouldEmitPaymentFailedOutboxMessage()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var simulator = new PaymentSimulator();
        var service = new PaymentApplicationService(dbContext, simulator, NullLogger<PaymentApplicationService>.Instance);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var correlationId = "corr-fail-test";

        var orderCreatedEvent = new OrderCreatedIntegrationEvent
        {
            EventId = eventId,
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = correlationId,
            OrderId = orderId,
            CustomerId = Guid.NewGuid(),
            CustomerEmail = "fail-payment@example.com", // Deterministic failure trigger
            TotalAmount = 50.00m,
            Currency = "USD"
        };

        // Act
        await service.ProcessOrderCreatedAsync(orderCreatedEvent);

        // Assert
        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().NotBeNullOrWhiteSpace();

        var outbox = await dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.CorrelationId == correlationId);
        outbox.Should().NotBeNull();
        outbox!.EventType.Should().Contain(nameof(PaymentFailedIntegrationEvent));
        outbox.Payload.Should().Contain(orderId.ToString());
    }
}

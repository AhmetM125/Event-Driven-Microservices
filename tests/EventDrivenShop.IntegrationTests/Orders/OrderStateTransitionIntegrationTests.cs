using EventDrivenShop.Contracts;
using EventDrivenShop.OrderService.Application.DTOs;
using EventDrivenShop.OrderService.Application.Services;
using EventDrivenShop.OrderService.Application.Validators;
using EventDrivenShop.OrderService.Domain;
using EventDrivenShop.OrderService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventDrivenShop.IntegrationTests.Orders;

public sealed class OrderStateTransitionIntegrationTests
{
    private static OrderDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: $"OrdersDb_{Guid.NewGuid()}")
            .Options;

        return new OrderDbContext(options);
    }

    [Fact]
    public async Task ProcessPaymentCompleted_WhenReceived_ShouldTransitionOrderToPaid()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var validator = new CreateOrderRequestValidator();
        var service = new OrderApplicationService(dbContext, validator, NullLogger<OrderApplicationService>.Instance);

        var created = await service.CreateOrderAsync(
            new CreateOrderRequest(Guid.NewGuid(), "buyer@example.com", "USD", [new(Guid.NewGuid(), "Item", 1, 100m)]),
            "corr-1");

        var paymentCompletedEvent = new PaymentCompletedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-1",
            PaymentId = Guid.NewGuid(),
            OrderId = created.Id,
            Amount = 100m,
            Currency = "USD"
        };

        // Act - Consume event
        await service.ProcessPaymentCompletedAsync(paymentCompletedEvent);

        // Assert
        var order = await dbContext.Orders.FindAsync(created.Id);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Paid);

        var inbox = await dbContext.InboxMessages.FirstOrDefaultAsync(m => m.EventId == paymentCompletedEvent.EventId);
        inbox.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPaymentCompleted_WhenDeliveredTwice_ShouldBeIdempotent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var validator = new CreateOrderRequestValidator();
        var service = new OrderApplicationService(dbContext, validator, NullLogger<OrderApplicationService>.Instance);

        var created = await service.CreateOrderAsync(
            new CreateOrderRequest(Guid.NewGuid(), "buyer@example.com", "USD", [new(Guid.NewGuid(), "Item", 1, 100m)]),
            "corr-2");

        var paymentCompletedEvent = new PaymentCompletedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-2",
            PaymentId = Guid.NewGuid(),
            OrderId = created.Id,
            Amount = 100m,
            Currency = "USD"
        };

        // Act - Deliver twice
        await service.ProcessPaymentCompletedAsync(paymentCompletedEvent);
        await service.ProcessPaymentCompletedAsync(paymentCompletedEvent);

        // Assert - Status remains Paid and exactly one inbox record
        var order = await dbContext.Orders.FindAsync(created.Id);
        order!.Status.Should().Be(OrderStatus.Paid);

        var inboxCount = await dbContext.InboxMessages.CountAsync(m => m.EventId == paymentCompletedEvent.EventId);
        inboxCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPaymentFailed_WhenOrderAlreadyPaid_ShouldSafelyPreservePaidStatus()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var validator = new CreateOrderRequestValidator();
        var service = new OrderApplicationService(dbContext, validator, NullLogger<OrderApplicationService>.Instance);

        var created = await service.CreateOrderAsync(
            new CreateOrderRequest(Guid.NewGuid(), "buyer@example.com", "USD", [new(Guid.NewGuid(), "Item", 1, 100m)]),
            "corr-3");

        // Order marked as Paid first
        await service.ProcessPaymentCompletedAsync(new PaymentCompletedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-3",
            PaymentId = Guid.NewGuid(),
            OrderId = created.Id,
            Amount = 100m,
            Currency = "USD"
        });

        // A stale/out-of-order PaymentFailed arrives later
        var lateFailedEvent = new PaymentFailedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = "corr-3",
            PaymentId = Guid.NewGuid(),
            OrderId = created.Id,
            Amount = 100m,
            Currency = "USD",
            Reason = "Stale failure event"
        };

        // Act
        var act = () => service.ProcessPaymentFailedAsync(lateFailedEvent);

        // Assert - Transition from Paid to PaymentFailed is prevented by domain state invariants
        await act.Should().ThrowAsync<InvalidOperationException>();

        var order = await dbContext.Orders.FindAsync(created.Id);
        order!.Status.Should().Be(OrderStatus.Paid);
    }
}

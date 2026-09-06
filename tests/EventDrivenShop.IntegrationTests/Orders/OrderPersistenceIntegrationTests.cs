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

public sealed class OrderPersistenceIntegrationTests
{
    private static OrderDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: $"OrdersDb_{Guid.NewGuid()}")
            .Options;

        return new OrderDbContext(options);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldPersistOrderAndOutboxMessageAtomically()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var validator = new CreateOrderRequestValidator();
        var service = new OrderApplicationService(dbContext, validator, NullLogger<OrderApplicationService>.Instance);

        var request = new CreateOrderRequest(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "alice@example.com",
            Currency: "USD",
            Items: new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Mechanical Keyboard", 1, 150.00m),
                new(Guid.NewGuid(), "Wireless Mouse", 2, 45.00m)
            });

        var correlationId = Guid.NewGuid().ToString("D");

        // Act
        var result = await service.CreateOrderAsync(request, correlationId);

        // Assert - verify API return value
        result.Should().NotBeNull();
        result.Status.Should().Be(OrderStatus.PendingPayment.ToString());
        result.TotalAmount.Should().Be(240.00m); // 150 + 2 * 45
        result.Items.Should().HaveCount(2);

        // Assert - verify DB persistence of Order
        var persistedOrder = await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == result.Id);

        persistedOrder.Should().NotBeNull();
        persistedOrder!.TotalAmount.Should().Be(240.00m);
        persistedOrder.Status.Should().Be(OrderStatus.PendingPayment);
        persistedOrder.Items.Should().HaveCount(2);

        // Assert - verify DB persistence of Outbox Message
        var outboxMessage = await dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.CorrelationId == correlationId);

        outboxMessage.Should().NotBeNull();
        outboxMessage!.EventType.Should().Contain(nameof(OrderCreatedIntegrationEvent));
        outboxMessage.ProcessedAtUtc.Should().BeNull(); // Pending outbox processor publish
        outboxMessage.Payload.Should().Contain("alice@example.com");
        outboxMessage.Payload.Should().Contain("240");
    }

    [Fact]
    public async Task GetOrdersAsync_WithFiltersAndPagination_ShouldReturnCorrectSubset()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var validator = new CreateOrderRequestValidator();
        var service = new OrderApplicationService(dbContext, validator, NullLogger<OrderApplicationService>.Instance);

        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            await service.CreateOrderAsync(new CreateOrderRequest(
                customerA, $"a{i}@test.com", "USD", [new(Guid.NewGuid(), $"Product {i}", 1, 10m)]),
                Guid.NewGuid().ToString("D"));
        }

        for (var i = 0; i < 3; i++)
        {
            await service.CreateOrderAsync(new CreateOrderRequest(
                customerB, $"b{i}@test.com", "EUR", [new(Guid.NewGuid(), $"Product {i}", 1, 20m)]),
                Guid.NewGuid().ToString("D"));
        }

        // Act - Query customer A's orders with pagination
        var pagedForCustomerA = await service.GetOrdersAsync(new GetOrdersQuery(
            PageNumber: 1,
            PageSize: 3,
            CustomerId: customerA));

        // Assert
        pagedForCustomerA.TotalCount.Should().Be(5);
        pagedForCustomerA.Items.Should().HaveCount(3);
        pagedForCustomerA.TotalPages.Should().Be(2);
        pagedForCustomerA.HasNextPage.Should().BeTrue();
    }
}
